@echo off
chcp 65001 >nul
title Limpieza de Entornos de Desarrollo

echo ============================================================
echo   LIMPIEZA DE CACHES DE ENTORNOS DE DESARROLLO
echo ============================================================
echo.
echo Se intentara limpiar los caches de:
echo - Pip (Python)
echo - NPM (Node.js)
echo - Conda (Anaconda/Miniconda)
echo - Docker (Imagenes no usadas)
echo.

set /p confirm="Deseas continuar? (SI/NO): "
if /i not "%confirm%"=="SI" (
    echo.
    echo Operacion cancelada.
    pause
    exit /b
)

echo.
echo [1] Limpiando cache de PIP (Python)...
pip cache purge 2>nul
if %ERRORLEVEL% EQU 0 (echo   - Cache de pip limpio.) else (echo   - Pip no encontrado o error.)

echo.
echo [2] Limpiando cache de NPM (Node.js)...
call npm cache clean --force 2>nul
if %ERRORLEVEL% EQU 0 (echo   - Cache de npm limpio.) else (echo   - NPM no encontrado o error.)

echo.
echo [3] Limpiando cache de Conda...
call conda clean -a -y 2>nul
if %ERRORLEVEL% EQU 0 (echo   - Cache de conda limpio.) else (echo   - Conda no encontrado o error.)

echo.
echo [4] Limpiando sistema Docker (requiere que Docker este corriendo)...
docker system prune -a -f --volumes 2>nul
if %ERRORLEVEL% EQU 0 (echo   - Sistema Docker podado.) else (echo   - Docker no encontrado o no esta corriendo.)

echo.
echo ============================================================
echo   LIMPIEZA COMPLETADA
echo ============================================================
pause
