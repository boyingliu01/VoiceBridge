# AGENTS.md

This file provides guidance to AI agents working in this VoiceSync repository.

## Project Overview

VoiceSync is a Windows system tray application (.NET 8, WinForms) that monitors clipboard changes and automatically pastes text into active remote desktop windows (RDP via `mstsc`, or Sunflower/向日葵 remote control). Use case: voice input typed locally auto-syncs to remote machines.

### Architecture

```
ClipboardWatcher → SyncEngine → InputSender
                       ↑
                  WindowDetector
```

**Components:**
- **`ClipboardWatcher`** — Extends `NativeWindow` to receive `WM_CLIPBOARDUPDATE` via `AddClipboardFormatListener`. Fires `Changed` event. No polling.
- **`WindowDetector`** — `Classify(processName)` is a pure static function (testable without Win32), `Detect()` calls Win32 to get foreground process then classifies. Returns `RemoteType` enum: `None | Rdp | Sunflower`.
- **`SyncEngine`** — Stateful core logic. Holds `_lastClip` for deduplication, checks `IsEnabled`, applies per-type delay (`RdpDelayMs=150`, `SunflowerDelayMs=900`), re-confirms remote window still foreground before calling `pasteAction`. `WindowDetector` injected for Moq testability.
- **`InputSender`** — Static `SendCtrlV()` using `SendInput` with 4 `INPUT` structs (Ctrl↓, V↓, V↑, Ctrl↑).
- **`TrayIconApp`** — `ApplicationContext` subclass. Manages tray icon, context menu (pause/resume, autostart, exit), and autostart registry (`HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`).
- **`NativeMethods`** — All P/Invoke declarations for user32.dll/kernel32.dll.
- **`Program.cs`** — Single-instance mutex (`VoiceSync_SingleInstance`) before launching `TrayIconApp`.

## Build/Test Commands

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter "ClassName=WindowDetectorTests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~MethodName"

# Run the application
dotnet run --project src/VoiceSync

# Publish self-contained single .exe
dotnet publish src/VoiceSync -c Release -o publish/
```

## Code Style Guidelines

### File Organization

- Use file-scope namespaces (`namespace VoiceSync;` at top of file)
- Add file-relative path comment on line 1: `// src/VoiceSync/ClassName.cs`
- Group related classes logically in the same namespace
- Test files mirror source structure under `tests/VoiceSync.Tests/`

### Naming Conventions

- **Classes**: `PascalCase` - `SyncEngine`, `WindowDetector`
- **Methods**: `PascalCase` - `OnClipboardChanged`, `GetForegroundProcessName`
- **Properties**: `PascalCase` - `IsEnabled`, `RdpDelayMs`
- **Fields**: `_camelCase` with underscore prefix - `_lastClip`
- **Enums**: `PascalCase` - `RemoteType`
- **Enum members**: `PascalCase` - `None`, `Rdp`, `Sunflower`
- **Constants**: `PascalCase` (public) or `_camelCase` (private)
- **Parameters**: `camelCase` - `processName`, `text`
- **Local variables**: `camelCase` - `hwnd`, `hProc`

### C# Language Features

- Use **primary constructors** for dependency injection: `class SyncEngine(WindowDetector detector, Action<string> pasteAction)`
- Use **nullable reference types** (`<Nullable>enable`) - mark nullable parameters with `string?`
- Use **string comparison helpers**: `StringComparison.OrdinalIgnoreCase` instead of `.ToLower()`
- Use **null-conditional operators**: `Changed?.Invoke(this, EventArgs.Empty)`
- Use **collection expressions**: `["item1", "item2"]` instead of `new[] { "item1", "item2" }`
- Use **pattern matching**: `if (processName is null) return RemoteType.None;`
- Use **async/await** for I/O operations with proper `Task` return types

### Class Design

- **Single responsibility**: Each class has one clear purpose
- **Dependency injection**: Pass dependencies via constructor (enables testing)
- **Separation of concerns**: Split Win32-dependent code (testable logic) from pure functions
- **Use static classes** for stateless utilities: `NativeMethods`, `InputSender`
- **Use sealed classes** when inheritance is not expected: `ClipboardWatcher`
- **Implement IDisposable** for native resource cleanup with `finally` blocks

### P/Invoke Patterns

- Declare all Win32 functions in `NativeMethods` static class
- Use `[DllImport("user32.dll", SetLastError = true)]` for error handling
- Use `[StructLayout]` for structs with explicit byte layout
- Use `Marshal.SizeOf<T>()` for struct size calculation
- Group related P/Invoke declarations with section comments
- Close handles immediately after use in `finally` blocks

### Testing Patterns

- Use **xUnit** with `[Fact]` for single scenarios and `[Theory]` with `[InlineData]` for data-driven tests
- Use **Moq** to mock dependencies: `Mock<WindowDetector>`
- **Test pure functions** directly: `WindowDetector.Classify("mstsc")`
- **Mock Win32-dependent code**: Mock `WindowDetector.Detect()` in `SyncEngineTests`
- Use **constructor helpers** for test setup: `CreateEngine()` method
- Test all public method behaviors including edge cases (null input, empty strings, disabled state)

### XML Documentation

- Add `<summary>` tags for public/internal types and members
- Keep docs concise and focused on purpose, not implementation details
- Document non-obvious behavior: `/// <summary>纯函数，便于单元测试</summary>`

### Error Handling

- Use `try/finally` for resource cleanup (never leave handles open)
- Return `null` or default values for non-critical failures (e.g., `GetForegroundProcessName()` returning `null`)
- Use `StringBuilder` with fixed capacity for Win32 string buffers
- Check for zero handles/null values immediately after Win32 calls

### Architecture Patterns

- Pipeline design: `ClipboardWatcher → SyncEngine → InputSender` (with `WindowDetector` injected)
- Event-driven updates: `ClipboardWatcher.Changed` event triggers flow
- Deduplication: Track `_lastClip` to avoid redundant paste operations
- Per-type configuration: Different delays for RDP (150ms) vs Sunflower (900ms)
- Re-verification: Confirm remote window still foreground after delay before pasting

### Key Constraints

- **Windows-only**: `net8.0-windows`, `UseWindowsForms` - no cross-platform abstractions
- **No unsafe code**: `AllowUnsafeBlocks=false` - use `[StructLayout]` marshaling instead
- **Single-instance**: Uses mutex in `Program.cs` to prevent multiple app instances
- **Self-contained publish**: Single `.exe` deployment without .NET runtime requirement
- **Re-verification after delay**: The second `detector.Detect()` call after delay is intentional — prevents pasting if user switched away from remote window during delay period
