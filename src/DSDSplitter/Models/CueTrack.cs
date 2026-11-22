namespace DSDSplitter.Models;

/// <summary>CUE 中的一条音轨（TRACK）。</summary>
public sealed class CueTrack
{
    public int Number { get; set; }

    public string? Title { get; set; }

    public string? Performer { get; set; }

    /// <summary>是否为音频轨（MODE1/2 等数据轨不切）。</summary>
    public bool IsAudio { get; set; } = true;

    /// <summary>INDEX 00（ pregap 起点），秒；可为空。</summary>
    public double? Index00 { get; set; }

    /// <summary>INDEX 01（音频正式起点 = 切分点），秒。</summary>
    public double? Index01 { get; set; }

    /// <summary>本轨结束时间（秒），由下一轨起点或音频总时长填充。</summary>
    public double? EndTime { get; set; }

    public double Duration => (Index01.HasValue && EndTime.HasValue)
        ? Math.Max(0, EndTime.Value - Index01.Value)
        : 0;

    /// <summary>用于显示/文件名的安全标题：为空时回退 "Track NN"。</summary>
    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Title) ? $"Track {Number:00}" : Title!.Trim();
}
