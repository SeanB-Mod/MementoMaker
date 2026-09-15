@echo off
setlocal
cd /d "%~dp0"
call Build_EXE.bat --nopause
if errorlevel 1 exit /b %errorlevel%
start "" "%~dp0dist\MementoMaker.exe"
