@echo off
chcp 65001 >nul
title Analisis y Limpieza de Archivos .VHDX

echo Ejecutando analisis de archivos .VHDX...
echo.

python "%~dp0analisis_vhdx.py"

echo.
pause
