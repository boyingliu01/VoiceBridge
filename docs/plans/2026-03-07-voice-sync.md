# VoiceSync Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 构建一个 Windows 系统托盘程序，监听本地剪贴板变化，自动检测当前活动窗口是 RDP 还是向日葵远程控制，并自动向远程窗口粘贴文字，实现语音输入→文字自动出现在远程光标处，支持双向（在哪台机器上运行就服务那台机器）。

**Architecture:** 使用 WinForms 隐藏窗口作为 Win32 消息泵，通过 `AddClipboardFormatListener` 注册系统级剪贴板变化通知（无轮询、零延迟）；检测前台窗口进程名判断远程类型；向日葵用固定延迟等待剪贴板同步，RDP 用极短延迟后通过 `SendInput` 注入 Ctrl+V。

**Tech Stack:** C# .NET 8, Windows Forms, Win32 P/Invoke (user32.dll, kernel32.dll), xUnit + Moq (测试)

---

## 项目目录结构

```
VoiceSync/                          ← 项目根（可独立迁移）
├── VoiceSync.sln
├── src/
│   └── VoiceSync/
│       ├── VoiceSync.csproj
│       ├── Program.cs
│       ├── NativeMethods.cs        ← Win32 P/Invoke 声明
│       ├── ClipboardWatcher.cs     ← 监听剪贴板变化
│       ├── WindowDetector.cs       ← 检测活动窗口类型
│       ├── SyncEngine.cs           ← 核心粘贴逻辑
│       └── TrayIconApp.cs          ← 系统托盘 UI
├── tests/
│   └── VoiceSync.Tests/
│       ├── VoiceSync.Tests.csproj
│       ├── WindowDetectorTests.cs
│       └── SyncEngineTests.cs
├── publish/                        ← 构建输出（.gitignore）
└── docs/
    └── plans/
        └── 2026-03-07-voice-sync.md
```

---

## 前置条件

- 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download)（`dotnet --version` 应显示 8.x）
- 向日葵客户端设置中开启「剪贴板共享」（双向）
- 确认向日葵进程名：任务管理器 → 详细信息，找到向日葵相关 .exe 名称

---

### Task 1: 项目脚手架

**Files:**
- Create: `src/VoiceSync/VoiceSync.csproj`
- Create: `tests/VoiceSync.Tests/VoiceSync.Tests.csproj`
- Create: `VoiceSync.sln`

**Step 1: 创建解决方案**

```bash
cd E:/Study/LLM/SystemManagement/VoiceSync
dotnet new sln -n VoiceSync
dotnet new winforms -n VoiceSync -o src/VoiceSync --framework net8.0-windows
dotnet new xunit -n VoiceSync.Tests -o tests/VoiceSync.Tests --framework net8.0-windows
dotnet sln add src/VoiceSync/VoiceSync.csproj
dotnet sln add tests/VoiceSync.Tests/VoiceSync.Tests.csproj
cd tests/VoiceSync.Tests && dotnet add reference ../../src/VoiceSync/VoiceSync.csproj
dotnet add package Moq
```

**Step 2: 修改 VoiceSync.csproj 支持单文件发布**

将 `src/VoiceSync/VoiceSync.csproj` 内容替换为：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <AssemblyName>VoiceSync</AssemblyName>
    <RootNamespace>VoiceSync</RootNamespace>
    <!-- 发布配置：单文件自包含 .exe -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishReadyToRun>false</PublishReadyToRun>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>
</Project>
```

**Step 3: 验证构建通过**

```bash
cd E:/Study/LLM/SystemManagement/VoiceSync
dotnet build
```

Expected: `Build succeeded. 0 Error(s)`

**Step 4: Commit**

```bash
git init
echo "publish/\nbin/\nobj/\n*.user" > .gitignore
git add .
git commit -m "feat: scaffold VoiceSync project"
```

---

### Task 2: NativeMethods — Win32 API 声明

**Files:**
- Create: `src/VoiceSync/NativeMethods.cs`
- Delete: `src/VoiceSync/Form1.cs`（默认生成的，不需要）

**Step 1: 删除默认文件**

```bash
rm src/VoiceSync/Form1.cs
rm src/VoiceSync/Form1.Designer.cs  # 如果存在
```

**Step 2: 创建 NativeMethods.cs**

```csharp
// src/VoiceSync/NativeMethods.cs
using System.Runtime.InteropServices;

