@echo off
echo ============================================================
echo  Step 1: Generate tray icon
echo ============================================================
cd /d "%~dp0"
dotnet run --project tools\GenerateIcon
if errorlevel 1 ( echo ICON GENERATION FAILED & pause & exit /b 1 )

echo.
echo ============================================================
echo  Step 2: Build solution
echo ============================================================
dotnet build
if errorlevel 1 ( echo BUILD FAILED & pause & exit /b 1 )

echo.
echo ============================================================
echo  Step 3: Run tests
echo ============================================================
dotnet test
if errorlevel 1 ( echo TESTS FAILED & pause & exit /b 1 )

echo.
echo All done!
pause
