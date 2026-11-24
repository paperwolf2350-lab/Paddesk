@echo off
setlocal
cd /d "%~dp0"
title Build PadDesk

if not exist "PadDesk.cs" (
    echo PadDesk.cs isn't in this folder. Put build.bat and PadDesk.cs in the same folder.
    pause
    exit /b 1
)

tasklist /fi "imagename eq PadDesk.exe" | find /i "PadDesk.exe" >nul
if not errorlevel 1 (
    echo PadDesk is running. Right-click its tray icon, choose Exit, then run this again.
    pause
    exit /b 1
)

rem The C# compiler that ships with Windows (.NET Framework 4.x).
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Couldn't find the .NET Framework C# compiler on this PC.
    pause
    exit /b 1
)

echo Building PadDesk.exe...
"%CSC%" /nologo /target:winexe /optimize+ /out:PadDesk.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll PadDesk.cs
if errorlevel 1 (
    echo.
    echo Build failed. Copy the error lines above and send them to Claude.
    pause
    exit /b 1
)

echo.
echo Done! PadDesk.exe is in this folder.
pause
