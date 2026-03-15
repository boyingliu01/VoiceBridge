// src/VoiceSync/InputSender.cs
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
        if (targetHwnd == IntPtr.Zero) return;
        if (!NativeMethods.IsWindow(targetHwnd)) return;

        try
        {
            // 如果窗口最小化，先恢复
            if (NativeMethods.IsIconic(targetHwnd))
            {
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

            // 使用 AttachThreadInput 技巧来获得前台窗口切换权限
            bool attached = false;
            if (targetThreadId != currentThread && foregroundThreadId != currentThread)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThreadId, true);
                NativeMethods.AttachThreadInput(currentThread, targetThreadId, true);
                attached = true;
            }

            try
            {
                // 方法：先 ShowWindow 再 SetForegroundWindow（不使用 Alt 键模拟）
                NativeMethods.ShowWindow(targetHwnd, NativeMethods.SW_SHOW);
                NativeMethods.SetForegroundWindow(targetHwnd);
                NativeMethods.SetFocus(targetHwnd);
            }
            finally
            {
                // 分离线程输入
                if (attached)
                {
                    NativeMethods.AttachThreadInput(currentThread, foregroundThreadId, false);
                    NativeMethods.AttachThreadInput(currentThread, targetThreadId, false);
                }
            }

            // 等待窗口激活
            System.Threading.Thread.Sleep(100);

            // 验证窗口是否已激活
            var activated = NativeMethods.GetForegroundWindow();
            if (activated != targetHwnd)
            {
                // 如果 SetForegroundWindow 失败，尝试强制方式
                ForceActivateWindow(targetHwnd);
            }

            // 再次确保修饰键已释放
            NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0,
                NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 发送 Ctrl+V
            SendCtrlV();
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
}
