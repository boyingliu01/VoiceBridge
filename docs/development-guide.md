# VoiceBridge 开发指南

本文档记录 VoiceBridge 项目开发过程中的关键问题、解决方案和经验教训。

## 项目概述

VoiceBridge 是一个 Windows 系统托盘应用，用于将本地剪贴板内容同步到远程桌面窗口（RDP 或向日葵）。

**技术栈**: .NET 8, WinForms, P/Invoke (user32.dll, kernel32. dll)
**目标平台**: Windows 10/11 (x64)

---

## 关键问题与解决方案

### 1. 窗口切换时触发 Office 365

**问题现象**:
- 使用语音模式时，切换窗口会意外打开 Microsoft 365 网站
- 按 Alt+Enter 会触发全屏
- 按 Win+Alt 可能触发 Office 快捷键

**根本原因**:
- 原始实现使用 `keybd_event` 模拟 Alt 键
- Alt 键模拟可能与系统其他按键（Win、Shift、Ctrl）形成组合键
- Win+Alt 组合被 Windows 识别为 Office 365 打开快捷键

**最终解决方案**:
```csharp
// 完全移除 Alt 键模拟
// 使用纯 Windows API: ShowWindow + SetForegroundWindow + SetFocus
// 添加窗口激活验证和备用方案（最小化再恢复）
```

**关键代码** (`src/VoiceBridge/InputSender.cs`):
```csharp
public static void SendCtrlVToWindow(IntPtr targetHwnd)
{
    // 先释放所有修饰键，避免意外组合键
    NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0,
        NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    // ... (Ctrl, Alt, Shift, Win 键)

    // 方法：ShowWindow 再 SetForegroundWindow 再 SetFocus
    NativeMethods.ShowWindow(targetHwnd, NativeMethods.SW_SHOW);
    NativeMethods.SetForegroundWindow(targetHwnd);
    NativeMethods.SetFocus(targetHwnd);

    // 等待并验证窗口是否已激活
    var activated = NativeMethods.GetForegroundWindow();
    if (activated != targetHwnd)
    {
        // 备用方案：最小化再恢复
        ForceActivateWindow(targetHwnd);
    }
}
```

**经验教训**:
- ❌ 不要假设问题简单就跳过系统性调试
- ✅ 模拟键盘操作风险高，应优先使用纯 API
- ✅ 窗口激活是 Windows 系统敏感操作，需要充分的测试
- ✅ 遵免系统快捷键的关键：不要模拟可能组合键

---

### 2. 语音模式激活焦点问题

**问题现象**:
- 右键托盘图标会抢走焦点
- `GetForegroundWindow()` 在菜单关闭前无法获取到正确的窗口

**解决方案**:
```csharp
private void OnVoiceModeClick(object? sender, EventArgs e)
{
    if (_engine.VoiceModeEnabled)
    {
        // 关闭语音模式...
        return;
    }

    // 显示倒计时提示，让用户有时间切换到目标窗口
    // 使用 MessageBox 确保用户能看到提示
    var result = MessageBox.Show(
        "点击确定后，请在 5 秒内切换到目标窗口...",
        "开启语音模式",
        MessageBoxButtons.OKCancel,
        MessageBoxIcon.Information);

    if (result != DialogResult.OK) return;

    // 5秒后检测前台窗口
    var timer = new System.Windows.Forms.Timer { Interval = 5000 };
    timer.Tick += (_, _) =>
    {
        timer.Dispose();
        StartVoiceModeWithConfirmation();
    };
    timer.Start();
}
```

**关键设计**:
- 使用 MessageBox 而非模态对话框
- 5 秒倒计时给用户充足时间
- 延迟检测确保菜单已关闭

---

### 3. 向日葵进程名称不匹配

**问题现象**:
- 语音模式检测不到向日葵远程窗口
- 用户报告"请先将焦点切换到远程桌面窗口"错误

**根本原因**:
- 向日葵各版本的进程名称不统一
- 旧代码只支持 "SunloginClient" 和 "Sunlogin"
- 实际新版向日葵主进程是 "AweSun"

**解决方案** (`src/VoiceBridge/WindowDetector.cs`):
```csharp
private static readonly string[] SunflowerNames =
[
    "AweSun",           // 向日葵远程控制主窗口（用户确认）
    "SunloginClient",   // 旧版本
    "Sunlogin",         // 旧版本
    "向日葵远程控制",    // 中文版本
    "RustDesk",         // 备用：开源替代品
];
```

