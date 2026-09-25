@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "NOPAUSE="
if /I "%~1"=="--nopause" set "NOPAUSE=1"
title Memento Maker 0.9.91 Beta

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo ERROR: The Windows .NET Framework C# compiler could not be found.
    echo Expected csc.exe under C:\Windows\Microsoft.NET\Framework...
    echo.
    if not defined NOPAUSE pause
    exit /b 1
)

if exist dist rmdir /s /q dist
mkdir dist

set "REFS=/reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:"Theme\MM_Icon.ico" /out:"dist\MementoMaker.exe" %REFS% Properties\AssemblyInfo.cs src\*.cs src\Services\*.cs
if errorlevel 1 (
    echo.
    echo BUILD FAILED.
    if not defined NOPAUSE pause
    exit /b 2
)

xcopy /E /I /Y Automation "dist\Automation" >nul
xcopy /E /I /Y Config "dist\Config" >nul
xcopy /E /I /Y Theme "dist\Theme" >nul
copy /Y README.txt "dist\README.txt" >nul
copy /Y CREDITS.txt "dist\CREDITS.txt" >nul
copy /Y VERSION.txt "dist\VERSION.txt" >nul

echo.
echo ============================================================
echo BUILD COMPLETE
echo ============================================================
echo.
echo EXE:
echo   %CD%\dist\MementoMaker.exe
echo.
echo You can now run the EXE from the dist folder.
echo Keep the Automation, Config and Theme folders beside the EXE.
echo.
if not defined NOPAUSE pause
