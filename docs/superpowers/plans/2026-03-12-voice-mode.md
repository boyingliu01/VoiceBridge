# Voice Mode (语音模式) Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Voice Mode" feature that allows users to switch to a local window for voice input, then automatically paste the text to a previously-captured remote window.

**Architecture:** The Voice Mode works by capturing the remote window handle when the user enables it, then when clipboard changes, it activates that window and sends Ctrl+V. Uses Win32 `SetForegroundWindow` with `AttachThreadInput` trick to reliably switch windows.

**Tech Stack:** .NET 8, WinForms, P/Invoke (user32.dll), xUnit, Moq

---

## File Structure

| File | Responsibility |
|------|----------------|
| `NativeMethods.cs` | Win32 API declarations (existing + new window activation APIs) |
| `WindowDetector.cs` | Detect remote windows + get window handles |
| `InputSender.cs` | Send keyboard input + activate window before sending |
| `SyncEngine.cs` | Core logic: voice mode state, target window tracking |
| `TrayIconApp.cs` | UI: voice mode menu item, status display |
| `SyncEngineTests.cs` | Unit tests for voice mode |

---

## Chunk 1: NativeMethods - Add Window Activation APIs

### Task 1: Add Window Activation P/Invoke Declarations

**Files:**
- Modify: `src/VoiceSync/NativeMethods.cs:74-77` (add after existing constants)

- [ ] **Step 1: Add new P/Invoke declarations to NativeMethods.cs**

Add after line 76 (after `VK_V = 0x56`):

```csharp
    // ── 窗口激活 ─────────────────────────────────────────────────
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    public const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hwnd);  // minimized

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);

    // For simulating Alt key to bypass SetForegroundWindow restriction
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public const byte VK_MENU = 0x12;  // Alt key
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
```

- [ ] **Step 2: Build to verify no syntax errors**

Run: `dotnet build src/VoiceSync`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/VoiceSync/NativeMethods.cs
git commit -m "feat: add window activation Win32 APIs for voice mode"
```

---

## Chunk 2: WindowDetector - Add GetForegroundWindowHandle

### Task 2: Add Window Handle Getter

**Files:**
- Modify: `src/VoiceSync/WindowDetector.cs:62` (add method before closing brace)

- [ ] **Step 1: Add GetForegroundWindowHandle method**

Add after line 62 (after `Detect()` method):

```csharp
    /// <summary>获取当前前台窗口句柄</summary>
    public virtual IntPtr GetForegroundWindowHandle() => NativeMethods.GetForegroundWindow();
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/VoiceSync`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/VoiceSync/WindowDetector.cs
git commit -m "feat: add GetForegroundWindowHandle method"
```

---

## Chunk 3: InputSender - Add SendCtrlVToWindow

### Task 3: Add Window Activation + Paste Method

**Files:**
- Modify: `src/VoiceSync/InputSender.cs:32` (add method before closing brace)

- [ ] **Step 1: Add SendCtrlVToWindow method with window activation logic**

Add after line 32 (after `SendCtrlV()` method):

