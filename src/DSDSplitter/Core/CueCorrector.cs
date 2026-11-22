using System.Text;
using DSDSplitter.Models;
using DSDSplitter.Services;

namespace DSDSplitter.Core;

/// <summary>纠错结果：归一化后的 CUE 文本 + 探测到的源编码名。</summary>
public sealed record CueCorrectionResult(string Text, string DetectedEncodingWebName);

/// <summary>
/// CUE 静默纠错 —— 文本归一化阶段（见开发文档 4.1，顺序 1/2/3/4/5/12）。
/// 输出规范化文本，交给 <see cref="CueParser"/> 做结构纠错（6/7/8/9/10/11）。
/// </summary>
public static class CueCorrector
{
    private static readonly HashSet<string> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        "CATALOG", "FILE", "FLAGS", "INDEX", "ISRC", "PERFORMER", "POSTGAP",
        "PREGAP", "REM", "TITLE", "TRACK",
    };

    // 需要自动补引号的值命令（值通常是自由文本）
    private static readonly HashSet<string> QuoteValueCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "TITLE", "PERFORMER",
    };

    public static CueCorrectionResult Correct(byte[] raw, ILogSink log)
    {
        // 1) 编码探测 + 解码
        var enc = EncodingDetector.Detect(raw);
        var text = enc.GetString(raw);
        if (!string.Equals(enc.WebName, "utf-8", StringComparison.OrdinalIgnoreCase))
            log.Log(LogLevel.Fix, $"编码: {enc.WebName} → UTF-8");

        // 2) 去 BOM 字符（编码探测已处理字节 BOM，这里兜底去掉 U+FEFF）
        text = text.TrimStart('\uFEFF');

        // 3) 统一换行（\r\n / \r → \n）
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        // 12) 剥离控制字符：保留 \t(0x09) 与 \n(0x0A)，丢弃 0x00-0x08、0x0B、0x0C、0x0E-0x1F 及 0x7F
        //     【修正 v2.0 会误删换行的 bug】
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\t' || ch == '\n' || (ch >= 0x20 && ch != 0x7F))
                sb.Append(ch);
        }
        text = sb.ToString();

        // 4)+5) 逐行：关键字大写 + 引号补全
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = NormalizeLine(lines[i], i + 1, log);

        return new CueCorrectionResult(string.Join("\n", lines), enc.WebName);
    }

    private static string NormalizeLine(string line, int lineNo, ILogSink log)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0) return line;

        var firstSep = trimmed.IndexOfAny(new[] { ' ', '\t' });
        var token = firstSep < 0 ? trimmed : trimmed[..firstSep];
        var rest = firstSep < 0 ? string.Empty : trimmed[firstSep..].Trim();

        if (!Commands.Contains(token))
            return line; // 非命令行原样保留

        var upper = token.ToUpperInvariant();

        // 5) 关键字小写 → 大写
        if (!string.Equals(token, upper, StringComparison.Ordinal))
            log.Log(LogLevel.Fix, $"关键字大写: 第 {lineNo} 行 {token} → {upper}");

        // 4) 引号缺失 / 不闭合修复（TITLE / PERFORMER）
        if (QuoteValueCommands.Contains(upper) && rest.Length > 0)
        {
            var startsWithQuote = rest[0] == '"';
            var endsWithQuote = rest[^1] == '"';
            var quoteCount = rest.Count(c => c == '"');

            if (quoteCount == 0)
            {
                rest = "\"" + rest + "\"";
                log.Log(LogLevel.Fix, $"引号补全: 第 {lineNo} 行 {upper}");
            }
            else if (startsWithQuote && !endsWithQuote)
            {
                rest = rest + "\"";
                log.Log(LogLevel.Fix, $"引号补全: 第 {lineNo} 行 {upper}");
            }
            else if (!startsWithQuote && endsWithQuote)
            {
                rest = "\"" + rest;
                log.Log(LogLevel.Fix, $"引号补全: 第 {lineNo} 行 {upper}");
            }
            else if (quoteCount % 2 != 0)
            {
                rest = "\"" + rest.Trim('"') + "\"";
                log.Log(LogLevel.Fix, $"引号补全: 第 {lineNo} 行 {upper}");
            }
        }
        // FILE 行文件名未加引号但含空格（例如: FILE My Album.ape WAVE）
        else if (upper == "FILE" && rest.Length > 0 && rest[0] != '"')
        {
            var lastSpace = rest.LastIndexOfAny(new[] { ' ', '\t' });
            if (lastSpace > 0)
            {
                var fileNamePart = rest[..lastSpace].Trim().Trim('"');
                var fileTypePart = rest[(lastSpace + 1)..].Trim();
                if (fileNamePart.Length > 0 && fileTypePart.Length > 0)
                {
                    rest = $"\"{fileNamePart}\" {fileTypePart}";
                    log.Log(LogLevel.Fix, $"引号补全: 第 {lineNo} 行 FILE");
                }
            }
        }

        var leading = line[..(line.Length - trimmed.Length)];
        return leading + upper + (rest.Length > 0 ? " " + rest : string.Empty);
    }
}
