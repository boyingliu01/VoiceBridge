// 诊断脚本：枚举向日葵窗口的子窗口
// 运行：dotnet run --project tools/DiagnoseWindow

using System.Runtime.InteropServices;
using System.Text;

class Program
{
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    const uint GW_CHILD = 0x0005;
    const uint GW_ENABLEDPOPUP = 0x0006;
    const uint GW_HWNDFIRST = 0;
    const uint GW_HWNDNEXT = 2;
    const int GWL_STYLE = -16;
    const int GWL_EXSTYLE = -20;
    const int WS_VISIBLE = 0x10000000;
    const int WS_DISABLED = 0x08000000;
    const int WS_TABSTOP = 0x00010000;

    static void Main()
    {
        Console.WriteLine("=== 向日葵窗口层次结构诊断 ===\n");
        Console.WriteLine("每 3 秒检测一次，按 Ctrl+C 退出\n");

        while (true)
        {
            DetectWindowHierarchy();
            Thread.Sleep(3000);
        }
    }

    static void DetectWindowHierarchy()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 没有前台窗口");
            return;
        }

        var processName = GetProcessNameByHwnd(hwnd);

        Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] === 前台窗口层次 ===");
        Console.WriteLine($"主窗口: {hwnd} (进程: {processName})");

        // 枚举子窗口
        var child = GetWindow(hwnd, GW_CHILD);
        if (child != IntPtr.Zero)
        {
            Console.WriteLine("\n子窗口列表:");
            int count = 0;
            while (child != IntPtr.Zero && count < 20)
            {
                var title = GetWindowTitle(child);
                var className = GetClassNameStr(child);
                var style = GetWindowLong(child, GWL_STYLE);
                var isVisible = IsWindowVisible(child);
                var isDisabled = (style & WS_DISABLED) != 0;
                var isTabStop = (style & WS_TABSTOP) != 0;

                Console.WriteLine($"  [{count}] hwnd={child}, class={className}");
                Console.WriteLine($"       title='{title}', visible={isVisible}, disabled={isDisabled}, tabstop={isTabStop}");
                Console.WriteLine($"       style=0x{style:X8}");

                // 继续下一个同级窗口
                child = GetWindow(child, GW_HWNDNEXT);
                count++;
            }
        }
        else
        {
            Console.WriteLine("  (无子窗口)");
        }
    }

    static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length == 0) return "(无标题)";
        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    static string GetClassNameStr(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    static string? GetProcessNameByHwnd(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;
        return $"PID={pid}";
    }
}
