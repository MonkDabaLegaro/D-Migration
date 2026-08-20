@echo off
chcp 65001 >nul
title Analisis y Movimiento de Archivos .MP4

echo Ejecutando analisis de archivos .MP4...
echo.

python "%~dp0analisis_mp4.py"

echo.
pause
