@echo off
chcp 65001 >nul
title Analisis de Archivos Sin Extension

echo Ejecutando analisis de archivos sin extension...
echo.

python "%~dp0analisis_disco.py"

echo.
pause
