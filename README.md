# VoiceSync

<p align="center">
  <strong>远程桌面语音输入助手</strong><br>
  <sub>让语音输入跨越本地与远程桌面的边界</sub>
</p>

---

## 它解决什么问题？

当你使用向日葵、RDP 等远程桌面控制另一台电脑时，语音输入会遇到一个棘手的问题：

**问题场景**：
- 你想在电脑 B（远程）输入文字
- 你切换到向日葵窗口，焦点在远程桌面
- 你按下语音输入快捷键（如微信输入法的 Ctrl+Win）
- **问题来了**：快捷键被发送到了远程电脑 B，而不是触发本地电脑 A 的语音输入

**VoiceSync 的解决方案**：
1. 在电脑 A 运行 VoiceSync
2. 开启"语音模式"，锁定远程窗口
3. 切回本地任意窗口，使用语音输入
4. 语音识别结果自动同步到远程电脑 B

---

## 快速开始

### 1. 下载安装

从 [Releases](../../releases) 页面下载 `VoiceSync.exe`，双击运行即可。

> 无需安装 .NET Runtime，程序已打包为独立可执行文件。

### 2. 基础使用（正常模式）

```
运行 VoiceSync → 在本地复制文字 → 切换到远程窗口 → 自动粘贴
```

适合场景：手动复制文本，需要粘贴到远程桌面。

### 3. 语音模式

```
右键托盘图标 → 语音模式 → 5秒内切换到远程窗口 → 确认 → 切回本地使用语音输入
```

适合场景：使用语音输入，结果需要发送到远程桌面。

---

## 功能详解

### 正常模式

| 操作 | 结果 |
|------|------|
| 本地 Ctrl+C 复制 | 监听到剪贴板变化 |
| 切换到远程窗口 | 自动执行 Ctrl+V |

**支持延迟**：RDP 150ms，向日葵 900ms（可根据网络延迟自动调整）

### 语音模式

**使用流程**：

```
┌─────────────────────────────────────────────────────────┐
│  1. 右键托盘图标 → 点击"语音模式"                          │
│  2. 弹出提示框 → 点击"确定"                               │
│  3. 【5秒内】切换到远程桌面窗口（向日葵/RDP）               │
│  4. 程序自动捕获窗口，显示确认信息                          │
│  5. 切换到本地任意窗口，使用语音输入                        │
│  6. 语音识别结果自动复制到剪贴板 → 自动粘贴到远程窗口        │
└─────────────────────────────────────────────────────────┘
```

**关闭语音模式**：再次点击菜单中的"语音模式"，取消勾选即可。

---

## 托盘菜单

| 菜单项 | 功能 |
|--------|------|
| **语音模式** | 开启/关闭语音模式（勾选表示已开启） |
| **暂停同步** | 临时停止自动粘贴功能 |
| **开机自启** | 设置开机自动启动 |
| **退出** | 关闭程序 |

**托盘图标状态**：
- `VoiceSync ● 运行中` - 正常工作
- `VoiceSync 🎤 语音模式` - 语音模式已开启
- `VoiceSync ○ 已暂停` - 同步已暂停

---

## 支持的远程软件

| 软件 | 进程名 | 默认延迟 |
|------|--------|----------|
| Windows RDP | `mstsc` | 150ms |
| 向日葵远程控制 | `AweSun` | 900ms |
| 向日葵（旧版） | `SunloginClient`, `Sunlogin` | 900ms |
| RustDesk | `RustDesk` | 900ms |

> 延迟说明：RDP 响应快，150ms 足够；向日葵等第三方远程软件有额外网络延迟，需要更长时间。

---

## 向日葵配置

### 必须开启剪贴板同步

在向日葵客户端设置中：
```
远程控制设置 → 常规设置 → 勾选"自动同步剪贴板文本"
```

### 常见问题

**Q: 语音模式检测不到向日葵窗口？**

A: 请确认向日葵进程名。打开任务管理器 → 详细信息 → 查找向日葵相关进程。如果进程名不在支持列表中，可以修改 `WindowDetector.cs` 中的 `SunflowerNames` 数组。

**Q: 粘贴延迟太长/太短？**

A: 可以在 `SyncEngine.cs` 中调整延迟时间：
```csharp
public int RdpDelayMs { get; set; } = 150;
public int SunflowerDelayMs { get; set; } = 900;
```

---

## 构建方法

### 环境要求
- .NET 8 SDK
- Windows 10/11

### 一键构建
```bash
setup.bat
```

### 手动构建
```bash
# 编译
dotnet build

# 运行测试
dotnet test

# 发布独立 exe（约 150MB）
dotnet publish src/VoiceSync -c Release -o publish/ --self-contained true -r win-x64
```

---

## 技术架构

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│ ClipboardWatcher │────▶│   SyncEngine     │────▶│   InputSender    │
│                  │     │                  │     │                  │
│ 监听剪贴板变化    │     │ - 延迟控制       │     │ - 模拟 Ctrl+V    │
│ WM_CLIPBOARDUPDA │     │ - 去重           │     │ - 窗口激活       │
└──────────────────┘     │ - 语音模式       │     └──────────────────┘
                         └────────┬─────────┘
                                  │
                         ┌────────▼─────────┐
                         │  WindowDetector  │
                         │                  │
                         │ - 检测远程类型   │
                         │ - 获取窗口信息   │
                         └──────────────────┘
```

### 核心组件

| 组件 | 职责 | 关键技术 |
|------|------|----------|
| `ClipboardWatcher` | 监听剪贴板变化 | `AddClipboardFormatListener` |
| `WindowDetector` | 检测远程窗口类型 | `GetForegroundWindow`, `QueryFullProcessImageName` |
| `SyncEngine` | 核心业务逻辑 | 延迟、去重、状态管理 |
| `InputSender` | 键盘输入模拟 | `SendInput`, `AttachThreadInput` |
| `TrayIconApp` | 托盘图标 UI | WinForms `NotifyIcon` |

---

## 项目结构

```
VoiceSync/
├── src/VoiceSync/           # 主程序
│   ├── Program.cs           # 入口点，单实例检查
│   ├── TrayIconApp.cs       # 托盘图标应用
│   ├── ClipboardWatcher.cs  # 剪贴板监听
│   ├── WindowDetector.cs    # 远程窗口检测
│   ├── SyncEngine.cs        # 核心逻辑
│   ├── InputSender.cs       # 键盘输入
│   └── NativeMethods.cs     # P/Invoke 声明
├── tests/VoiceSync.Tests/   # 单元测试
├── tools/GenerateIcon/      # 图标生成工具
├── publish/                 # 发布输出
└── docs/
    └── QA_REPORT.md         # 质量保证报告
```

---

## 许可证

MIT License

---

## 贡献

欢迎提交 Issue 和 Pull Request！

### 开发环境设置
```bash
git clone https://github.com/your-username/VoiceSync.git
cd VoiceSync
dotnet restore
dotnet test
```

### 代码规范
- 文件作用域命名空间
- Primary Constructor 依赖注入
- 私有字段：`_camelCase`
- P/Invoke 声明统一放在 `NativeMethods.cs`