```csharp
    /// <summary>
    /// 激活目标窗口并发送 Ctrl+V。
    /// 使用 AttachThreadInput 技巧绕过 SetForegroundWindow 限制。
    /// </summary>
    public static void SendCtrlVToWindow(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero) return;
        if (!NativeMethods.IsWindow(targetHwnd)) return;

        // 如果窗口最小化，先恢复
        if (NativeMethods.IsIconic(targetHwnd))
        {
            NativeMethods.ShowWindow(targetHwnd, NativeMethods.SW_RESTORE);
        }

        // 获取当前前台窗口和线程信息
        var currentForeground = NativeMethods.GetForegroundWindow();
        var currentThread = NativeMethods.GetCurrentThreadId();
        NativeMethods.GetWindowThreadProcessId(targetHwnd, out uint targetThreadId);
        NativeMethods.GetWindowThreadProcessId(currentForeground, out uint foregroundThreadId);

        // 使用 AttachThreadInput 技巧来获得前台窗口切换权限
        if (targetThreadId != currentThread && foregroundThreadId != currentThread)
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThreadId, true);
            NativeMethods.AttachThreadInput(currentThread, targetThreadId, true);
        }

        // 按 Alt 键绕过 SetForegroundWindow 限制
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);

        // 切换到目标窗口
        NativeMethods.SetForegroundWindow(targetHwnd);

        // 释放 Alt 键
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

        // 分离线程输入
        if (targetThreadId != currentThread && foregroundThreadId != currentThread)
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThreadId, false);
            NativeMethods.AttachThreadInput(currentThread, targetThreadId, false);
        }

        // 短暂等待窗口激活
        System.Threading.Thread.Sleep(50);

        // 发送 Ctrl+V
        SendCtrlV();
    }
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/VoiceSync`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/VoiceSync/InputSender.cs
git commit -m "feat: add SendCtrlVToWindow with window activation"
```

---

## Chunk 4: SyncEngine - Add Voice Mode State

### Task 4: Add Voice Mode Properties and Methods

**Files:**
- Modify: `src/VoiceSync/SyncEngine.cs` (multiple additions)

- [ ] **Step 1: Add voice mode fields and properties**

Replace line 8-14 with:

```csharp
internal class SyncEngine(WindowDetector detector, Action<string> pasteAction)
{
    public bool IsEnabled { get; set; } = true;
    public int RdpDelayMs { get; set; } = 150;
    public int SunflowerDelayMs { get; set; } = 900;

    // ── 语音模式 ─────────────────────────────────────────────────
    public bool VoiceModeEnabled { get; private set; }
    public IntPtr TargetWindowHandle { get; private set; }
    private RemoteType _targetRemoteType;
    private Action<IntPtr>? _pasteToWindowAction;

    private string _lastClip = string.Empty;

    /// <summary>开启语音模式，记录目标窗口</summary>
    public void StartVoiceMode(IntPtr hwnd, RemoteType remoteType, Action<IntPtr>? pasteToWindow = null)
    {
        VoiceModeEnabled = true;
        TargetWindowHandle = hwnd;
        _targetRemoteType = remoteType;
        _pasteToWindowAction = pasteToWindow;
    }

    /// <summary>关闭语音模式</summary>
    public void StopVoiceMode()
    {
        VoiceModeEnabled = false;
        TargetWindowHandle = IntPtr.Zero;
        _pasteToWindowAction = null;
    }
```

- [ ] **Step 2: Modify OnClipboardChanged to handle voice mode**

Replace line 16-33 with:

```csharp
    public async Task OnClipboardChanged(string text)
    {
        if (!IsEnabled) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text == _lastClip) return;

        _lastClip = text;

        // 语音模式：直接粘贴到目标窗口
        if (VoiceModeEnabled && TargetWindowHandle != IntPtr.Zero)
        {
            var delay = _targetRemoteType == RemoteType.Rdp ? RdpDelayMs : SunflowerDelayMs;
            if (delay > 0) await Task.Delay(delay);

            if (_pasteToWindowAction is not null)
                _pasteToWindowAction(TargetWindowHandle);
            else
                pasteAction(text);

            return;
        }

        // 正常模式：检测前台窗口是否是远程窗口
        var remoteType = detector.Detect();
        if (remoteType == RemoteType.None) return;

        var delay2 = remoteType == RemoteType.Rdp ? RdpDelayMs : SunflowerDelayMs;
        if (delay2 > 0) await Task.Delay(delay2);

        // 延迟后再次确认还在同一个远程窗口（防止用户切走后误粘）
        if (detector.Detect() == remoteType)
            pasteAction(text);
    }
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/VoiceSync`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/VoiceSync/SyncEngine.cs
git commit -m "feat: add voice mode state and logic to SyncEngine"
```

---

## Chunk 5: Tests - Add Voice Mode Tests

### Task 5: Add Unit Tests for Voice Mode

**Files:**
- Modify: `tests/VoiceSync.Tests/SyncEngineTests.cs:75` (add after last test)

- [ ] **Step 1: Add voice mode test methods**

Add after line 75 (after last `}`):

