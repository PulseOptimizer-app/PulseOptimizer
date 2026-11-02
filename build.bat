@echo off
setlocal
cd /d "%~dp0"
title Building Pulse Optimizer

rem ---- 1. Make sure the .NET 8 SDK is installed ----
set "DOTNET=dotnet"
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
"%DOTNET%" --list-sdks 2>nul | findstr /r "^[89]\. ^1[0-9]\." >nul
if errorlevel 1 (
  echo Installing the .NET 8 SDK with winget...
  winget install -e --id Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements
  if errorlevel 1 goto :fail
  set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
)

rem ---- 2. Build a single self-contained exe ----
echo Building...
"%DOTNET%" publish src\PulseOptimizer\PulseOptimizer.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
if errorlevel 1 goto :fail

rem ---- 3. Bundle PresentMon for frame-time measurement ----
if not exist "dist\tools\PresentMon.exe" (
  echo Downloading PresentMon ^(Intel, MIT license^)...
  powershell -NoProfile -ExecutionPolicy Bypass -File "tools\get-presentmon.ps1" -Out "dist\tools\PresentMon.exe"
  if errorlevel 1 echo PresentMon download failed. Frame-time measurement will be unavailable until it's in dist\tools.
)

echo.
echo Done: dist\PulseOptimizer.exe
pause
exit /b 0

:fail
echo.
echo Build failed. Scroll up for the first error.
pause
exit /b 1
