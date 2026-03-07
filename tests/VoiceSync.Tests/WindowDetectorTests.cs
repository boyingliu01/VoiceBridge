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
