// src/VoiceSync/WindowDetector.cs
using System.Text;

namespace VoiceSync;

public enum RemoteType { None, Rdp, Sunflower }

internal class WindowDetector
{
    // 向日葵各版本可能的进程名（不含 .exe）
    private static readonly string[] SunflowerNames =
    [
        "SunloginClient",
        "Sunlogin",
        "向日葵远程控制",
        "RustDesk",        // 备用：开源替代品
    ];

    /// <summary>根据进程名判断远程类型（纯函数，便于单元测试）</summary>
    public static RemoteType Classify(string? processName)
    {
        if (processName is null) return RemoteType.None;

        if (processName.Equals("mstsc", StringComparison.OrdinalIgnoreCase))
            return RemoteType.Rdp;

        foreach (var name in SunflowerNames)
            if (processName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return RemoteType.Sunflower;

        return RemoteType.None;
    }

    /// <summary>获取当前前台窗口的进程名（不含 .exe 后缀）</summary>
    public virtual string? GetForegroundProcessName()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        var hProc = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return null;

        try
        {
            var sb = new StringBuilder(512);
            uint size = (uint)sb.Capacity;
            return NativeMethods.QueryFullProcessImageName(hProc, 0, sb, ref size)
                ? Path.GetFileNameWithoutExtension(sb.ToString())
                : null;
        }
        finally
        {
            NativeMethods.CloseHandle(hProc);
        }
    }

    /// <summary>检测当前前台窗口的远程类型</summary>
    public virtual RemoteType Detect() => Classify(GetForegroundProcessName());

    /// <summary>获取当前前台窗口句柄</summary>
    public virtual IntPtr GetForegroundWindowHandle() => NativeMethods.GetForegroundWindow();
}
