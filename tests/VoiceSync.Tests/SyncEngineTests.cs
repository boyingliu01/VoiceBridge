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
