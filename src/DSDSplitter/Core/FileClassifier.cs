namespace DSDSplitter.Core;

/// <summary>
/// 把拖入的文件/文件夹归类成待处理任务（见开发文档 3 ①）。
/// 去重：CUE 与其音频同时拖入时只产生一个任务。
/// </summary>
public static class FileClassifier
{
    private static readonly string[] AudioExtensions =
    {
        ".wav", ".flac", ".ape", ".diff", ".dsf", ".dff", ".wv", ".tta",
    };
    private const string CueExtension = ".cue";

    public static IReadOnlyList<SplitJob> Classify(IEnumerable<string> paths)
    {
        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
                files.AddRange(EnumerateFilesSafe(p));
            else if (File.Exists(p))
                files.Add(p);
        }

        var jobs = new List<SplitJob>();
        var seenCue = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenAudio = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in files)
        {
            var ext = Path.GetExtension(f);
            var dir = Path.GetDirectoryName(f) ?? ".";

            if (string.Equals(ext, CueExtension, StringComparison.OrdinalIgnoreCase))
            {
                if (!seenCue.Add(f)) continue;
                var audio = FindSiblingAudio(f);
                jobs.Add(new SplitJob(SplitKind.CueSplit, f, f, audio, dir));
                if (audio is not null) seenAudio.Add(audio); // 避免音频再单独入队
            }
            else if (AudioExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                if (seenAudio.Contains(f)) continue;

                var cue = FindSiblingCue(f);
                if (cue is not null)
                {
                    if (!seenCue.Add(cue)) continue;
                    jobs.Add(new SplitJob(SplitKind.CueSplit, cue, cue, f, dir));
                }
                else
                {
                    seenAudio.Add(f);
                    jobs.Add(new SplitJob(SplitKind.WholeConvert, f, null, f, dir));
                }
            }
            // 其余类型忽略
        }

        return jobs;
    }

    private static string? FindSiblingAudio(string cuePath)
    {
        var dir = Path.GetDirectoryName(cuePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(cuePath);
        foreach (var ext in AudioExtensions)
        {
            var cand = Path.Combine(dir, baseName + ext);
            if (File.Exists(cand)) return Path.GetFullPath(cand);
        }
        return null; // 找不到时交给 CueParser 做模糊匹配兜底
    }

    private static string? FindSiblingCue(string audioPath)
    {
        var dir = Path.GetDirectoryName(audioPath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(audioPath);
        var cand = Path.Combine(dir, baseName + CueExtension);
        return File.Exists(cand) ? Path.GetFullPath(cand) : null;
    }

    private static IEnumerable<string> EnumerateFilesSafe(string dir)
    {
        var result = new List<string>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir))
                result.Add(f);
            foreach (var sub in Directory.EnumerateDirectories(dir))
                result.AddRange(EnumerateFilesSafe(sub));
        }
        catch
        {
            // 无权限的子目录跳过，不中断整体
        }
        return result;
    }
}
