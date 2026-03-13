// src/VoiceSync/WindowDetector.cs
using System.Text;

namespace VoiceSync;

public enum RemoteType { None, Rdp, Sunflower }

internal class WindowDetector
{
    // 向日葵各版本可能的进程名（不含 .exe）
    private static readonly string[] SunflowerNames =
    [
        "AweSun",           // 向日葵远程控制主窗口（用户确认）
        "SunloginClient",   // 旧版本
        "Sunlogin",         // 旧版本
        "向日葵远程控制",    // 中文版本
        "RustDesk",         // 备用：开源替代品
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

        return GetProcessNameByHwnd(hwnd);
    }

    /// <summary>根据窗口句柄获取进程名（不含 .exe 后缀）</summary>
    private static string? GetProcessNameByHwnd(IntPtr hwnd)
    {
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

    /// <summary>获取窗口标题</summary>
    public static string GetWindowTitle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;
        var length = NativeMethods.GetWindowTextLength(hwnd);
        if (length == 0) return string.Empty;
        var sb = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>获取前台窗口信息（句柄、进程名、窗口标题）</summary>
    public static (IntPtr Handle, string? ProcessName, string Title) GetForegroundWindowInfo()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return (IntPtr.Zero, null, string.Empty);

        var processName = GetProcessNameByHwnd(hwnd);
        var title = GetWindowTitle(hwnd);

        return (hwnd, processName, title);
    }
}