```csharp

    // ── 语音模式测试 ─────────────────────────────────────────────

    [Fact]
    public async Task VoiceMode_PastesToTargetWindow()
    {
        var detector = new Mock<WindowDetector>();
        detector.Setup(d => d.Detect()).Returns(RemoteType.None); // foreground is NOT remote

        var pastedToWindow = new List<IntPtr>();
        var engine = new SyncEngine(detector.Object, _ => { })
        {
            RdpDelayMs = 0,
            SunflowerDelayMs = 0
        };

        var fakeHwnd = new IntPtr(12345);
        engine.StartVoiceMode(fakeHwnd, RemoteType.Sunflower, hwnd => pastedToWindow.Add(hwnd));

        await engine.OnClipboardChanged("voice text");

        Assert.Single(pastedToWindow);
        Assert.Equal(fakeHwnd, pastedToWindow[0]);
    }

    [Fact]
    public async Task VoiceMode_WhenDisabled_FallsBackToNormalBehavior()
    {
        var detector = new Mock<WindowDetector>();
        detector.Setup(d => d.Detect()).Returns(RemoteType.Rdp);

        var pasted = new List<string>();
        var engine = new SyncEngine(detector.Object, text => pasted.Add(text))
        {
            RdpDelayMs = 0,
            SunflowerDelayMs = 0
        };

        // Start then stop voice mode
        engine.StartVoiceMode(new IntPtr(123), RemoteType.Sunflower, _ => { });
        engine.StopVoiceMode();

        await engine.OnClipboardChanged("normal text");

        // Should use normal behavior: check foreground, it's RDP, so paste
        Assert.Single(pasted);
        Assert.Equal("normal text", pasted[0]);
    }

    [Fact]
    public void StartVoiceMode_SetsTargetWindow()
    {
        var detector = new Mock<WindowDetector>();
        var engine = new SyncEngine(detector.Object, _ => { });

        var hwnd = new IntPtr(999);
        engine.StartVoiceMode(hwnd, RemoteType.Rdp, _ => { });

        Assert.True(engine.VoiceModeEnabled);
        Assert.Equal(hwnd, engine.TargetWindowHandle);
    }

    [Fact]
    public void StopVoiceMode_ClearsTargetWindow()
    {
        var detector = new Mock<WindowDetector>();
        var engine = new SyncEngine(detector.Object, _ => { });

        engine.StartVoiceMode(new IntPtr(999), RemoteType.Rdp, _ => { });
        engine.StopVoiceMode();

        Assert.False(engine.VoiceModeEnabled);
        Assert.Equal(IntPtr.Zero, engine.TargetWindowHandle);
    }

    [Fact]
    public async Task VoiceMode_AppliesCorrectDelay()
    {
        var detector = new Mock<WindowDetector>();
        var engine = new SyncEngine(detector.Object, _ => { })
        {
            RdpDelayMs = 0,
            SunflowerDelayMs = 50  // Small delay for testing
        };

        var pasted = false;
        engine.StartVoiceMode(new IntPtr(1), RemoteType.Sunflower, _ => pasted = true);

        var task = engine.OnClipboardChanged("test");
        // Before delay completes
        Assert.False(pasted);

        await task;
        // After delay completes
        Assert.True(pasted);
    }
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test --filter "ClassName=SyncEngineTests"`
Expected: All tests pass (10 passed)

- [ ] **Step 3: Commit**

```bash
git add tests/VoiceSync.Tests/SyncEngineTests.cs
git commit -m "test: add voice mode unit tests"
```

---

## Chunk 6: TrayIconApp - Add Voice Mode UI

### Task 6: Add Voice Mode Menu Item

**Files:**
- Modify: `src/VoiceSync/TrayIconApp.cs` (multiple changes)

- [ ] **Step 1: Add voice mode field**

Add after line 14 (after `_engine` field):

```csharp
    private ToolStripMenuItem? _voiceModeItem;
```

- [ ] **Step 2: Modify BuildMenu to add voice mode item**

Replace lines 38-63 with:

```csharp
    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        // 语音模式菜单项
        _voiceModeItem = new ToolStripMenuItem("语音模式");
        _voiceModeItem.Click += OnVoiceModeClick;

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

        menu.Items.AddRange([_voiceModeItem, toggleItem, autoRunItem, new ToolStripSeparator(), exitItem]);
        return menu;
    }
```

