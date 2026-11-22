namespace DSDSplitter.Core;

/// <summary>CUE 时间码 MM:SS:FF 与秒的换算（见开发文档 6.5）。</summary>
public static class CueTime
{
    /// <summary>1 秒 = 75 帧（CD 标准）。</summary>
    private const double FramesPerSecond = 75.0;

    /// <summary>MM / SS / FF 三段 → 秒。</summary>
    public static double ToSeconds(string mm, string ss, string ff)
    {
        _ = int.TryParse(mm, out var m);
        _ = int.TryParse(ss, out var s);
        _ = int.TryParse(ff, out var f);
        if (f > 74) f = 74; // 防御：非法帧值钳制
        if (f < 0) f = 0;
        if (m < 0) m = 0;
        if (s < 0) s = 0;
        return m * 60 + s + f / FramesPerSecond;
    }

    /// <summary>从 "MM:SS:FF" 字符串解析，失败返回 null。</summary>
    public static double? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Trim().Split(':');
        if (parts.Length != 3) return null;
        return ToSeconds(parts[0], parts[1], parts[2]);
    }
}
