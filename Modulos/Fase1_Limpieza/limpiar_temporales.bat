@echo off
chcp 65001 >nul
title Limpieza de Archivos Temporales

echo ============================================================
echo   LIMPIEZA DE ARCHIVOS TEMPORALES DEL SISTEMA
echo ============================================================
echo.
echo Esta accion eliminara archivos en:
echo - %%TEMP%% (Temporales del usuario)
echo - C:\Windows\Temp (Temporales del sistema)
echo - C:\Windows\Prefetch (Prefetch del sistema)
echo.

set /p confirm="Deseas continuar? Asegurate de cerrar otros programas. (SI/NO): "
if /i not "%confirm%"=="SI" (
    echo.
    echo Operacion cancelada.
    pause
    exit /b
)

echo.
echo [1] Limpiando temporales del usuario (%%TEMP%%)...
del /s /f /q "%TEMP%\*.*" 2>nul
for /d %%p in ("%TEMP%\*") do rmdir "%%p" /s /q 2>nul

echo.
echo [2] Limpiando temporales del sistema (C:\Windows\Temp)...
echo (Puede requerir permisos de Administrador para limpiar todo)
del /s /f /q "C:\Windows\Temp\*.*" 2>nul
for /d %%p in ("C:\Windows\Temp\*") do rmdir "%%p" /s /q 2>nul

echo.
echo [3] Limpiando Prefetch (C:\Windows\Prefetch)...
echo (Puede requerir permisos de Administrador)
del /s /f /q "C:\Windows\Prefetch\*.*" 2>nul

echo.
echo ============================================================
echo   LIMPIEZA DE TEMPORALES COMPLETADA
echo ============================================================
pause
