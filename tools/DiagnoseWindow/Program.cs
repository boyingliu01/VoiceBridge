// 诊断脚本：每秒自动检测前台窗口进程名
// 运行：dotnet run --project tools/DiagnoseWindow

using System.Runtime.InteropServices;
using System.Text;

class Program
{
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint dwAccess, bool bInherit, uint dwPid);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    static void Main()
    {
        Console.WriteLine("=== 前台窗口诊断（自动模式）===\n");
        Console.WriteLine("操作说明：");
        Console.WriteLine("1. 脚本会每秒自动检测前台窗口");
        Console.WriteLine("2. 切换到你想检测的窗口，观察输出");
        Console.WriteLine("3. 按 Ctrl+C 退出\n");
        Console.WriteLine("开始检测...\n");

        string? lastProcessName = null;

        while (true)
        {
            var result = DetectForegroundWindow();

            // 只在进程名变化时输出
            if (result != null && result != lastProcessName)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 切换到: {result}");
                lastProcessName = result;
            }

            Thread.Sleep(200);
        }
    }

    static string? DetectForegroundWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return null;

        var hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProc == IntPtr.Zero) return null;

        try
        {
            var sb = new StringBuilder(512);
            uint size = (uint)sb.Capacity;
            if (QueryFullProcessImageName(hProc, 0, sb, ref size))
            {
                return Path.GetFileNameWithoutExtension(sb.ToString());
            }
        }
        finally
        {
            CloseHandle(hProc);
        }

        return null;
    }
}