**扩展性设计**:
- 使用数组支持多个进程名称
- 新增进程名只需添加到数组，无需修改逻辑
- 支持国际化（中文版名称）

---

### 4. Alt 键释放标志不匹配导致键盘状态卡住

**问题现象**:
- 程序退出后，修饰键（Ctrl、Alt、Shift）仍然处于按下状态
- 用户按任何键都会触发意外的快捷键
- 需要重启电脑才能恢复正常

**根本原因**:
- 按下 Alt 键（释放）没有使用 `KEYEVENTF_EXTENDEDKEY` 标志
- 只使用了 `KEYEVENTF_KEYUP` 标志
- Alt 键的按下和释放标志不匹配，导致状态异常

**解决方案** (`src/VoiceBridge/InputSender.cs`):
```csharp
// 正确释放 Alt 键（必须同时包含两个标志！）
NativeMethods.keybd_event(NativeMethods.VK_MENU, 0,
    NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
```

**清理机制**:
```csharp
public static void ResetKeyboardState()
{
    // 程序退出时清理所有修饰键
    NativeMethods.keybd_event(NativeMethods.VK_CONTROL, 0,
        NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    NativeMethods.keybd_event(NativeMethods.VK_MENU, 0,
        NativeMethods.KEYEVENTF_EXTENDEDKEY | NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
    NativeMethods.keybd_event(NativeMethods.VK_SHIFT, 0,
        NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);
}
```

---

## 开发环境设置

### 必需工具

```bash
# .NET 8 SDK
dotnet --version

# 推荐编辑器
# Visual Studio Code 或 Rider
```

### 项目结构

```
VoiceBridge/
├── src/VoiceBridge/
│   ├── Program.cs           # 入口点
│   ├── TrayIconApp.cs       # 托盘图标和主界面
│   ├── ClipboardWatcher.cs  # 剪贴板监听
│   ├── WindowDetector.cs    # 窗口检测
│   ├── SyncEngine.cs        # 核心业务逻辑
│   ├── InputSender.cs       # 键盘输入模拟
│   └── NativeMethods.cs     # Win32 API 声明
├── tests/VoiceBridge.Tests/
│   ├── WindowDetectorTests.cs
│   ├── SyncEngineTests.cs
│   └── InputSenderTests.cs
├── tools/
│   └── GenerateIcon/      # 图标生成工具
```

---

## 测试策略

### 单元测试

项目使用 xUnit + Moq 进行单元测试。

**测试覆盖重点**:
- `SyncEngine`: 核心业务逻辑（覆盖率 97.3%）
- `InputSender`: 集成 API 调用（覆盖率 41.9%）
- `WindowDetector`: 进程名分类逻辑（覆盖率 30.9%）

**运行测试**:
```bash
# 运行所有测试
dotnet test

# 运行单个测试类
dotnet test --filter "ClassName=WindowDetectorTests"

# 运行单个测试方法
dotnet test --filter "FullyQualifiedName~OnClipboardChanged_Rdp_PastesText"

# 运行测试并生成覆盖率报告
dotnet test --collect:"XPlat Code Coverage"
```

### 端到端测试清单

在实际远程环境（向日葵或 RDP）中验证：

- [ ] 正常模式剪贴板同步
- [ ] 语音模式开启和关闭
- [ ] 窗口切换到远程窗口
- [ ] 语音识别结果自动粘贴
- [ ] 窗口切换时不触发 Office 365
- [ ] 程序退出后键盘状态正常
- [ ] 长时间运行不内存泄漏
- [ ] 多次切换窗口不崩溃

---

## 发布流程

### 版本管理

项目采用语义化版本号：`v1.0`, `v1.1`, `v1.2`, `v1.3`

**版本类型**:
- **Patch 版本** (`v1.x`): 小型修复，向后兼容
- **Minor 版本** (`v1.x`): 新功能
- **Major 版本** (`v1.0`): 重大更新或不兼容变更

### 版本历史

| 版本 | 主要更新 | 发布日期 |
|------|----------|----------|
| v1.0 | 初始版本，语音模式和键盘状态修复 | 2026-03-13 |
| v1.1 | 重命名：VoiceSync → VoiceBridge | 2026-03-14 |
| v1.2 | 轻量版（179KB）+ 窗口切换修复 | 2026-03-15 |

