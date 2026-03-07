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
