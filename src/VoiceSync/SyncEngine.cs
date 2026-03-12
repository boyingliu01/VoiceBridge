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
}