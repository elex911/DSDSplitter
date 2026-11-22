using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;
using DSDSplitter.Core;
using DSDSplitter.Models;
using DSDSplitter.Services;

namespace DSDSplitter.Forms;

/// <summary>
/// 主窗口（见开发文档第 1 节）：单窗口、拖放即处理、只读日志、进度条、右下角设置。
/// 实现 <see cref="ILogSink"/> 把日志渲染成彩色文本；跨线程调用自动 BeginInvoke。
/// </summary>
public partial class MainForm : Form, ILogSink
{
    private readonly SettingsService _settingsService = new();
    private Settings _settings;
    private Processor? _processor;
    private string _ffmpeg = string.Empty;
    private string _ffprobe = string.Empty;

    public MainForm()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        LocateTools();

        Log(new LogEntry(LogLevel.Info, "就绪。把 .cue 或音频文件拖到上方区域即可开始。"));
        if (string.IsNullOrEmpty(_ffmpeg) || string.IsNullOrEmpty(_ffprobe))
            Log(new LogEntry(LogLevel.Warning,
                "未找到 ffmpeg.exe / ffprobe.exe，请将它们放在 exe 同目录。"));
    }

    private void LocateTools()
    {
        _ffmpeg = FfmpegLocator.FindFfmpeg() ?? string.Empty;
        _ffprobe = FfmpegLocator.FindFfprobe() ?? string.Empty;
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private async void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (_processor?.IsRunning == true)
        {
            Log(new LogEntry(LogLevel.Warning, "正在处理中，请等待当前任务完成。"));
            return;
        }
        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0) return;

        await StartProcessingAsync(files);
    }

    private async Task StartProcessingAsync(string[] files)
    {
        if (string.IsNullOrEmpty(_ffmpeg) || string.IsNullOrEmpty(_ffprobe))
            LocateTools();

        // Progress<T> 在 UI 线程创建 → 捕获 SynchronizationContext → 回调自动回到 UI 线程
        var progress = new Progress<ProgressState>(UpdateProgress);
        _processor = new Processor(_settings, this, progress, _ffmpeg, _ffprobe);
        await _processor.RunAsync(files);

        if (!_processor.Canceled)
            PlayFinishFeedback();
    }

    private void UpdateProgress(ProgressState s)
    {
        var pct = (int)Math.Round(s.OverallFraction * 100);
        _progressBar.Value = Math.Clamp(pct, 0, 100);

        if (s.TaskTotal > 0)
        {
            var eta = s.Remaining.HasValue
                ? $" 剩余 {(int)s.Remaining.Value.TotalMinutes:00}:{s.Remaining.Value.Seconds:00}"
                : string.Empty;
            _lblStatus.Text = $"{pct}%{eta} · 轨 {s.TrackIndex}/{s.TrackTotal}";
            Text = $"DSD Splitter ({pct}%) - {s.StatusText}";
        }
        else
        {
            _lblStatus.Text = "空闲";
            Text = "DSD Splitter";
        }
    }

    private void PlayFinishFeedback()
    {
        if (_settings.PlayFinishSound)
        {
            try { SystemSounds.Asterisk.Play(); } catch { }
        }
        try
        {
            _trayIcon.Visible = true;
            _trayIcon.ShowBalloonTip(3000, "DSD Splitter", "处理完成", ToolTipIcon.Info);
            _trayTimer.Start();
        }
        catch { /* 托盘不可用时忽略 */ }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            _processor?.Cancel();
            e.Handled = true;
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _processor?.Cancel();
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _settings = dlg.Result;
            _settingsService.TrySave(_settings);
        }
    }

    // ---- ILogSink：彩色日志，跨线程安全 ----
    public void Log(LogEntry entry)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => Log(entry)));
            return;
        }

        var color = entry.Level switch
        {
            LogLevel.Error or LogLevel.Warning => Color.Firebrick,
            LogLevel.Skip => Color.RoyalBlue,
            LogLevel.Success => Color.SeaGreen,
            LogLevel.Fix => Color.Gray,
            _ => Color.Black,
        };
        var prefix = entry.Level switch
        {
            LogLevel.Fix => "[修复] ",
            LogLevel.Warning => "[警告] ",
            LogLevel.Error => "[错误] ",
            LogLevel.Skip => "[跳过] ",
            LogLevel.Success => "[完成] ",
            _ => string.Empty,
        };

        _rtbLog.SelectionStart = _rtbLog.TextLength;
        _rtbLog.SelectionLength = 0;
        _rtbLog.SelectionColor = color;
        _rtbLog.AppendText(prefix + entry.Message + Environment.NewLine);
        _rtbLog.ScrollToCaret();

        if (_rtbLog.TextLength > 200_000)
            _rtbLog.Clear();
    }
}
