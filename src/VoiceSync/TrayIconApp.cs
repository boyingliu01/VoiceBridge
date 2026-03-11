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

        menu.Items.AddRange([toggleItem, autoRunItem, new ToolStripSeparator(), exitItem]);
        return menu;
    }

    private static Icon LoadTrayIcon()
    {
        var asm = typeof(TrayIconApp).Assembly;
        using var stream = asm.GetManifestResourceStream("VoiceSync.tray.ico");
        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }

    private void UpdateTrayIcon()
    {
        _tray.Text = _engine.IsEnabled
            ? "VoiceSync ● 运行中"
            : "VoiceSync ○ 已暂停";
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
