using System;
using System.Windows.Forms;
using DSDSplitter.Forms;

namespace DSDSplitter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 高 DPI（开发文档第 2 节 / 第 0 节 #18）
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
