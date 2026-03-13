// tests/VoiceSync.Tests/InputSenderTests.cs
using VoiceSync;
using Xunit;

namespace VoiceSync.Tests;

/// <summary>
/// InputSender 的测试。
/// 注意：由于涉及 Win32 API，这些是集成测试而非单元测试。
/// 主要验证方法调用不会崩溃，而非验证具体的键盘输入。
/// </summary>
public class InputSenderTests
{
    [Fact]
    public void ResetKeyboardState_DoesNotThrow()
    {
        // 这个测试验证 ResetKeyboardState 可以安全调用
        // 它不会实际验证键盘状态，但确保没有异常
        var exception = Record.Exception(() => InputSender.ResetKeyboardState());
        Assert.Null(exception);
    }

    [Fact]
    public void SendCtrlV_DoesNotThrow()
    {
        // 这个测试验证 SendCtrlV 可以安全调用
        var exception = Record.Exception(() => InputSender.SendCtrlV());
        Assert.Null(exception);
    }

    [Fact]
    public void SendCtrlVToWindow_WithZeroHandle_ReturnsWithoutError()
    {
        // 验证传入零句柄时不会崩溃
        var exception = Record.Exception(() => InputSender.SendCtrlVToWindow(IntPtr.Zero));
        Assert.Null(exception);
    }

    [Fact]
    public void SendCtrlVToWindow_WithInvalidHandle_ReturnsWithoutError()
    {
        // 使用一个无效的窗口句柄（大概率不是真实窗口）
        // IsWindow 应该返回 false，方法应该提前返回
        var fakeHwnd = new IntPtr(0xDEADBEEF);
        var exception = Record.Exception(() => InputSender.SendCtrlVToWindow(fakeHwnd));
        Assert.Null(exception);
    }
}