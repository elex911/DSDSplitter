using System.Diagnostics;
using System.Text;
using DSDSplitter.Models;
using DSDSplitter.Services;

namespace DSDSplitter.Core;

/// <summary>处理进度快照，由 Processor 上报给 UI。</summary>
public sealed record ProgressState(
    int TaskIndex,          // 当前任务序号（1-based）
    int TaskTotal,          // 任务总数
    int TrackIndex,         // 当前轨序号（1-based）
    int TrackTotal,         // 当前任务轨数
    double TrackFraction,   // 当前任务内进度 0..1
    double OverallFraction, // 总体进度 0..1
    string StatusText,
    TimeSpan? Remaining = null);

/// <summary>
/// 主流程编排（见开发文档第 3 节）：归类 → 纠错 → 解析 → 切分 → 输出。
/// 支持任务队列、轨间并发、取消、剩余时间预估。UI 只依赖 <see cref="ILogSink"/> 与
/// <see cref="IProgress{T}"/> 两个抽象，不直接碰 ffmpeg。
/// </summary>
public sealed class Processor
{
    private readonly Settings _settings;
    private readonly ILogSink _log;
    private readonly IProgress<ProgressState> _progress;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    private CancellationTokenSource? _cts;
    private Stopwatch? _sw;

    public Processor(Settings settings, ILogSink log, IProgress<ProgressState> progress,
        string ffmpegPath, string ffprobePath)
    {
        _settings = settings;
        _log = log;
        _progress = progress;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath;
    }

    public bool IsRunning => _cts is not null;

    /// <summary>最近一次运行是否被用户取消。</summary>
    public bool Canceled { get; private set; }

    public void Cancel() => _cts?.Cancel();