namespace VoiceSync;

internal static class NativeMethods
{
    // ── 剪贴板监听 ──────────────────────────────────────────────
    public const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    // ── 前台窗口检测 ─────────────────────────────────────────────
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // ── 进程名查询 ───────────────────────────────────────────────
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwAccess, bool bInherit, uint dwPid);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool QueryFullProcessImageName(
        IntPtr hProcess, int dwFlags,
        System.Text.StringBuilder lpExeName, ref uint lpdwSize);

    // ── 键盘输入注入 ─────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;        // 1 = KEYBOARD
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public MOUSEINPUT mi;   // 确保 union 尺寸正确
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_V = 0x56;
}
```

**Step 3: 验证编译**

```bash
dotnet build src/VoiceSync
```

Expected: `Build succeeded. 0 Error(s)`

**Step 4: Commit**

```bash
git add src/VoiceSync/NativeMethods.cs
git commit -m "feat: add Win32 P/Invoke declarations"
```

---

### Task 3: ClipboardWatcher — 剪贴板监听器

**Files:**
- Create: `src/VoiceSync/ClipboardWatcher.cs`

**Step 1: 创建 ClipboardWatcher.cs**

```csharp
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
```

**Step 2: 验证编译**

```bash
dotnet build src/VoiceSync
```

Expected: `Build succeeded. 0 Error(s)`

**Step 3: Commit**

```bash
git add src/VoiceSync/ClipboardWatcher.cs
git commit -m "feat: add clipboard watcher using AddClipboardFormatListener"
```

---

### Task 4: WindowDetector — 活动窗口类型检测

**Files:**
- Create: `src/VoiceSync/WindowDetector.cs`
- Create: `tests/VoiceSync.Tests/WindowDetectorTests.cs`

**Step 1: 先写失败的测试**

```csharp
// tests/VoiceSync.Tests/WindowDetectorTests.cs
using VoiceSync;
using Xunit;

namespace VoiceSync.Tests;

public class WindowDetectorTests
{
    [Theory]
    [InlineData("mstsc", RemoteType.Rdp)]
    [InlineData("MSTSC", RemoteType.Rdp)]           // 大小写不敏感
    [InlineData("SunloginClient", RemoteType.Sunflower)]
    [InlineData("Sunlogin", RemoteType.Sunflower)]
    [InlineData("notepad", RemoteType.None)]
    [InlineData(null, RemoteType.None)]
    public void Classify_ReturnsCorrectType(string? processName, RemoteType expected)
    {
        var result = WindowDetector.Classify(processName);
        Assert.Equal(expected, result);
    }
}
```

**Step 2: 运行测试，确认失败**

```bash
dotnet test tests/VoiceSync.Tests -- --filter "WindowDetectorTests"
```

Expected: 编译错误（`WindowDetector` 和 `RemoteType` 不存在）

**Step 3: 实现 WindowDetector.cs**

```csharp
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
    public RemoteType Detect() => Classify(GetForegroundProcessName());
}
```

**Step 4: 运行测试，确认通过**

```bash
dotnet test tests/VoiceSync.Tests -- --filter "WindowDetectorTests"
```

Expected: `Passed! - Failed: 0, Passed: 7`

**Step 5: Commit**

```bash
git add src/VoiceSync/WindowDetector.cs tests/VoiceSync.Tests/WindowDetectorTests.cs
git commit -m "feat: add WindowDetector with unit tests"
```

---

### Task 5: SyncEngine — 核心粘贴逻辑

**Files:**
- Create: `src/VoiceSync/SyncEngine.cs`
- Create: `tests/VoiceSync.Tests/SyncEngineTests.cs`

**Step 1: 先写失败的测试**

```csharp
// tests/VoiceSync.Tests/SyncEngineTests.cs
using Moq;
using VoiceSync;
using Xunit;

namespace VoiceSync.Tests;

public class SyncEngineTests
{
    private SyncEngine CreateEngine(RemoteType remoteType, out List<string> pastedTexts)
    {
        var detector = new Mock<WindowDetector>();
        detector.Setup(d => d.Detect()).Returns(remoteType);

        var pasted = new List<string>();
        pastedTexts = pasted;

        return new SyncEngine(detector.Object, text => pasted.Add(text))
        {
            RdpDelayMs = 0,
            SunflowerDelayMs = 0
        };
    }

