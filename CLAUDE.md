# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VoiceSync is a Windows system tray application (.NET 8, WinForms) that monitors clipboard changes and automatically pastes text into active remote desktop windows (RDP via `mstsc`, or Sunflower/向日葵 remote control). Use case: voice input typed locally auto-syncs to remote machines.

## Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run a single test class
dotnet test --filter "ClassName=WindowDetectorTests"

# Run application
dotnet run --project src/VoiceSync

# Publish self-contained single .exe
dotnet publish src/VoiceSync -c Release -o publish/
```

## Architecture

The app is structured as a pipeline of single-responsibility components:

```
ClipboardWatcher → SyncEngine → InputSender
                       ↑
                  WindowDetector
```

- **`ClipboardWatcher`** — Extends `NativeWindow` to receive `WM_CLIPBOARDUPDATE` via `AddClipboardFormatListener`. Fires `Changed` event. No polling.
- **`WindowDetector`** — Two-part design: `Classify(processName)` is a pure static function (testable without Win32), and `Detect()` calls Win32 to get the foreground process name then classifies it. Returns `RemoteType` enum: `None | Rdp | Sunflower`.
- **`SyncEngine`** — Stateful core logic. Holds `_lastClip` to deduplicate, checks `IsEnabled`, applies per-remote-type delay (`RdpDelayMs=150`, `SunflowerDelayMs=900`), then re-confirms the remote window is still foreground before calling `pasteAction`. `WindowDetector` is injected via constructor for Moq testability.
- **`InputSender`** — Static `SendCtrlV()` using `SendInput` with 4 `INPUT` structs (Ctrl↓, V↓, V↑, Ctrl↑).
- **`TrayIconApp`** — `ApplicationContext` subclass wiring everything together. Manages tray icon, context menu (pause/resume, autostart, exit), and autostart registry (`HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`).
- **`NativeMethods`** — All P/Invoke declarations for user32.dll/kernel32.dll in one place.
- **`Program.cs`** — Single-instance mutex (`VoiceSync_SingleInstance`) before launching `TrayIconApp`.

## Testing Approach

Tests live in `tests/VoiceSync.Tests/` using xUnit + Moq. `WindowDetector.Classify` is tested via `[Theory]` inline data. `SyncEngine` tests mock `WindowDetector` to control `Detect()` return values without Win32 calls.

## Key Constraints

- Windows-only (`net8.0-windows`, WinForms, P/Invoke). No cross-platform abstractions needed.
- Published as a self-contained single `.exe` (no .NET runtime required on target machine).
- `AllowUnsafeBlocks` is disabled — P/Invoke structs use `[StructLayout]` with marshaling, not raw pointers.
- The paste delay re-check (second `detector.Detect()` call after the delay) is intentional — prevents pasting if the user switched away from the remote window during the delay period.
