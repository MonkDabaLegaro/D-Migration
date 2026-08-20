@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Iniciando el Pipeline Maestro...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "PipelineMaestro.ps1"
if %errorlevel% neq 0 pause
