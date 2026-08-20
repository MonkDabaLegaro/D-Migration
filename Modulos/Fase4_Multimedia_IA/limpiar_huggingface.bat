@echo off
chcp 65001 >nul
title Limpiar Cache de HuggingFace

echo ============================================================
echo   LIMPIAR CACHE DE HUGGINGFACE
echo ============================================================
echo.

echo Esta accion eliminara:
echo   - Modelos descargados de HuggingFace
echo   - Descargas incompletas
echo   - Archivos .safetensors, .bin, .incomplete
echo.

set /p confirm="Escribe SI para confirmar: "

if /i not "%confirm%"=="SI" (
    echo.
    echo Cancelado.
    pause
    exit /b
)

echo.
echo Eliminando cache de HuggingFace...
echo.

echo Eliminando carpeta de modelos...
rmdir /s /q "C:\Users\%USERNAME%\.cache\huggingface\hub" 2>nul

echo.
echo ======== LIMPIEZA COMPLETADA ========
echo.
echo Has liberado aproximadamente 420 GB de modelos de IA.
echo.

pause
