// src/VoiceSync/TrayIconApp.cs
using System.Windows.Forms;
using Microsoft.Win32;

namespace VoiceSync;

/// <summary>
/// WinForms ApplicationContext：管理托盘图标、消息循环、组件生命周期。
/// </summary>
internal sealed class TrayIconApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ClipboardWatcher _watcher;
    private readonly SyncEngine _engine;
    private ToolStripMenuItem? _voiceModeItem;

    private const string AutoRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AutoRunName = "VoiceSync";

    public TrayIconApp()
    {
        var detector = new WindowDetector();
        _engine = new SyncEngine(detector, _ => InputSender.SendCtrlV());

        _watcher = new ClipboardWatcher();
        _watcher.Changed += OnClipboardChanged;

        _tray = new NotifyIcon
        {
            Text = "VoiceSync — 语音远程同步",
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        UpdateTrayIcon();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        // 语音模式菜单项
        _voiceModeItem = new ToolStripMenuItem("语音模式");
        _voiceModeItem.Click += OnVoiceModeClick;

        var toggleItem = new ToolStripMenuItem("暂停同步");
        toggleItem.Click += (_, _) =>
        {
            _engine.IsEnabled = !_engine.IsEnabled;
            toggleItem.Text = _engine.IsEnabled ? "暂停同步" : "恢复同步";
            UpdateTrayIcon();
        };

        var autoRunItem = new ToolStripMenuItem("开机自启")
        {
            Checked = IsAutoRunEnabled()
        };
        autoRunItem.Click += (_, _) =>
        {
            autoRunItem.Checked = ToggleAutoRun();
        };

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitThread();

        menu.Items.AddRange([_voiceModeItem, toggleItem, autoRunItem, new ToolStripSeparator(), exitItem]);
        return menu;
    }

    private void OnVoiceModeClick(object? sender, EventArgs e)
    {
        if (_engine.VoiceModeEnabled)
        {
            // 关闭语音模式
            _engine.StopVoiceMode();
            _voiceModeItem!.Checked = false;
            UpdateTrayIcon();
            _tray.ShowBalloonTip(2000, "语音模式", "已关闭", ToolTipIcon.Info);
            return;
        }

        // 显示倒计时提示，让用户有时间切换到目标窗口
        // 使用 MessageBox 确保用户能看到提示
        var result = MessageBox.Show(
            "点击确定后，请在 5 秒内切换到目标窗口（如向日葵远程窗口）。\n\n" +
            "时间到后会自动捕获当前窗口。",
            "开启语音模式",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);

        if (result != DialogResult.OK) return;

        // 5秒后检测前台窗口
        var timer = new System.Windows.Forms.Timer { Interval = 5000 };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            StartVoiceModeWithConfirmation();
        };
        timer.Start();
    }

    private void StartVoiceModeWithConfirmation()
    {
        var (hwnd, processName, title) = WindowDetector.GetForegroundWindowInfo();

        if (hwnd == IntPtr.Zero)
        {
            MessageBox.Show(
                "无法获取当前前台窗口，请重试。",
                "VoiceSync",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        // 确定远程类型（用于选择延迟时间）
        var remoteType = WindowDetector.Classify(processName);

        // 开启语音模式
        _engine.StartVoiceMode(hwnd, remoteType, InputSender.SendCtrlVToWindow);
        _voiceModeItem!.Checked = true;
        UpdateTrayIcon();

        // 显示确认信息
        var displayInfo = string.IsNullOrEmpty(title) ? processName ?? "未知窗口" : title;

        string modeInfo = remoteType switch
        {
            RemoteType.Rdp => "RDP 远程桌面（延迟 150ms）",
            RemoteType.Sunflower => "向日葵远程（延迟 900ms）",
            _ => "本地窗口（延迟 900ms）"
        };

        // 使用 MessageBox 确保用户能看到确认
        MessageBox.Show(
            $"目标窗口: {displayInfo}\n进程: {processName ?? "未知"}\n{modeInfo}\n\n" +
            "现在你可以切换到本地窗口使用语音输入了。",
            "语音模式已开启",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static Icon LoadTrayIcon()
    {
        var asm = typeof(TrayIconApp).Assembly;
        using var stream = asm.GetManifestResourceStream("VoiceSync.tray.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }

    private void UpdateTrayIcon()
    {
        if (_engine.VoiceModeEnabled)
        {
            _tray.Text = "VoiceSync 🎤 语音模式";
        }
        else if (_engine.IsEnabled)
        {
            _tray.Text = "VoiceSync ● 运行中";
        }
        else
        {
            _tray.Text = "VoiceSync ○ 已暂停";
        }
    }

    private async void OnClipboardChanged(object? sender, EventArgs e)
    {
        string text;
        try { text = Clipboard.GetText(); }
        catch { return; }

        await _engine.OnClipboardChanged(text);
    }

    private static bool IsAutoRunEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutoRunKey, false);
        return key?.GetValue(AutoRunName) is not null;
    }

    private static bool ToggleAutoRun()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AutoRunKey, true);
        if (key is null)
        {
            // 无法打开注册表键，操作失败
            return false;
        }

        if (key.GetValue(AutoRunName) is not null)
        {
            key.DeleteValue(AutoRunName);
            return false;
        }
        else
        {
            key.SetValue(AutoRunName, Application.ExecutablePath);
            return true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 清理键盘状态，防止修饰键卡住
            InputSender.ResetKeyboardState();

            _watcher.Changed -= OnClipboardChanged;
            _watcher.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}