### 发布检查清单

- [ ] 所有测试通过（23/23）
- [ ] 编译无警告、无错误
- [ ] 功能完整性验证
- [ ] 文档更新（README、CLAUDE.md）
- [ ] 两个版本文件上传（轻量版 179KB + 完整版 147MB）
- [ ] GitHub Release Notes 清晰描述
- [ ] 代码已推送到远端

---

## 常见配置

### VoiceBridge.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <AssemblyName>VoiceBridge</AssemblyName>
    <RootNamespace>VoiceBridge</RootNamespace>
  </PropertyGroup>
</Project>
```

**发布配置对比**:

| 配置项 | 完整版 | 轻量版 |
|---------|--------|--------|
| `SelfContained` | `true` | `false` |
| `PublishSingleFile` | `true` | `true` |
| `RuntimeIdentifier` | `win-x64` | `win-x64` |
| `PublishReadyToRun` | `false` | `false` |

**体积对比**:
- 完整版：~147MB（自包含 .NET Runtime）
- 轻量版：179KB（需要 .NET 8 Runtime）
- 体积减少：~98.8%

---

## 性能优化

### 已实施的优化

1. **移除不必要的依赖**
   - 项目只依赖系统 DLL（user32.dll, kernel32.dll）
   - 无第三方 NuGet 包

2. **延迟配置合理**
   - RDP: 150ms（RDP 响应快）
   - 向日葵: 900ms（向日葵有额外网络延迟）

3. **内存管理**
   - 及时释放 Win32 句柄
   - `using` 语句确保资源清理
   - 单实例限制（防止多开）

4. **异步操作**
   - 使用 `async/await` 模式处理剪贴板事件
   - 避免阻塞 UI 线程

---

## 安全考虑

### 键盘输入安全

**潜在风险**:
- 模拟键盘事件可能触发系统快捷键
- 窗口激活可能影响用户工作流

**缓解措施**:
- ✅ 完全移除 Alt 键模拟，使用纯 API
- ✅ 添加窗口激活验证，失败时尝试备用方案
- ✅ 程序退出时清理所有修饰键状态
- ✅ 在用户操作前释放修饰键

### 进程权限

- 只读取必要系统信息（进程名、窗口标题）
- 不尝试提升权限（不需要）
- 只操作自己的窗口和剪贴板

---

## 未来改进方向

### 短期优化
- [ ] 考虑 Rust 重写以进一步减小体积
- [ ] 或使用 Native AOT 编译

### 功能增强
- [ ] 支持更多远程软件（AnyDesk、Parsec）
- [ ] 添加剪贴板历史功能
- [ ] 支持多窗口同时同步
- [ ] 添加自定义延迟设置

### 用户体验
- [ ] 添加托盘动画效果
- [ ] 添加声音提示
- [ ] 支持深色模式
- [ ] 添加多语言支持

### 可维护性
- [ ] 添加自动更新检查
- [ ] 添加错误日志记录
- [ ] 添加使用统计（匿名）

---

## 贡献指南

### 报告 Bug

如果发现新的问题：

1. **创建 Issue**
   - 使用清晰的标题和描述
   - 包含重现步骤
   - 添加环境信息（Windows 版本、.NET 版本）
   - 提供截图或日志

2. **提交修复**
   - 在提交消息中引用 Issue 编号
   - 遵循 "fix: issue#N - 简要修复的问题"
   - 确保测试通过

3. **推送修复**
   - 推送到 GitHub
   - 更新相关 Issue 状态

### 代码贡献

1. **遵循现有代码风格**
   - 文件作用域命名空间
   - Primary Constructor 依赖注入
   - 私有字段：`_camelCase`
   - 公共成员：`PascalCase`

2. **添加测试**
   - 为新功能编写测试
   - 确保测试覆盖率不降低

3. **更新文档**
   - 修改 README 时的功能
   - 更新 CLAUDE.md 的开发指南

---

## 联系方式

**作者**: Claude Code
**项目**: VoiceBridge
**最后更新**: 2026-03-16

---

## 相关资源

- [.NET WinForms 文档](https://learn.microsoft.com/en-us/dotnet/desktop/winforms)
- [Windows API 参考](https://docs.microsoft.com/en-us/windows/win32/)
- [xUnit 文档](https://xunit.net/)
- [P/Invoke 参考](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke/)