- [ ] **Step 3: Add OnVoiceModeClick handler**

Add after `BuildMenu` method (before `LoadTrayIcon`):

```csharp
    private void OnVoiceModeClick(object? sender, EventArgs e)
    {
        if (_engine.VoiceModeEnabled)
        {
            // 关闭语音模式
            _engine.StopVoiceMode();
            _voiceModeItem!.Checked = false;
            UpdateTrayIcon();
            return;
        }

        // 检测当前前台窗口是否是远程窗口
        var detector = new WindowDetector();
        var remoteType = detector.Detect();

        if (remoteType == RemoteType.None)
        {
            MessageBox.Show(
                "请先将焦点切换到远程桌面窗口（RDP 或向日葵），\n然后再开启语音模式。",
                "VoiceSync",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // 记录当前远程窗口
        var hwnd = detector.GetForegroundWindowHandle();
        _engine.StartVoiceMode(hwnd, remoteType, InputSender.SendCtrlVToWindow);
        _voiceModeItem!.Checked = true;
        UpdateTrayIcon();
    }
```

- [ ] **Step 4: Modify UpdateTrayIcon to show voice mode status**

Replace lines 73-78 with:

```csharp
    private void UpdateTrayIcon()
    {
        if (_engine.VoiceModeEnabled)
        {
            _tray.Text = "VoiceSync 🎤 语音模式";
        }
        else if (_engine.IsEnabled)
        {
            _tray.Text = "VoiceSync ● 运行中";
        }
        else
        {
            _tray.Text = "VoiceSync ○ 已暂停";
        }
    }
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build src/VoiceSync`
Expected: Build succeeded

- [ ] **Step 6: Run all tests**

Run: `dotnet test`
Expected: All tests pass

- [ ] **Step 7: Commit**

```bash
git add src/VoiceSync/TrayIconApp.cs
git commit -m "feat: add voice mode menu item and UI logic"
```

---

## Chunk 7: Final Verification and Integration

### Task 7: Full Build, Test, and Publish

- [ ] **Step 1: Run full test suite**

Run: `dotnet test`
Expected: All tests pass

- [ ] **Step 2: Build and publish**

Run: `dotnet publish src/VoiceSync -c Release -o publish/`
Expected: Build succeeded, VoiceSync.exe created

- [ ] **Step 3: Update CLAUDE.md with voice mode documentation**

Add after the Architecture section in `CLAUDE.md`:

```markdown
## Voice Mode

Voice Mode solves the "focus hijacking" problem when using voice input with remote desktop:

1. User switches to remote window (RDP/Sunflower)
2. Right-click tray icon → "Voice Mode"
3. System captures the remote window handle
4. User switches to any local window
5. User triggers voice input (e.g., WeChat IME)
6. Clipboard changes → VoiceSync activates the remote window → Ctrl+V

**Technical details:**
- Uses `AttachThreadInput` + `SetForegroundWindow` to reliably switch windows
- Alt key simulation bypasses Windows foreground window restrictions
- Works even when target window is minimized
```

- [ ] **Step 4: Commit all changes**

```bash
git add CLAUDE.md
git commit -m "docs: add voice mode documentation"
```

- [ ] **Step 5: Create final tag**

```bash
git tag -a v0.2-voice-mode -m "Voice mode feature: switch to local window for voice input, auto-paste to remote"
git push origin main --tags
```

---

## Summary

| Chunk | Task | Files Modified | Commits |
|-------|------|----------------|---------|
| 1 | NativeMethods APIs | NativeMethods.cs | 1 |
| 2 | WindowDetector handle | WindowDetector.cs | 1 |
| 3 | InputSender window paste | InputSender.cs | 1 |
| 4 | SyncEngine voice mode | SyncEngine.cs | 1 |
| 5 | Unit tests | SyncEngineTests.cs | 1 |
| 6 | TrayIconApp UI | TrayIconApp.cs | 1 |
| 7 | Final integration | CLAUDE.md, publish | 2 |

**Total: 8 commits, 6 files modified, 1 file created (plan)**