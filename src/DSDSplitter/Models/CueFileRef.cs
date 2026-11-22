namespace DSDSplitter.Models;

/// <summary>CUE 中的一个 FILE 条目（一个音频文件及其音轨）。</summary>
public sealed class CueFileRef
{
    /// <summary>CUE 中 FILE 行声明的文件名（纠错后）。</summary>
    public string DeclaredName { get; set; } = string.Empty;

    /// <summary>FILE 行声明的类型（WAVE / MP3 / AIFF / BINARY 等），保留原值不擅自改写。</summary>
    public string DeclaredType { get; set; } = string.Empty;

    /// <summary>纠错后实际定位到的音频文件绝对路径；找不到则为 null。</summary>
    public string? ResolvedAudioPath { get; set; }

    /// <summary>该音频文件的总时长（秒），由 ffprobe 探测后填充。</summary>
    public double? TotalDuration { get; set; }

    /// <summary>该 FILE 下的音轨。</summary>
    public List<CueTrack> Tracks { get; } = new();
}
