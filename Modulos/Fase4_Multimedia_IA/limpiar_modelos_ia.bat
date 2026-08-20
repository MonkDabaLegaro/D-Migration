@echo off
chcp 65001 >nul
title Limpiar Archivos de Modelos de IA

echo ============================================================
echo   LIMPIEZA DE ARCHIVOS DE MODELOS DE IA
echo ============================================================
echo.

echo Estos archivos ocupan mucho espacio:
echo   .SAFETENSORS (modelos incompletos) - 420 GB
echo   .INCOMPLETE   (descargas incompletas) - 62 GB
echo   .BIN          (modelos binarios) - 23 GB
echo   .BODY         (partes de modelos) - 11 GB
echo.
echo TOTAL: ~516 GB
echo.

echo Primero, buscaremos estos archivos para ver donde estan...
echo.

set "username=%USERNAME%"

echo [1] Buscando archivos .SAFETENSORS...
echo ----------------------------------------
for /r "C:\Users\%username%" %%f in (*.safetensors) do (
    echo %%f
)

echo.
echo [2] Buscando archivos .INCOMPLETE...
echo ----------------------------------------
for /r "C:\Users\%username%" %%f in (*.incomplete) do (
    echo %%f
)

echo.
echo [3] Buscando archivos .BIN en carpetas de IA...
echo ----------------------------------------------------
for /r "C:\Users\%username%" %%f in (*.bin) do (
    echo %%f
)

echo.
echo [4] Buscando archivos .BODY...
echo --------------------------------
for /r "C:\Users\%username%" %%f in (*.body) do (
    echo %%f
)

echo.
echo ============================================================
echo   RESUMEN DE ARCHIVOS ENCONTRADOS
echo ============================================================
echo.

echo Para eliminar estos archivos, ejecuta como ADMINISTRADOR:
echo.

echo OPCION 1: Eliminar solo archivos incompletos (.incomplete)
echo    del /s /q C:\Users\%username%\*.incomplete
echo.

echo OPCION 2: Eliminar archivos .safetensors
echo    del /s /q C:\Users\%username%\*.safetensors
echo.

echo OPCION 3: Eliminar archivos .bin en carpeta de Descargas
echo    del /s /q C:\Users\%username%\Downloads\*.bin
echo.

echo OPCION 4: ELIMINAR TODOS LOS ARCHIVOS DE MODELOS DE IA
echo    del /s /q C:\Users\%username%\*.safetensors
echo    del /s /q C:\Users\%username%\*.incomplete
echo    del /s /q C:\Users\%username%\*.bin
echo    del /s /q C:\Users\%username%\*.body
echo.

echo.
echo ============================================================
echo   QUE DESEAS HACER?
echo ============================================================
echo.
echo 1) Eliminar solo archivos .INCOMPLETE (62 GB)
echo 2) Eliminar archivos .SAFETENSORS (420 GB) 
echo 3) Eliminar todo los archivos de IA (516 GB)
echo 4) Nada, salir
echo.
set /p opcion="Elige una opcion (1-4): "

if "%opcion%"=="1" (
    echo.
    echo Eliminando archivos .incomplete...
    del /s /q "C:\Users\%username%\*.incomplete"
    echo Listo!
) else if "%opcion%"=="2" (
    echo.
    echo Eliminando archivos .safetensors...
    del /s /q "C:\Users\%username%\*.safetensors"
    echo Listo!
) else if "%opcion%"=="3" (
    echo.
    echo ELIMINANDO TODOS LOS ARCHIVOS DE MODELOS DE IA...
    echo.
    echo Eliminando .safetensors...
    del /s /q "C:\Users\%username%\*.safetensors"
    echo.
    echo Eliminando .incomplete...
    del /s /q "C:\Users\%username%\*.incomplete"
    echo.
    echo Eliminando .bin...
    del /s /q "C:\Users\%username%\*.bin"
    echo.
    echo Eliminando .body...
    del /s /q "C:\Users\%username%\*.body"
    echo.
    echo ======== ELIMINACION COMPLETADA ========
    echo.
    echo Has liberado aproximadamente 516 GB!
) else (
    echo.
    echo Saliendo sin hacer cambios...
)

echo.
pause
