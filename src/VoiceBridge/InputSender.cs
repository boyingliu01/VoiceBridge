// src/VoiceBridge/InputSender.cs
using System.Runtime.InteropServices;

namespace VoiceBridge;

/// <summary>通过 Win32 SendInput 向当前前台窗口注入 Ctrl+V 键盘事件</summary>
internal static class InputSender
{
    public static void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];

        // Ctrl 按下
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;

        // V 按下
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = NativeMethods.VK_V;

        // V 释放
        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].u.ki.wVk = NativeMethods.VK_V;
        inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        // Ctrl 释放
        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>
    /// 清理所有可能卡住的修饰键状态（Ctrl、Alt、Shift、Win）。
    /// 在程序退出时调用，防止键盘状态异常。
    /// </summary>
    public static void ResetKeyboardState()
    {
        // 释放所有修饰键（使用 keybd_event 因为它可以可靠地清除状态）
        NativeMethods.keybd_event(NativeMethods.VK_LWIN, 0,
            NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_RWIN, 0,
            NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0,
            NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0,
            NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_SHIFT, 0,
            NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    /// <summary>
    /// 激活目标窗口并发送 Ctrl+V。
    /// 不使用 Alt 键模拟，改用 ShowWindow + SetForegroundWindow。
    /// </summary>
    public static void SendCtrlVToWindow(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: targetHwnd is Zero");
            return;
        }
        if (!NativeMethods.IsWindow(targetHwnd))
        {
            System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: targetHwnd is not a valid window");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"SendCtrlVToWindow: Start, targetHwnd={targetHwnd}");

            // 如果窗口最小化，先恢复
            if (NativeMethods.IsIconic(targetHwnd))
            {
                System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Window is iconic, restoring...");
                NativeMethods.ShowWindow(targetHwnd, NativeMethods.SW_RESTORE);
            }

            // 先确保所有修饰键都已释放，避免意外组合键
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0,
                NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0,
                NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_SHIFT, 0,
                NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_LWIN, 0,
                NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_RWIN, 0,
                NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 获取当前前台窗口和线程信息
            var currentForeground = NativeMethods.GetForegroundWindow();
            var currentThread = NativeMethods.GetCurrentThreadId();
            NativeMethods.GetWindowThreadProcessId(targetHwnd, out uint targetThreadId);
            NativeMethods.GetWindowThreadProcessId(currentForeground, out uint foregroundThreadId);

            System.Diagnostics.Debug.WriteLine($"SendCtrlVToWindow: currentThread={currentThread}, targetThreadId={targetThreadId}, foregroundThreadId={foregroundThreadId}");

            // 使用 AttachThreadInput 技巧来获得前台窗口切换权限
            bool attached = false;
            if (targetThreadId != currentThread && foregroundThreadId != currentThread)
            {
                System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Attaching thread input...");
                NativeMethods.AttachThreadInput(currentThread, foregroundThreadId, true);
                NativeMethods.AttachThreadInput(currentThread, targetThreadId, true);
                attached = true;
            }

            try
            {
                // 方法：先 ShowWindow 再 SetForegroundWindow（不使用 Alt 键模拟）
                System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Showing and setting foreground...");
                NativeMethods.ShowWindow(targetHwnd, NativeMethods.SW_SHOW);
                NativeMethods.SetForegroundWindow(targetHwnd);
                NativeMethods.SetFocus(targetHwnd);
            }
            finally
            {
                // 分离线程输入
                if (attached)
                {
                    System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Detaching thread input...");
                    NativeMethods.AttachThreadInput(currentThread, foregroundThreadId, false);
                    NativeMethods.AttachThreadInput(currentThread, targetThreadId, false);
                }
            }

            // 等待窗口激活
            System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Waiting 100ms for activation...");
            System.Threading.Thread.Sleep(100);

            // 验证窗口是否已激活
            var activated = NativeMethods.GetForegroundWindow();
            System.Diagnostics.Debug.WriteLine($"SendCtrlVToWindow: Current foreground={activated}, expected={targetHwnd}, match={activated == targetHwnd}");

            if (activated != targetHwnd)
            {
                // 如果 SetForegroundWindow 失败，尝试强制方式
                System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Activation failed, trying force...");
                ForceActivateWindow(targetHwnd);
            }

            // 再次确保修饰键已释放
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0,
                NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 刷新剪贴板内容，触发远程软件立即同步
            System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Refreshing clipboard...");
            RefreshClipboard();

            // 等待剪贴板同步
            System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Waiting 300ms for clipboard sync...");
            System.Threading.Thread.Sleep(300);

            // 发送 Ctrl+V
            System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Sending Ctrl+V...");
            SendCtrlV();
            System.Diagnostics.Debug.WriteLine("SendCtrlVToWindow: Ctrl+V sent");
        }
        finally
        {
            // 安全清理：确保所有修饰键都被释放
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0,
                NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0,
                NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    /// <summary>
    /// 强制激活窗口（备用方案，使用最小化/恢复技巧）
    /// </summary>
    private static void ForceActivateWindow(IntPtr hwnd)
    {
        // 使用最小化再恢复的方式强制激活
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
        System.Threading.Thread.Sleep(50);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    /// <summary>
    /// 刷新剪贴板内容：读取并重新写入，触发远程软件立即同步。
    /// 解决向日葵/RDP 剪贴板同步延迟问题。
    /// </summary>
    private static void RefreshClipboard()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("RefreshClipboard: Getting clipboard text...");
            // 读取当前剪贴板内容
            var text = System.Windows.Forms.Clipboard.GetText();
            System.Diagnostics.Debug.WriteLine($"RefreshClipboard: Clipboard text length={text?.Length ?? 0}, content='{text?.Substring(0, Math.Min(50, text?.Length ?? 0))}'");
            if (string.IsNullOrEmpty(text))
            {
                System.Diagnostics.Debug.WriteLine("RefreshClipboard: Clipboard is empty, skipping");
                return;
            }

            // 短暂延迟，确保剪贴板操作完成
            System.Threading.Thread.Sleep(50);

            // 重新写入剪贴板，触发远程软件立即同步
            System.Diagnostics.Debug.WriteLine("RefreshClipboard: Setting clipboard text to trigger sync...");
            System.Windows.Forms.Clipboard.SetText(text);
            System.Diagnostics.Debug.WriteLine("RefreshClipboard: Clipboard refreshed successfully");
        }
        catch (Exception ex)
        {
            // 剪贴板操作失败时静默处理
            System.Diagnostics.Debug.WriteLine($"RefreshClipboard: Exception - {ex.Message}");
        }
    }
}