    [Fact]
    public async Task OnClipboardChanged_Rdp_PastesText()
    {
        var engine = CreateEngine(RemoteType.Rdp, out var pasted);
        await engine.OnClipboardChanged("hello world");
        Assert.Single(pasted);
        Assert.Equal("hello world", pasted[0]);
    }

    [Fact]
    public async Task OnClipboardChanged_Sunflower_PastesText()
    {
        var engine = CreateEngine(RemoteType.Sunflower, out var pasted);
        await engine.OnClipboardChanged("test 测试");
        Assert.Single(pasted);
    }

    [Fact]
    public async Task OnClipboardChanged_NoRemote_DoesNotPaste()
    {
        var engine = CreateEngine(RemoteType.None, out var pasted);
        await engine.OnClipboardChanged("ignored");
        Assert.Empty(pasted);
    }

    [Fact]
    public async Task OnClipboardChanged_SameTextTwice_PastesOnlyOnce()
    {
        var engine = CreateEngine(RemoteType.Rdp, out var pasted);
        await engine.OnClipboardChanged("duplicate");
        await engine.OnClipboardChanged("duplicate");
        Assert.Single(pasted);
    }

    [Fact]
    public async Task OnClipboardChanged_WhenDisabled_DoesNotPaste()
    {
        var engine = CreateEngine(RemoteType.Rdp, out var pasted);
        engine.IsEnabled = false;
        await engine.OnClipboardChanged("should not paste");
        Assert.Empty(pasted);
    }

    [Fact]
    public async Task OnClipboardChanged_EmptyText_DoesNotPaste()
    {
        var engine = CreateEngine(RemoteType.Rdp, out var pasted);
        await engine.OnClipboardChanged("   ");
        Assert.Empty(pasted);
    }
}
```

**Step 2: 运行测试，确认失败**

```bash
dotnet test tests/VoiceSync.Tests -- --filter "SyncEngineTests"
```

Expected: 编译错误（`SyncEngine` 不存在）

**Step 3: 实现 SyncEngine.cs**

```csharp
// src/VoiceSync/SyncEngine.cs
namespace VoiceSync;

/// <summary>
/// 核心逻辑：剪贴板变化 → 检测远程类型 → 延迟 → 触发粘贴。
/// pasteAction 由外部注入（生产环境用 SendInput，测试用 mock）。
/// </summary>
internal class SyncEngine(WindowDetector detector, Action<string> pasteAction)
{
    public bool IsEnabled { get; set; } = true;
    public int RdpDelayMs { get; set; } = 150;
    public int SunflowerDelayMs { get; set; } = 900;

    private string _lastClip = string.Empty;

    public async Task OnClipboardChanged(string text)
    {
        if (!IsEnabled) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text == _lastClip) return;

        _lastClip = text;

        var remoteType = detector.Detect();
        if (remoteType == RemoteType.None) return;

        var delay = remoteType == RemoteType.Rdp ? RdpDelayMs : SunflowerDelayMs;
        if (delay > 0) await Task.Delay(delay);

