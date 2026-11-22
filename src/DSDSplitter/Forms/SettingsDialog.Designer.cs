using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DSDSplitter.Forms;

partial class SettingsDialog
{
    private IContainer? components = null;

    private Label _lblFormat = null!;
    private ComboBox _cboFormat = null!;

    private Label _lblOutputDir = null!;
    private ComboBox _cboOutputDir = null!;
    private Button _btnBrowse = null!;

    private Label _lblConcurrency = null!;
    private NumericUpDown _nudConcurrency = null!;

    private Label _lblDsd = null!;
    private ComboBox _cboDsd = null!;
    private NumericUpDown _nudSr = null!;
    private Label _lblHz = null!;
    private ComboBox _cboBits = null!;
    private Label _lblBit = null!;

    private Label _lblExisting = null!;
    private ComboBox _cboExisting = null!;

    private CheckBox _chkSound = null!;
    private CheckBox _chkKeepCue = null!;

    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new Container();
        SuspendLayout();

        ClientSize = new Size(440, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "设置";
        Font = new Font("Microsoft YaHei", 9F);

        _lblFormat = MakeLabel(20, 22, "输出格式:");
        _cboFormat = MakeCombo(130, 18, 160);

        _lblOutputDir = MakeLabel(20, 57, "输出目录:");
        _cboOutputDir = MakeCombo(130, 53, 160);
        _btnBrowse = new Button
        {
            Text = "浏览…", Location = new Point(300, 52), Size = new Size(90, 26),
        };
        _btnBrowse.Click += BtnBrowse_Click;

        _lblConcurrency = MakeLabel(20, 92, "并发数:");
        _nudConcurrency = new NumericUpDown
        {
            Location = new Point(130, 88), Size = new Size(70, 26),
            Minimum = 1, Maximum = 8, Value = 4,
        };

        _lblDsd = MakeLabel(20, 127, "DSD 转码:");
        _cboDsd = MakeCombo(130, 123, 200);
        _cboDsd.SelectedIndexChanged += (_, _) => UpdateCustomEnabled();

        _nudSr = new NumericUpDown
        {
            Location = new Point(130, 153), Size = new Size(80, 26),
            Minimum = 8000, Maximum = 384000, Increment = 100, Value = 44100,
        };
        _lblHz = MakeLabel(215, 157, "Hz /");
        _cboBits = MakeCombo(250, 153, 60);
        _lblBit = MakeLabel(315, 157, "bit");

        _lblExisting = MakeLabel(20, 192, "已存在文件:");
        _cboExisting = MakeCombo(130, 188, 160);

        _chkSound = new CheckBox
        {
            Text = "完成后播放提示音", Location = new Point(20, 225), Size = new Size(180, 24),
        };
        _chkKeepCue = new CheckBox
        {
            Text = "保留原始 CUE 不覆盖（输出 _fixed.cue）",
            Location = new Point(20, 250), Size = new Size(300, 24),
        };

        _btnOk = new Button
        {
            Text = "确定", Location = new Point(240, 290), Size = new Size(85, 30),
            DialogResult = DialogResult.None,
        };
        _btnOk.Click += BtnOk_Click;
        _btnCancel = new Button
        {
            Text = "取消", Location = new Point(335, 290), Size = new Size(85, 30),
            DialogResult = DialogResult.Cancel,
        };

        Controls.AddRange(new Control[]
        {
            _lblFormat, _cboFormat,
            _lblOutputDir, _cboOutputDir, _btnBrowse,
            _lblConcurrency, _nudConcurrency,
            _lblDsd, _cboDsd,
            _nudSr, _lblHz, _cboBits, _lblBit,
            _lblExisting, _cboExisting,
            _chkSound, _chkKeepCue,
            _btnOk, _btnCancel,
        });

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
        ResumeLayout(false);
    }

    private static Label MakeLabel(int x, int y, string text) => new()
    {
        Text = text, Location = new Point(x, y), AutoSize = true,
    };

    private static ComboBox MakeCombo(int x, int y, int w) => new()
    {
        Location = new Point(x, y), Size = new Size(w, 26), DropDownStyle = ComboBoxStyle.DropDownList,
    };
}
