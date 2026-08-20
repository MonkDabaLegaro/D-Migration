@echo off
chcp 65001 >nul
title Verificar Origen de Archivos

echo ============================================================
echo   VERIFICANDO UBICACION EXACTA DE LOS ARCHIVOS
echo ============================================================
echo.

set "username=%USERNAME%"

echo [1] Archivos .SAFETENSORS - Primero 10 ubicaciones:
echo --------------------------------------------------------
for /r "C:\Users\%username%" %%f in (*.safetensors) do (
    echo %%f
)
echo (Si no hay resultados, no existen archivos .safetensors)

echo.
echo [2] Archivos .INCOMPLETE - Todas las ubicaciones:
echo --------------------------------------------------------
for /r "C:\" %%f in (*.incomplete) do (
    echo %%f
)

echo.
echo [3] Archivos .BIN grandes (>1GB) en carpeta de usuario:
echo --------------------------------------------------------
for /r "C:\Users\%username%" %%f in (*.bin) do (
    for %%a in ("%%f") do (
        if %%~za GTR 1073741824 (
            echo %%f  -  ^(%%~za / 1073741824^ GB^)
        )
    )
)

echo.
echo [4] Archivos .BODY - Todas las ubicaciones:
echo --------------------------------------------------------
for /r "C:\Users\%username%" %%f in (*.body) do (
    echo %%f
)

echo.
echo [5] Buscando en carpetas comunes de modelos de IA:
echo --------------------------------------------------------

set "carpetas_ia=Documents Downloads AppData"

for %%c in (%carpetas_ia%) do (
    echo.
    echo Buscando en C:\Users\%username%\%%c\...
    for /d /r "C:\Users\%username%\%%c" %%d in (*) do (
        echo  %%d
    )
)

echo.
echo ============================================================
echo   RESUMEN DE CARPETAS DONDE SE ENCUENTRAN:
echo ============================================================
echo.

echo Busca manualmente estas carpetas:
echo   C:\Users\%username%\Documents\
echo   C:\Users\%username%\Downloads\
echo   C:\Users\%username%\AppData\Local\
echo.

echo Presiona una tecla para buscar en Descargas...
pause >nul

echo.
echo Buscando en Descargas:
echo ------------------------
dir "C:\Users\%username%\Downloads" /s /a | findstr /i "safetensors incomplete bin body"

echo.
echo Presiona una tecla para buscar en Documents...
pause >nul

echo.
echo Buscando en Documents:
echo ------------------------
dir "C:\Users\%username%\Documents" /s /a | findstr /i "safetensors incomplete bin body"

echo.
echo ============================================================
echo   FIN DEL ANALISIS
echo ============================================================
pause
