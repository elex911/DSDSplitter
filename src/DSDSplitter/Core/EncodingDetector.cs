using System.Text;

namespace DSDSplitter.Core;

/// <summary>
/// 编码自动探测（见开发文档 6.2，已修正 v2.0 的 try/catch 永远成功问题）。
/// 策略：BOM 优先；否则对 UTF-8/GB18030/Big5/Shift-JIS 都解码，
/// 取「替换符 U+FFFD 最少 + CUE 关键字命中最多」者。
/// </summary>
public static class EncodingDetector
{
    private static readonly string[] CueKeywords =
    {
        "TRACK", "INDEX", "FILE", "TITLE", "PERFORMER", "REM",
    };

    static EncodingDetector()
    {
        // 注册 GB18030 / Big5 / Shift-JIS 等代码页
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding Detect(byte[] raw)
    {
        // 1) BOM 优先
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            return new UTF8Encoding(false);
        if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE) return Encoding.Unicode;
        if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF) return Encoding.BigEndianUnicode;

        // 2) 候选编码都解一遍打分。
        //    关键：用 ExceptionFallback，遇到非法字节才会抛异常，从而能被排除。
        var candidates = new (string Name, Encoding Strict)[]
        {
            ("utf8",    new UTF8Encoding(false, true)),
            ("gb18030", MakeStrict("GB18030")),
            ("big5",    MakeStrict("Big5")),
            ("sjis",    MakeStrict("Shift_JIS")),
        };

        string? best = null;
        var bestScore = int.MaxValue;
        foreach (var c in candidates)
        {
            string s;
            try { s = c.Strict.GetString(raw); }
            catch { continue; } // 严格模式失败 = 排除该编码

            var replacement = s.Count(ch => ch == '\uFFFD');
            var hits = CountCueKeywords(s);
            var score = replacement * 100 - hits;
            if (score < bestScore)
            {
                bestScore = score;
                best = c.Name;
            }
        }

        // 返回「正常（非严格）」编码用于真正解码写回
        return best switch
        {
            "utf8" => new UTF8Encoding(false),
            "gb18030" => Encoding.GetEncoding("GB18030"),
            "big5" => Encoding.GetEncoding("Big5"),
            "sjis" => Encoding.GetEncoding("Shift_JIS"),
            _ => Encoding.Latin1, // 兜底
        };
    }

    private static Encoding MakeStrict(string name) =>
        Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    private static int CountCueKeywords(string s)
    {
        var upper = s.ToUpperInvariant();
        return CueKeywords.Sum(k =>
        {
            var count = 0;
            var idx = upper.IndexOf(k, StringComparison.Ordinal);
            while (idx >= 0)
            {
                count++;
                idx = upper.IndexOf(k, idx + k.Length, StringComparison.Ordinal);
            }
            return count;
        });
    }
}