    public async Task RunAsync(IEnumerable<string> paths)
    {
        if (_cts is not null) return; // 已在运行

        Canceled = false;
        _cts = new CancellationTokenSource();
        _sw = Stopwatch.StartNew();
        var ct = _cts.Token;
        try
        {
            await RunCoreAsync(paths, ct);
        }
        catch (OperationCanceledException)
        {
            Canceled = true;
            _log.Log(LogLevel.Warning, "已取消");
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"处理失败: {ex.Message}");
        }
        finally
        {
            _sw?.Stop();
            _sw = null;
            _cts.Dispose();
            _cts = null;
            _progress.Report(new ProgressState(0, 0, 0, 0, 0, 0, "空闲", null));
        }
    }

    private async Task RunCoreAsync(IEnumerable<string> paths, CancellationToken ct)
    {
        var jobs = FileClassifier.Classify(paths);
        if (jobs.Count == 0)
        {
            _log.Log(LogLevel.Info, "未找到可处理的 CUE 或音频文件");
            return;
        }

        if (string.IsNullOrEmpty(_ffmpegPath))
        {
            _log.Log(LogLevel.Error, "未找到 ffmpeg.exe，请将其放在 exe 同目录");
            return;
        }

        _log.Log(LogLevel.Info, $"共 {jobs.Count} 个任务进入队列");

        for (var i = 0; i < jobs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var job = jobs[i];
            try
            {
                if (job.Kind == SplitKind.CueSplit)
                    await ProcessCueAsync(job, i, jobs.Count, ct);
                else
                    await ProcessWholeAsync(job, i, jobs.Count, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Error,
                    $"任务失败 ({Path.GetFileName(job.SourcePath)}): {ex.Message}");
            }
        }

        _log.Log(LogLevel.Success, "全部完成");
    }

    // ---------- CUE 分轨 ----------
    private async Task ProcessCueAsync(SplitJob job, int taskIndex, int taskTotal, CancellationToken ct)
    {
        var cuePath = job.CuePath!;
        var cueDir = job.BaseDir;

        _log.Log(LogLevel.Info, $"正在处理: {Path.GetFileName(cuePath)}");

        byte[] raw;
        try { raw = File.ReadAllBytes(cuePath); }
        catch (Exception ex) { _log.Log(LogLevel.Error, $"无法读取 CUE: {ex.Message}"); return; }

        var corrected = CueCorrector.Correct(raw, _log);
        var sheet = CueParser.Parse(corrected.Text, cueDir, _log);

        var totalTracks = sheet.AllTracks.Count();
        if (totalTracks == 0)
        {
            _log.Log(LogLevel.Error, "未找到任何音轨定义");
            return;
        }

        var outputDir = ResolveOutputDir(job.BaseDir);
        try { Directory.CreateDirectory(outputDir); }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"无法写入输出目录，请检查权限或磁盘空间: {ex.Message}");
            return;
        }

        // 可选：把纠错后的 CUE 写到输出目录（不覆盖原文件）
        if (_settings.KeepOriginalCue)
        {
            try
            {
                var fixedPath = Path.Combine(outputDir,
                    Path.GetFileNameWithoutExtension(cuePath) + "_fixed.cue");
                File.WriteAllText(fixedPath,
                    corrected.Text.Replace("\n", "\r\n"), new UTF8Encoding(false));
            }
            catch { /* 非致命，忽略 */ }
        }

        var ext = FfmpegRunner.GetOutputExtension(_settings);
        var tp = new TrackProgress(totalTracks);

        void Report(int trackDisplay)
        {
            var jobFrac = tp.JobFraction(totalTracks);
            var overall = taskTotal == 0 ? 0 : (taskIndex + jobFrac) / taskTotal;
            _progress.Report(new ProgressState(
                taskIndex + 1, taskTotal,
                Math.Clamp(trackDisplay, 1, totalTracks), totalTracks,
                jobFrac, overall, Path.GetFileName(cuePath),
                EstimateRemaining(overall)));
        }
        Report(1);

        using var sem = new SemaphoreSlim(_settings.SafeConcurrency);
        var tasks = new List<Task>();
        var slot = 0;

        foreach (var file in sheet.Files)
        {
            ct.ThrowIfCancellationRequested();

            // 若 CUE 内 FILE 名未能匹配，但单 FILE 任务已由拖入/同目录定位到音频，则兜底使用
            if (file.ResolvedAudioPath is null && sheet.Files.Count == 1 && !string.IsNullOrEmpty(job.AudioPath))
            {
                file.ResolvedAudioPath = job.AudioPath;
                _log.Log(LogLevel.Fix,
                    $"FILE 回退匹配: \"{file.DeclaredName}\" → {Path.GetFileName(job.AudioPath)}");
            }

            if (file.ResolvedAudioPath is null)
            {
                _log.Log(LogLevel.Error,
                    $"未找到音频文件（FILE: {file.DeclaredName}），请手动拖入音频");
                foreach (var _ in file.Tracks) { tp.MarkDone(slot++); }
                Report(slot);
                continue;
            }

            // 开发文档 4.3：多轨时间戳全为 0 → 时间戳无效
            if (file.Tracks.Count > 1 && file.Tracks.All(t => (t.Index01 ?? 0) <= 0))
            {
                _log.Log(LogLevel.Error,
                    $"时间戳无效（所有音轨 INDEX 01 均为 0）: {Path.GetFileName(file.ResolvedAudioPath)}");
                foreach (var _ in file.Tracks) { tp.MarkDone(slot++); }
                Report(slot);
                continue;
            }

            var dur = FfprobeReader.GetDuration(_ffprobePath, file.ResolvedAudioPath);
            if (!dur.HasValue)
            {
                _log.Log(LogLevel.Error,
                    $"无法读取音频时长（文件可能损坏）: {Path.GetFileName(file.ResolvedAudioPath)}");
                foreach (var _ in file.Tracks) { tp.MarkDone(slot++); }
                Report(slot);
                continue;
            }
            file.TotalDuration = dur;
            SplitPlanner.FillEndTimes(file);

            var isDsd = SplitPlanner.IsDsd(file.ResolvedAudioPath);
            var album = sheet.Title;
            var albumArtist = sheet.Performer;

            foreach (var track in file.Tracks)
            {
                var mySlot = slot++;
                var t = track;
                var f = file;
                tasks.Add(ProcessOneTrackAsync(
                    t, f, mySlot, totalTracks, outputDir, ext, isDsd,
                    album, albumArtist, sem, tp, () => Report(mySlot + 1), ct));
            }
        }

        await Task.WhenAll(tasks);
        Report(totalTracks);
    }

    private async Task ProcessOneTrackAsync(
        CueTrack track, CueFileRef file, int slot, int total,
        string outputDir, string ext, bool isDsd,
        string? album, string? albumArtist,
        SemaphoreSlim sem, TrackProgress tp, Action report, CancellationToken ct)
    {
        await sem.WaitAsync(ct);
        try
        {
            if (track.Index01 is null || track.Duration <= 0)
            {
                _log.Log(LogLevel.Warning,
                    $"跳过 TRACK {track.Number:00}：无有效时间戳");
                tp.MarkDone(slot); report();
                return;
            }

            // Windows MAX_PATH (255 字符) 防护：根据输出目录长度动态计算文件名最大长度
            var maxBaseLen = Math.Max(16, 250 - outputDir.Length - 1 - ext.Length - 1);
            var cleanTitle = PathSanitizer.SanitizeFileName(track.Title ?? string.Empty, maxBaseLen);
            if (string.IsNullOrEmpty(cleanTitle))
            {
                cleanTitle = $"Track {track.Number:00}";
                if (!string.IsNullOrWhiteSpace(track.Title))
                    _log.Log(LogLevel.Warning, $"TRACK {track.Number:00} 标题清洗后为空，已回退命名: {cleanTitle}");
            }

            var safeName = PathSanitizer.SanitizeFileName(
                $"{track.Number:00} - {cleanTitle}", maxBaseLen);
            if (string.IsNullOrEmpty(safeName))
                safeName = $"Track {track.Number:00}";
            var outPath = Path.Combine(outputDir, safeName + "." + ext);

            if (!PathSanitizer.IsWithin(outputDir, outPath))
            {
                _log.Log(LogLevel.Error, $"输出路径越界，已拒绝: {safeName}");
                tp.MarkDone(slot); report();
                return;
            }

            if (File.Exists(outPath) &&
                _settings.ExistingFileAction == ExistingFileAction.Skip)
            {
                _log.Log(LogLevel.Skip, $"文件已存在: {Path.GetFileName(outPath)}");
                tp.MarkDone(slot); report();
                return;
            }

            _log.Log(LogLevel.Info,
                $"分轨 {track.Number}/{total}: {Path.GetFileName(outPath)}");

            var prog = new Progress<double>(f =>
            {
                lock (tp.Lock) tp.Slots[slot] = f;
                report();
            });

            var req = new SplitTrackRequest(
                file.ResolvedAudioPath!, outPath,
                track.Index01.Value, track.Duration,
                track.Number, total,
                track.Title, track.Performer ?? albumArtist,
                album, isDsd);

            try
            {
                await FfmpegRunner.SplitTrackAsync(
                    _ffmpegPath, req, _settings, prog, ct);
                _log.Log(LogLevel.Success,
                    $"完成 {track.Number}/{total}: {Path.GetFileName(outPath)}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Error,
                    $"分轨失败 {track.Number}/{total}: {ex.Message}");
            }
            finally
            {
                tp.MarkDone(slot);
                report();
            }
        }
        finally
        {
            sem.Release();
        }
    }

    // ---------- 整轨转换（无 CUE） ----------
    private async Task ProcessWholeAsync(SplitJob job, int taskIndex, int taskTotal, CancellationToken ct)
    {
        var audio = job.AudioPath!;
        _log.Log(LogLevel.Info, $"整轨转换: {Path.GetFileName(audio)}");

        var outputDir = ResolveOutputDir(job.BaseDir);
        try { Directory.CreateDirectory(outputDir); }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"无法写入输出目录，请检查权限或磁盘空间: {ex.Message}");
            return;
        }

        var ext = FfmpegRunner.GetOutputExtension(_settings);
        var maxBaseLen = Math.Max(16, 250 - outputDir.Length - 1 - ext.Length - 1);
        var baseName = Path.GetFileNameWithoutExtension(audio);
        var safeBase = PathSanitizer.SanitizeFileName(baseName, maxBaseLen);
        if (string.IsNullOrEmpty(safeBase)) safeBase = "output";
        var outPath = Path.Combine(outputDir, safeBase + "." + ext);

        if (!PathSanitizer.IsWithin(outputDir, outPath))
        {
            _log.Log(LogLevel.Error, "输出路径越界，已拒绝");
            return;
        }

        if (File.Exists(outPath) && _settings.ExistingFileAction == ExistingFileAction.Skip)
        {
            _log.Log(LogLevel.Skip, $"文件已存在: {Path.GetFileName(outPath)}");
            return;
        }

        var isDsd = SplitPlanner.IsDsd(audio);
        var dur = FfprobeReader.GetDuration(_ffprobePath, audio) ?? 0;
        var tp = new TrackProgress(1);

        var prog = new Progress<double>(f =>
        {
            lock (tp.Lock) tp.Slots[0] = f;
            var jobFrac = tp.JobFraction(1);
            var overall = taskTotal == 0 ? 0 : (taskIndex + jobFrac) / taskTotal;
            _progress.Report(new ProgressState(
                taskIndex + 1, taskTotal, 1, 1, jobFrac, overall,
                Path.GetFileName(audio), EstimateRemaining(overall)));
        });

        try
        {
            await FfmpegRunner.ConvertWholeAsync(
                _ffmpegPath, audio, outPath, isDsd, dur, _settings, prog, ct);
            _log.Log(LogLevel.Success, $"完成: {Path.GetFileName(outPath)}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"转换失败: {ex.Message}");
        }
    }

    // ---------- 辅助 ----------
    private string ResolveOutputDir(string baseDir)
    {
        if (!_settings.OutputToSourceSplit
            && !string.IsNullOrWhiteSpace(_settings.CustomOutputDir))
        {
            return _settings.CustomOutputDir;
        }
        return Path.Combine(baseDir, "Split");
    }

    private TimeSpan? EstimateRemaining(double overallFraction)
    {
        if (_sw is null || overallFraction <= 0.01 || overallFraction >= 1.0)
            return null;
        var elapsedSec = _sw.Elapsed.TotalSeconds;
        if (elapsedSec < 1.0) return null;
        var remainSec = elapsedSec * (1.0 - overallFraction) / overallFraction;
        return TimeSpan.FromSeconds(Math.Clamp(remainSec, 0, 86400));
    }

    /// <summary>线程安全的轨进度聚合器。</summary>
    private sealed class TrackProgress
    {
        public readonly double[] Slots;
        public int Done;
        public readonly object Lock = new();

        public TrackProgress(int total) => Slots = new double[total];

        public void MarkDone(int slot)
        {
            lock (Lock) { Slots[slot] = 0; Done++; }
        }

        public double JobFraction(int total)
        {
            lock (Lock)
            {
                var sum = 0.0;
                for (var i = 0; i < Slots.Length; i++) sum += Slots[i];
                return total <= 0 ? 1 : (Done + sum) / total;
            }
        }
    }
}