        // 延迟后再次确认还在同一个远程窗口（防止用户切走后误粘）
        if (detector.Detect() == remoteType)
            pasteAction(text);
    }
}
```

**Step 4: 运行测试，确认全部通过**

```bash
dotnet test tests/VoiceSync.Tests -- --filter "SyncEngineTests"
```

Expected: `Passed! - Failed: 0, Passed: 6`

**Step 5: Commit**

```bash
git add src/VoiceSync/SyncEngine.cs tests/VoiceSync.Tests/SyncEngineTests.cs
git commit -m "feat: add SyncEngine with unit tests"
```

---

### Task 6: SendInput 辅助方法

**Files:**
- Create: `src/VoiceSync/InputSender.cs`

**Step 1: 实现 InputSender.cs**

```csharp
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
```

**Step 2: 验证编译**

```bash
dotnet build src/VoiceSync
```

Expected: `Build succeeded. 0 Error(s)`

**Step 3: Commit**

```bash
git add src/VoiceSync/InputSender.cs
git commit -m "feat: add InputSender for Win32 Ctrl+V injection"
```

---

### Task 7: TrayIconApp — 系统托盘 UI

**Files:**
- Create: `src/VoiceSync/TrayIconApp.cs`

**Step 1: 创建 TrayIconApp.cs**

```csharp
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
            Icon = SystemIcons.Application,
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

    private void UpdateTrayIcon()
    {
        _tray.Text = _engine.IsEnabled
            ? "VoiceSync ✓ 运行中"
            : "VoiceSync ✗ 已暂停";
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
```

**Step 2: 验证编译**

```bash
dotnet build src/VoiceSync
```

Expected: `Build succeeded. 0 Error(s)`

**Step 3: Commit**

```bash
git add src/VoiceSync/TrayIconApp.cs
git commit -m "feat: add system tray app with toggle and auto-start"
```

---

### Task 8: Program.cs — 入口点与单实例

**Files:**
- Modify: `src/VoiceSync/Program.cs`

**Step 1: 替换 Program.cs**

```csharp
// src/VoiceSync/Program.cs
using System.Windows.Forms;

namespace VoiceSync;

static class Program
{
    [STAThread]
    static void Main()
    {
        // 确保只有一个实例运行
        using var mutex = new Mutex(true, "VoiceSync_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("VoiceSync 已在运行中。\n请查看系统托盘图标。",
                "VoiceSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayIconApp());
    }
}
```

**Step 2: 完整构建并运行测试**

```bash
dotnet build
dotnet test
```

Expected: `Build succeeded`, `Passed! - Failed: 0`

**Step 3: Commit**

```bash
git add src/VoiceSync/Program.cs
git commit -m "feat: add entry point with single-instance mutex"
```

---

### Task 9: 本地运行验证

**Step 1: 运行程序**

```bash
dotnet run --project src/VoiceSync
```

Expected: 系统托盘出现图标

**Step 2: 手动测试剪贴板监听**

1. 打开任意文本编辑器（记事本）
2. 随意复制一段文字（Ctrl+C）
3. 确认托盘图标没有异常（不在远程窗口内，不应触发粘贴）

**Step 3: 测试 RDP 场景**

1. 打开 Windows 远程桌面（mstsc.exe）连接到目标机器
2. 在远程桌面窗口内点击一个文本输入框，让光标在里面
3. 在本机复制一段文字
4. 约 150ms 后，文字应自动出现在远程输入框中

**Step 4: 调整向日葵延迟（如需要）**

如果向日葵剪贴板同步较慢，打开 `src/VoiceSync/TrayIconApp.cs`，修改：
```csharp
_engine = new SyncEngine(detector, _ => InputSender.SendCtrlV())
{
    SunflowerDelayMs = 1200  // 可调整：800 ~ 1500
};
```

---

### Task 10: 发布单文件 .exe

**Step 1: 发布**

```bash
cd E:/Study/LLM/SystemManagement/VoiceSync
dotnet publish src/VoiceSync -c Release -o publish/
```

Expected: `publish/VoiceSync.exe`（约 60-80 MB，自包含）

**Step 2: 验证**

双击 `publish/VoiceSync.exe`，系统托盘出现图标，功能正常。

**Step 3: 部署到其他机器**

把 `VoiceSync.exe` 复制到另外两台机器，直接运行即可（无需安装 .NET）。
右键托盘图标 → 开机自启 → 勾选。

**Step 4: 最终 Commit**

```bash
git add publish/.gitkeep
echo "publish/*.exe" >> .gitignore
git add .gitignore
git commit -m "chore: add publish gitignore, ready for deployment"
```

---

## 调试常见问题

| 症状 | 可能原因 | 排查方式 |
|------|----------|----------|
| 向日葵不触发粘贴 | 进程名不匹配 | 任务管理器查实际进程名，加入 `SunflowerNames` 数组 |
| 向日葵粘贴内容是旧内容 | 延迟不足 | 把 `SunflowerDelayMs` 调大到 1200-1500 |
| RDP 粘贴时机不对 | 窗口失焦 | 确认复制时 RDP 窗口是前台窗口 |
| 每次复制都触发粘贴 | 正常行为，可暂停 | 右键托盘 → 暂停同步 |

---

## 说明：双向支持

程序天然支持双向：**安装在哪台机器，就服务那台机器的语音输入**。

- 笔记本上运行 → 笔记本语音输入，自动粘贴到 RDP/向日葵远程窗口
- 台式机上运行 → 台式机语音输入，自动粘贴到它控制的远程窗口

三台机器各装一份，互不干扰。
