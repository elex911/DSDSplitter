namespace DSDSplitter.Core;

/// <summary>
/// 定位随包分发的 ffmpeg.exe / ffprobe.exe。
/// 优先 exe 同目录（AppContext.BaseDirectory），其次系统 PATH。
/// 见开发文档 6.3 / 8.3：单文件发布下不能用 Assembly.Location。
/// </summary>
public static class FfmpegLocator
{
    public static string? FindFfmpeg() => FindExecutable("ffmpeg.exe");

    public static string? FindFfprobe() => FindExecutable("ffprobe.exe");

    private static string? FindExecutable(string exe)
    {
        var local = Path.Combine(AppContext.BaseDirectory, exe);
        if (File.Exists(local)) return local;

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
