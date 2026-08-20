@echo off
cd /d "%~dp0"
echo Iniciando DeepCleaner...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "DeepCleaner.ps1"
if %errorlevel% neq 0 pause
