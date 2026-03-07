// src/VoiceSync/ClipboardWatcher.cs
using System.Windows.Forms;

namespace VoiceSync;

/// <summary>
/// 注册为 Windows 剪贴板监听器，剪贴板内容变化时触发 Changed 事件。
/// 必须在 WinForms 消息循环的线程上创建和使用。
/// </summary>
internal sealed class ClipboardWatcher : NativeWindow, IDisposable
{
    public event EventHandler? Changed;

    public ClipboardWatcher()
    {
        CreateHandle(new CreateParams());
        NativeMethods.AddClipboardFormatListener(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
            Changed?.Invoke(this, EventArgs.Empty);
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        NativeMethods.RemoveClipboardFormatListener(Handle);
        DestroyHandle();
    }
}
