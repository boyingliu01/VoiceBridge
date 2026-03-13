# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VoiceSync is a Windows system tray application (.NET 8, WinForms) that monitors clipboard changes and automatically pastes text into active remote desktop windows (RDP via `mstsc`, or Sunflower/向日葵 remote control). Use case: voice input typed locally auto-syncs to remote machines.

## Commands

```bash
# One-click setup (generate icon + build + test)
setup.bat

# Build
dotnet build

# Run tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run a single test class
dotnet test --filter "ClassName=WindowDetectorTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~OnClipboardChanged_Rdp_PastesText"

# Run application
dotnet run --project src/VoiceSync

# Publish self-contained single .exe
dotnet publish src/VoiceSync -c Release -o publish/

# Create GitHub release
gh release create v1.0 publish/VoiceSync.exe --title "v1.0" --notes "..."
```

## Architecture

```
ClipboardWatcher → SyncEngine → InputSender
                       ↑
                  WindowDetector
```

| Component | Responsibility | Key Details |
|-----------|----------------|-------------|
| `ClipboardWatcher` | Listen for clipboard changes | `AddClipboardFormatListener`, fires `Changed` event |
| `WindowDetector` | Detect remote window type | `Classify()` is pure static (testable), `Detect()` calls Win32 |
| `SyncEngine` | Core logic with state | Deduplication, delays, voice mode state |
| `InputSender` | Keyboard input simulation | `SendCtrlV()`, `SendCtrlVToWindow()`, `ResetKeyboardState()` |
| `TrayIconApp` | UI and orchestration | Tray icon, context menu, autostart registry |
| `NativeMethods` | P/Invoke declarations | All Win32 API in one place |

## Voice Mode

Solves the "focus hijacking" problem when using voice input with remote desktop:

1. User right-clicks tray icon → "语音模式"
2. 5-second countdown begins (MessageBox confirmation)
3. User switches to remote window within 5 seconds
4. System captures the remote window handle
5. User switches to any local window, uses voice input
6. Clipboard changes → VoiceSync activates remote window → Ctrl+V

**Window activation technique:**
- `AttachThreadInput` + `SetForegroundWindow` for reliable switching
- Alt key simulation bypasses Windows foreground restrictions
- Works even when target window is minimized

**Keyboard state safety:**
- `keybd_event` flags must match: `KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP` for Alt release
- `ResetKeyboardState()` cleans up Ctrl/Alt/Shift on exit
- Prevents stuck modifier keys

## Testing

Tests use xUnit + Moq in `tests/VoiceSync.Tests/`.

**Testability setup:**
- `WindowDetector.Detect()` is `virtual` for mocking
- `InternalsVisibleTo` for `VoiceSync.Tests` and `DynamicProxyGenAssembly2` (Moq)
- Set `RdpDelayMs = 0` / `SunflowerDelayMs = 0` to avoid delays in tests

**Coverage (v1.0):**
- Overall: 25%
- `SyncEngine`: 97.3%
- `InputSender`: 41.9%

## C# Conventions

- File-scope namespaces with path comment: `// src/VoiceSync/ClassName.cs`
- Primary constructors for DI: `class SyncEngine(WindowDetector detector, Action<string> pasteAction)`
- Private fields: `_camelCase`. Properties/methods/types: `PascalCase`
- `StringComparison.OrdinalIgnoreCase` over `.ToLower()`
- All P/Invoke in `NativeMethods.cs`; close handles in `finally` blocks

## Key Constraints

- Windows-only (`net8.0-windows`, WinForms, P/Invoke)
- Self-contained single `.exe` (no .NET runtime required)
- `AllowUnsafeBlocks` disabled — use `[StructLayout]` marshaling
- Paste delay re-check is intentional — prevents pasting if user switched windows during delay