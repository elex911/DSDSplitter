@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo   DSD Splitter - Windows Build Script
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 goto NO_DOTNET

echo [1/3] Publishing single-file win-x64 Release...
dotnet publish "src\DSDSplitter\DSDSplitter.csproj" -c Release -r win-x64 -o "dist\DSDSplitter"
if errorlevel 1 goto BUILD_FAIL

echo [2/3] Checking tools\ffmpeg.exe and tools\ffprobe.exe ...
if exist "tools\ffmpeg.exe" copy /Y "tools\ffmpeg.exe" "dist\DSDSplitter\" >nul
if exist "tools\ffprobe.exe" copy /Y "tools\ffprobe.exe" "dist\DSDSplitter\" >nul

echo [3/3] Done! Output folder: dist\DSDSplitter\
if not exist "dist\DSDSplitter\ffmpeg.exe" (
    echo.
    echo [NOTE] ffmpeg.exe / ffprobe.exe not found yet.
    echo Put Windows ffmpeg.exe and ffprobe.exe into dist\DSDSplitter\ then run DSDSplitter.exe.
)
echo.
pause
exit /b 0

:NO_DOTNET
echo [ERROR] .NET 8 SDK was not found in PATH.
echo Please install .NET 8 SDK first:
echo   https://dotnet.microsoft.com/download/dotnet/8.0
echo After installing, double-click build.bat again.
echo.
pause
exit /b 1

:BUILD_FAIL
echo [ERROR] Build failed. Check the messages above.
echo.
pause
exit /b 1
