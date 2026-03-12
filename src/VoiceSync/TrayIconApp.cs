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
            return;
        }

        // 检测当前前台窗口是否是远程窗口
        var detector = new WindowDetector();
        var remoteType = detector.Detect();

        if (remoteType == RemoteType.None)
        {
            MessageBox.Show(
                "请先将焦点切换到远程桌面窗口（RDP 或向日葵），\n然后再开启语音模式。",
                "VoiceSync",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // 记录当前远程窗口
        var hwnd = detector.GetForegroundWindowHandle();
        _engine.StartVoiceMode(hwnd, remoteType, InputSender.SendCtrlVToWindow);
        _voiceModeItem!.Checked = true;
        UpdateTrayIcon();
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
        using var key = Registry.CurrentUser.OpenSubKey(AutoRunKey, true)!;
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
            _watcher.Changed -= OnClipboardChanged;
            _watcher.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}