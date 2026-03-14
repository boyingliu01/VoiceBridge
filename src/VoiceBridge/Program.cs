// src/VoiceSync/Program.cs
using System.Windows.Forms;

namespace VoiceBridge;

static class Program
{
    [STAThread]
    static void Main()
    {
        // 确保只有一个实例运行
        using var mutex = new Mutex(true, "VoiceBridge_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("VoiceBridge 已在运行中。\n请查看系统托盘图标。",
                "VoiceBridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayIconApp());
    }
}
