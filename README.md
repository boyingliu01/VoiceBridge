# VoiceSync

Windows 系统托盘应用，自动将本地剪贴板内容同步到远程桌面窗口。

## 功能特性

### 正常模式
- 监听本地剪贴板变化
- 自动检测当前前台窗口是否为远程桌面
- 是远程窗口时，自动执行 Ctrl+V 粘贴
- 支持 RDP（mstsc）和向日葵远程控制

### 语音模式
- 解决语音输入时的"焦点劫持"问题
- 记录远程窗口，后续剪贴板变化直接粘贴到该窗口
- 用户可以在本地任意窗口使用语音输入，结果自动同步到远程

## 安装方法

### 方法一：下载预编译版本
从 GitHub Releases 页面下载最新的 `VoiceSync.exe`。

### 方法二：自己构建
```bash
dotnet publish src/VoiceSync -c Release -o publish/
```

## 使用方法

### 正常模式
1. 运行 VoiceSync，程序会在系统托盘显示图标
2. 在本地复制文字（Ctrl+C）
3. 切换到远程桌面窗口，文字会自动粘贴进去

### 语音模式
语音模式用于解决使用微信等应用的语音输入时，焦点被劫持到本地输入法窗口的问题。

**启用步骤：**
1. 确保已连接到远程桌面（ RDP 或向日葵）
2. 切换到远程桌面窗口，让它成为前台窗口
3. 右键点击托盘图标，选择"语音模式"
4. 切换到本地窗口，使用语音输入（如微信输入法）
5. 语音识别结果会自动复制到剪贴板，然后粘贴到远程窗口

**关闭语音模式：**
再次点击托盘图标菜单中的"语音模式"即可关闭。

### 托盘菜单功能
- **语音模式**：开启/关闭语音模式
- **暂停同步**：临时停止同步功能
- **开机自启**：开机自动启动程序
- **退出**：关闭程序

## 支持的远程软件

| 软件 | 进程名 | 延迟设置 |
|------|--------|----------|
| RDP (mstsc) | `mstsc` | 150ms |
| 向日葵远程控制 | `AweSun` / `SunloginClient` / `Sunlogin` | 900ms |
| RustDesk | `RustDesk` | 900ms |

## 向日葵配置说明

### 1. 开启剪贴板共享
在向日葵客户端设置中，确保已开启"剪贴板同步"功能：
- 远程控制设置 → 常规设置 → 勾选"自动同步剪贴板文本"

### 2. 确认进程名
如果同步不生效，可能是进程名不匹配。请按以下步骤确认：

1. 打开任务管理器（Ctrl+Shift+Esc）
2. 点击"详细信息"
3. 找到向日葵相关进程，记录进程名（不含 .exe 后缀）
4. 如需自定义进程名，可修改 `WindowDetector.cs` 中的 `SunflowerNames` 数组

## 构建方法

### 一键构建
```bash
setup.bat
```
这会依次执行：生成图标 → 编译项目 → 运行测试。

### 手动命令
```bash
# 编译
dotnet build

# 运行测试
dotnet test

# 发布为独立 exe
dotnet publish src/VoiceSync -c Release -o publish/
```

## 技术栈

- .NET 8
- WinForms
- P/Invoke (user32.dll, kernel32.dll)
- xUnit + Moq（单元测试）

## 工作原理

```
ClipboardWatcher → SyncEngine → InputSender
                       ↑
                  WindowDetector
```

- **ClipboardWatcher**：通过 `AddClipboardFormatListener` 监听剪贴板变化
- **WindowDetector**：检测当前前台窗口是否为远程桌面
- **SyncEngine**：核心逻辑，控制延迟和粘贴时机
- **InputSender**：通过 `SendInput` 模拟 Ctrl+V 按键