using DSDSplitter.Models;

namespace DSDSplitter.Core;

/// <summary>切分点计算与 DSD 判定。</summary>
public static class SplitPlanner
{
    private static readonly string[] DsdExtensions = { ".dsf", ".dff", ".diff" };

    /// <summary>按扩展名判断是否为 DSD 音频（需要转 PCM）。</summary>
    public static bool IsDsd(string path) =>
        DsdExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 填充每轨结束时间：中间轨 = 下一轨 INDEX 01；末轨 = 音频总时长。
    /// 必须在 ffprobe 填好 TotalDuration 之后调用。
    /// </summary>
    public static void FillEndTimes(CueFileRef file)
    {
        var tracks = file.Tracks;
        for (var i = 0; i < tracks.Count; i++)
        {
            if (i < tracks.Count - 1)
                tracks[i].EndTime = tracks[i + 1].Index01;
            else if (file.TotalDuration.HasValue)
                tracks[i].EndTime = file.TotalDuration.Value;
        }
    }
}
