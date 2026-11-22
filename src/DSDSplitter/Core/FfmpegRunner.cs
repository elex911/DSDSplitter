using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DSDSplitter.Models;

namespace DSDSplitter.Core;

/// <summary>单轨切分请求。</summary>
public sealed record SplitTrackRequest(
    string AudioPath,
    string OutputPath,
    double StartSec,
    double DurationSec,
    int TrackNo,
    int Total,
    string? Title,
    string? Artist,
    string? Album,
    bool IsDsd);

/// <summary>
/// ffmpeg 调用封装（见开发文档 6.3 / 6.4）。
/// 使用 ProcessStartInfo.ArgumentList 安全传参（无引号截断/空格转义风险）；
/// 进度从 stderr 按 \r/\n 切分解析；支持取消（kill 进程树）。
/// </summary>
public static class FfmpegRunner
{
    private static readonly Regex TimeRegex =
        new(@"time=(\d+):(\d+):(\d+)\.(\d+)", RegexOptions.Compiled);

    /// <summary>切一轨。</summary>
    public static async Task SplitTrackAsync(
        string ffmpegPath, SplitTrackRequest req, Settings settings,
        IProgress<double>? progress, CancellationToken ct)
    {
        var (audioArgs, _) = BuildAudioArgs(settings, req.IsDsd);
        var args = new List<string>
        {
            "-y",
            "-ss", req.StartSec.ToString("F3", CultureInfo.InvariantCulture),
            "-t", req.DurationSec.ToString("F3", CultureInfo.InvariantCulture),
            "-i", req.AudioPath,
        };
        args.AddRange(audioArgs);
        args.Add("-map_metadata");
        args.Add("0");
        args.Add("-metadata");
        args.Add($"track={req.TrackNo}/{req.Total}");
        if (!string.IsNullOrWhiteSpace(req.Title))
        {
            args.Add("-metadata");
            args.Add($"title={SanitizeMetadataValue(req.Title)}");
        }
        if (!string.IsNullOrWhiteSpace(req.Artist))
        {
            args.Add("-metadata");
            args.Add($"artist={SanitizeMetadataValue(req.Artist)}");
        }
        if (!string.IsNullOrWhiteSpace(req.Album))
        {
            args.Add("-metadata");
            args.Add($"album={SanitizeMetadataValue(req.Album)}");
        }
        args.Add(req.OutputPath);

        var (code, lastError) = await RunAsync(ffmpegPath, args, req.DurationSec, progress, ct);
        if (code != 0)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(lastError)
                    ? $"ffmpeg 退出码 {code}"
                    : $"ffmpeg 退出码 {code}: {lastError}");
    }

    /// <summary>整轨转换（无 CUE 时）。</summary>
    public static async Task ConvertWholeAsync(
        string ffmpegPath, string audioPath, string outputPath,
        bool isDsd, double totalSec, Settings settings,
        IProgress<double>? progress, CancellationToken ct)
    {
        var (audioArgs, _) = BuildAudioArgs(settings, isDsd);
        var args = new List<string> { "-y", "-i", audioPath };
        args.AddRange(audioArgs);
        args.Add("-map_metadata");
        args.Add("0");
        args.Add(outputPath);

        var (code, lastError) = await RunAsync(ffmpegPath, args, totalSec, progress, ct);
        if (code != 0)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(lastError)
                    ? $"ffmpeg 退出码 {code}"
                    : $"ffmpeg 退出码 {code}: {lastError}");
    }

    private static async Task<(int ExitCode, string? LastError)> RunAsync(
        string ffmpegPath, IEnumerable<string> args, double totalSec,
        IProgress<double>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = false, // 不重定向 stdout，避免缓冲区写满导致死锁
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!proc.Start())
            throw new InvalidOperationException("无法启动 ffmpeg");

        var readTask = ReadProgressAsync(proc, totalSec, progress, ct);

        try
        {
            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            KillTree(proc);
            throw;
        }

        var lastError = await readTask;
        return (proc.ExitCode, lastError);
    }

    private static async Task<string?> ReadProgressAsync(
        Process proc, double totalSec, IProgress<double>? progress, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new char[1024];
        string? lastNonProgressLine = null;
        using var reader = proc.StandardError;

        int n;
        while ((n = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            sb.Append(buffer, 0, n);
            var text = sb.ToString();
            var idx = text.LastIndexOfAny(new[] { '\r', '\n' });
            if (idx < 0) continue;

            var ready = text[..idx];
            sb.Clear();
            sb.Append(text[(idx + 1)..]);

            foreach (var rawLine in ready.Split('\r', '\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                var m = TimeRegex.Match(line);
                if (m.Success)
                {
                    var sec = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 3600
                            + int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * 60
                            + int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)
                            + int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) / 100.0;
                    if (totalSec > 0)
                        progress?.Report(Math.Min(1.0, sec / totalSec));
                }
                else
                {
                    lastNonProgressLine = line;
                }
            }
        }

        var tail = sb.ToString().Trim();
        if (tail.Length > 0 && !TimeRegex.IsMatch(tail))
            lastNonProgressLine = tail;

        return lastNonProgressLine;
    }

    private static void KillTree(Process proc)
    {
        try { proc.Kill(entireProcessTree: true); }
        catch { /* 进程可能已退出 */ }
    }

    /// <summary>构造输出音频参数，返回 (ffmpeg 参数列表, 输出扩展名)。</summary>
    private static (List<string> Args, string Ext) BuildAudioArgs(Settings s, bool isDsd)
    {
        var args = new List<string>();
        string ext;

        if (isDsd)
        {
            var (sr, bits) = DsdParams(s);
            args.Add("-af");
            args.Add("aresample=resampler=soxr:precision=32:cutoff=0.84:dither_method=triangular_hp");
            args.Add("-ac");
            args.Add("2");

            switch (s.OutputFormat)
            {
                case OutputFormat.Flac:
                    args.Add("-ar");
                    args.Add(sr.ToString(CultureInfo.InvariantCulture));
                    args.Add("-c:a");
                    args.Add("flac");
                    if (bits >= 24)
                    {
                        // ffmpeg 的 flac 编码器不存在 s24 采样格式，24bit 必须使用 s32 + bits_per_raw_sample 24
                        args.Add("-sample_fmt");
                        args.Add("s32");
                        args.Add("-bits_per_raw_sample");
                        args.Add("24");
                    }
                    else
                    {
                        args.Add("-sample_fmt");
                        args.Add("s16");
                    }
                    ext = "flac";
                    break;

                case OutputFormat.Wav:
                    args.Add("-ar");
                    args.Add(sr.ToString(CultureInfo.InvariantCulture));
                    args.Add("-c:a");
                    args.Add(bits >= 32 ? "pcm_s32le" : bits >= 24 ? "pcm_s24le" : "pcm_s16le");
                    ext = "wav";
                    break;

                case OutputFormat.Mp3:
                    args.Add("-ar");
                    args.Add("44100");
                    args.Add("-c:a");
                    args.Add("libmp3lame");
                    args.Add("-b:a");
                    args.Add("320k");
                    ext = "mp3";
                    break;

                default:
                    args.Add("-ar");
                    args.Add(sr.ToString(CultureInfo.InvariantCulture));
                    args.Add("-c:a");
                    args.Add("flac");
                    args.Add("-sample_fmt");
                    args.Add("s16");
                    ext = "flac";
                    break;
            }
        }
        else
        {
            switch (s.OutputFormat)
            {
                case OutputFormat.Flac:
                    args.Add("-c:a");
                    args.Add("flac");
                    ext = "flac";
                    break;
                case OutputFormat.Wav:
                    args.Add("-c:a");
                    args.Add("pcm_s16le");
                    ext = "wav";
                    break;
                case OutputFormat.Mp3:
                    args.Add("-c:a");
                    args.Add("libmp3lame");
                    args.Add("-b:a");
                    args.Add("320k");
                    ext = "mp3";
                    break;
                default:
                    args.Add("-c:a");
                    args.Add("flac");
                    ext = "flac";
                    break;
            }
        }

        return (args, ext);
    }

    private static (int SampleRate, int Bits) DsdParams(Settings s) => s.DsdTarget switch
    {
        DsdTarget.Cd44k16 => (44100, 16),
        DsdTarget.Hq88k24 => (88200, 24),
        _ => (Math.Clamp(s.CustomSampleRate, 8000, 384000), s.CustomBitDepth >= 24 ? 24 : 16),
    };

    /// <summary>输出扩展名（用于拼文件名）。</summary>
    public static string GetOutputExtension(Settings s)
    {
        var (_, ext) = BuildAudioArgs(s, false);
        return ext;
    }

    private static string SanitizeMetadataValue(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
