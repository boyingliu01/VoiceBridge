@echo off
echo ========================================
echo VoiceBridge 端到端测试脚本
echo ========================================
echo.

echo [步骤 1] 构建并发布轻量版...
dotnet build src/VoiceBridge -c Release
dotnet publish src/VoiceBridge -c Release -o publish-lite/ --self-contained false

echo.
echo [步骤 2] 检查文件大小...
for %%F in (publish-lite\VoiceBridge.exe) do echo 轻量版大小: %%~zF 字节 (约 %%~zFKB)

echo.
echo ========================================
echo 请手动测试以下场景：
echo ========================================
echo.
echo 1. 运行 publish-lite\VoiceBridge.exe
echo 2. 右键托盘图标 ^> 语音模式
echo 3. 在 5 秒内切换到向日葵/RDP 远程窗口
echo 4. 确认目标窗口被捕获
echo 5. 切回本地窗口，使用语音输入
echo 6. 验证文字是否自动粘贴到远程窗口
echo.
echo [关键验证] 窗口切换时是否触发 Office 365 或其他意外行为？
echo.
pause