using System.Diagnostics;
using System.Globalization;

namespace DSDSplitter.Core;

/// <summary>用 ffprobe 探测音频总时长（秒）。</summary>
public static class FfprobeReader
{
    public static double? GetDuration(string ffprobePath, string audioPath)
    {
        if (string.IsNullOrEmpty(ffprobePath)) return null;
        try
        {
            var psi = new ProcessStartInfo(ffprobePath)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                // 不重定向 stderr，避免缓冲区填满导致死锁
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("format=duration");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
            psi.ArgumentList.Add(audioPath);

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0) return null;

            if (double.TryParse(output.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var d) && d > 0)
                return d;
            return null;
        }
        catch
        {
            return null;
        }
    }
}
