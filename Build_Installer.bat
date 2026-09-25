@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
title Memento Maker 0.9.91 Beta - Build Installer

echo ============================================================
echo MEMENTO MAKER 0.9.91 BETA - RELEASE BUILD
echo ============================================================
echo.
echo Building Memento Maker...
call Build_EXE.bat --nopause
if errorlevel 1 (
    echo.
    echo Application build failed. Installer was not created.
    pause
    exit /b 1
)

rem ------------------------------------------------------------
rem Locate the Inno Setup command-line compiler.
rem Prefer Inno Setup 7 x64, but retain compatibility with 7 x86
rem and Inno Setup 6 development machines.
rem ------------------------------------------------------------
set "ISCC="
set "INNO_LABEL="

rem Standard Inno Setup 7 locations (x64 first).
if exist "%ProgramFiles%\Inno Setup 7\ISCC.exe" (
    set "ISCC=%ProgramFiles%\Inno Setup 7\ISCC.exe"
    set "INNO_LABEL=Inno Setup 7 x64"
)
if not defined ISCC if defined ProgramFiles(x86) if exist "%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe" (
    set "ISCC=%ProgramFiles(x86)%\Inno Setup 7\ISCC.exe"
    set "INNO_LABEL=Inno Setup 7"
)
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe" (
    set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 7\ISCC.exe"
    set "INNO_LABEL=Inno Setup 7"
)

rem Fall back to an ISCC.exe available on PATH.
if not defined ISCC (
    for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC (
        set "ISCC=%%I"
        set "INNO_LABEL=Inno Setup from PATH"
    )
)

rem Backward-compatible Inno Setup 6 locations.
if not defined ISCC if defined ProgramFiles(x86) if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
    set "INNO_LABEL=Inno Setup 6"
)
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
    set "INNO_LABEL=Inno Setup 6"
)
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
    set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
    set "INNO_LABEL=Inno Setup 6"
)

if not defined ISCC (
    echo.
    echo ============================================================
    echo INSTALLER COMPILER NOT FOUND
    echo ============================================================
    echo.
    echo Memento Maker itself built successfully, but the Windows
    echo setup installer could not be created because ISCC.exe was
    echo not found.
    echo.
    echo Recommended: Inno Setup 7.1.0 x64
    echo Installer source: Installer\MementoMaker.iss
    echo.
    echo If Inno Setup is already installed in a custom location,
    echo add its folder to PATH or edit Build_Installer.bat.
    echo.
    pause
    exit /b 2
)

echo.
echo Installer compiler detected:
echo   !INNO_LABEL!
echo   !ISCC!

rem Read the actual file version when PowerShell is available.
set "ISCC_FOR_PS=!ISCC!"
set "INNO_VERSION="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "try { (Get-Item -LiteralPath $env:ISCC_FOR_PS).VersionInfo.ProductVersion } catch { '' }" 2^>nul`) do set "INNO_VERSION=%%V"
if defined INNO_VERSION echo   Version !INNO_VERSION!

echo.
echo Refreshing installer splash version...
powershell -NoProfile -ExecutionPolicy Bypass -File "Installer\RefreshSplashVersion.ps1"
if errorlevel 1 (
    echo.
    echo INSTALLER SPLASH VERSION REFRESH FAILED.
    pause
    exit /b 3
)

echo.
echo Building themed Windows installer...
"!ISCC!" "Installer\MementoMaker.iss"
if errorlevel 1 (
    echo.
    echo INSTALLER BUILD FAILED.
    pause
    exit /b 3
)

echo.
echo ============================================================
echo RELEASE BUILD COMPLETE
echo ============================================================
echo.
echo Installer:
echo   %CD%\Installer\Output\MementoMakerSetup_0.9.91_Beta.exe
echo.
echo Portable application files:
echo   %CD%\dist\
echo.
pause
