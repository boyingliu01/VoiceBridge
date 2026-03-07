// src/VoiceSync/InputSender.cs
using System.Runtime.InteropServices;

namespace VoiceSync;

/// <summary>通过 Win32 SendInput 向当前前台窗口注入 Ctrl+V 键盘事件</summary>
internal static class InputSender
{
    public static void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];

        // Ctrl 按下
        inputs[0].type = 1;
        inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;

        // V 按下
        inputs[1].type = 1;
        inputs[1].u.ki.wVk = NativeMethods.VK_V;

        // V 释放
        inputs[2].type = 1;
        inputs[2].u.ki.wVk = NativeMethods.VK_V;
        inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        // Ctrl 释放
        inputs[3].type = 1;
        inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
        inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

        NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
