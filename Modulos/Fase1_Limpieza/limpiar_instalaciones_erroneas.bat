@echo off
chcp 65001 >nul
title Gestor de Librerias y Caches

echo Ejecutando Gestor Interactivo en Python...
echo.

python "%~dp0gestor_librerias_c.py"
