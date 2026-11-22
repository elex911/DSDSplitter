using System;
using System.IO;
using System.Windows.Forms;
using DSDSplitter.Models;

namespace DSDSplitter.Forms;

/// <summary>
/// 设置对话框（见开发文档第 5 节）。
/// 纯控件 ↔ Settings 映射，不触碰业务逻辑；确定后通过 <see cref="Result"/> 回传。
/// </summary>
public partial class SettingsDialog : Form
{
    // ---- 下拉项文本（与枚举一一对应，集中维护避免散落硬编码）----
    private const string FmtFlac = "FLAC";
    private const string FmtWav = "WAV";
    private const string FmtMp3 = "MP3 320k";

    private const string OutSource = "源目录\\Split";
    private const string OutCustom = "自选目录";

    private const string DsdCd = "44.1kHz / 16bit (CD标准)";
    private const string DsdHq = "88.2kHz / 24bit";
    private const string DsdCustom = "自定义";

    private const string ExSkip = "跳过";
    private const string ExOver = "覆盖";

    /// <summary>用户点“确定”后回传的新设置。</summary>
    public Settings Result { get; private set; } = new();

    private readonly Settings _current;
    private string _browsedDir;

    public SettingsDialog(Settings current)
    {
        _current = current;
        _browsedDir = current.CustomOutputDir ?? string.Empty;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        // 输出格式
        _cboFormat.Items.AddRange(new object[] { FmtFlac, FmtWav, FmtMp3 });
        _cboFormat.SelectedItem = _current.OutputFormat switch
        {
            OutputFormat.Wav => FmtWav,
            OutputFormat.Mp3 => FmtMp3,
            _ => FmtFlac,
        };

        // 输出目录
        _cboOutputDir.Items.AddRange(new object[] { OutSource, OutCustom });
        _cboOutputDir.SelectedItem = _current.OutputToSourceSplit ? OutSource : OutCustom;

        // 并发数（钳制到控件范围，避免旧配置越界抛 ArgumentOutOfRangeException）
        _nudConcurrency.Value = Clamp(_current.Concurrency,
            (int)_nudConcurrency.Minimum, (int)_nudConcurrency.Maximum);

        // DSD 转码目标
        _cboDsd.Items.AddRange(new object[] { DsdCd, DsdHq, DsdCustom });
        _cboDsd.SelectedItem = _current.DsdTarget switch
        {
            DsdTarget.Hq88k24 => DsdHq,
            DsdTarget.Custom => DsdCustom,
            _ => DsdCd,
        };
        _nudSr.Value = Clamp(_current.CustomSampleRate,
            (int)_nudSr.Minimum, (int)_nudSr.Maximum);
        _cboBits.Items.AddRange(new object[] { "16", "24" });
        _cboBits.SelectedItem = _current.CustomBitDepth >= 24 ? "24" : "16";
        UpdateCustomEnabled();

        // 已存在文件
        _cboExisting.Items.AddRange(new object[] { ExSkip, ExOver });
        _cboExisting.SelectedItem = _current.ExistingFileAction == ExistingFileAction.Overwrite
            ? ExOver
            : ExSkip;

        // 复选项
        _chkSound.Checked = _current.PlayFinishSound;
        _chkKeepCue.Checked = _current.KeepOriginalCue;
    }

    /// <summary>仅当 DSD 目标选“自定义”时，显示采样率/位深输入控件。</summary>
    private void UpdateCustomEnabled()
    {
        var custom = string.Equals(_cboDsd.SelectedItem as string, DsdCustom,
            StringComparison.Ordinal);
        _nudSr.Visible = custom;
        _lblHz.Visible = custom;
        _cboBits.Visible = custom;
        _lblBit.Visible = custom;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "选择分轨输出目录",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(_browsedDir) ? _browsedDir : string.Empty,
        };
        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            _browsedDir = fbd.SelectedPath;
            _cboOutputDir.SelectedItem = OutCustom;
        }
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        var outputToSource = string.Equals(_cboOutputDir.SelectedItem as string, OutSource,
            StringComparison.Ordinal);

        Result = new Settings
        {
            OutputFormat = (_cboFormat.SelectedItem as string) switch
            {
                FmtWav => OutputFormat.Wav,
                FmtMp3 => OutputFormat.Mp3,
                _ => OutputFormat.Flac,
            },
            OutputToSourceSplit = outputToSource,
            CustomOutputDir = outputToSource ? null : _browsedDir,
            Concurrency = (int)_nudConcurrency.Value,
            DsdTarget = (_cboDsd.SelectedItem as string) switch
            {
                DsdHq => DsdTarget.Hq88k24,
                DsdCustom => DsdTarget.Custom,
                _ => DsdTarget.Cd44k16,
            },
            CustomSampleRate = (int)_nudSr.Value,
            CustomBitDepth = int.TryParse(_cboBits.SelectedItem as string, out var b) ? b : 16,
            PlayFinishSound = _chkSound.Checked,
            KeepOriginalCue = _chkKeepCue.Checked,
            ExistingFileAction = string.Equals(_cboExisting.SelectedItem as string, ExOver,
                StringComparison.Ordinal)
                ? ExistingFileAction.Overwrite
                : ExistingFileAction.Skip,
        };

        DialogResult = DialogResult.OK;
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), max);
}
