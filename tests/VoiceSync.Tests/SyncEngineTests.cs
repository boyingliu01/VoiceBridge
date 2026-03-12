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
}