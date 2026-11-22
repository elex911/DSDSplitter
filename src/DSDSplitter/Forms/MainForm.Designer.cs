using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DSDSplitter.Forms;

partial class MainForm
{
    private IContainer? components = null;

    private TableLayoutPanel _tlpMain = null!;
    private Panel _pnlDrop = null!;
    private Label _lblDropHint = null!;
    private RichTextBox _rtbLog = null!;

    private TableLayoutPanel _tlpBottom = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblStatus = null!;
    private Button _btnSettings = null!;

    private NotifyIcon _trayIcon = null!;
    private System.Windows.Forms.Timer _trayTimer = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new Container();

        // ---- 窗体 ----
        SuspendLayout();
        Text = "DSD Splitter";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(680, 540);
        MinimumSize = new Size(500, 360);
        AllowDrop = true;
        KeyPreview = true;
        Font = new Font("Microsoft YaHei", 9F);

        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;
        KeyDown += MainForm_KeyDown;
        FormClosing += MainForm_FormClosing;

        // ---- 顶部拖放区 ----
        _pnlDrop = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(238, 243, 250),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(8, 8, 8, 4),
        };
        _lblDropHint = new Label
        {
            Dock = DockStyle.Fill,
            Text = "把 .cue 或音频文件拖到这里",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft YaHei", 14F),
            ForeColor = Color.FromArgb(60, 90, 150),
        };
        _pnlDrop.Controls.Add(_lblDropHint);

        // ---- 中部日志区（只读）----
        _rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowDrop = false,
            BackColor = Color.White,
            Font = new Font("Consolas", 9F),
            BorderStyle = BorderStyle.None,
            Margin = new Padding(8, 4, 8, 4),
        };

        // ---- 底部：进度 + 状态 + 设置 ----
        _tlpBottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(8, 4, 8, 8),
        };
        _tlpBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _tlpBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
        _tlpBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 18,
            Style = ProgressBarStyle.Continuous,
            Margin = new Padding(0, 6, 6, 6),
        };
        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = "空闲",
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(6, 0, 6, 0),
        };
        _btnSettings = new Button
        {
            Text = "⚙ 设置",
            Dock = DockStyle.Fill,
            Height = 28,
            Margin = new Padding(0),
        };
        _btnSettings.Click += BtnSettings_Click;

        _tlpBottom.Controls.Add(_progressBar, 0, 0);
        _tlpBottom.Controls.Add(_lblStatus, 1, 0);
        _tlpBottom.Controls.Add(_btnSettings, 2, 0);

        // ---- 主布局：3 行 ----
        _tlpMain = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        _tlpMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
        _tlpMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _tlpMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        _tlpMain.Controls.Add(_pnlDrop, 0, 0);
        _tlpMain.Controls.Add(_rtbLog, 0, 1);
        _tlpMain.Controls.Add(_tlpBottom, 0, 2);

        // ---- 托盘提示（完成时闪一下）----
        _trayIcon = new NotifyIcon(components)
        {
            Icon = SystemIcons.Information,
            Visible = false,
            Text = "DSD Splitter",
        };
        _trayTimer = new System.Windows.Forms.Timer(components) { Interval = 4000 };
        _trayTimer.Tick += (_, _) =>
        {
            _trayTimer.Stop();
            _trayIcon.Visible = false;
        };

        Controls.Add(_tlpMain);
        ResumeLayout(false);
    }
}
