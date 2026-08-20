@echo off
chcp 65001 >nul
title Herramienta Maestra de Limpieza y Analisis

:MENU
cls
echo ============================================================
echo   MENU MAESTRO DE LIMPIEZA Y ANALISIS DE DISCO
echo ============================================================
echo.
echo  1) Analisis de Disco General (Sin Extensiones, Carpetas)
echo  2) Limpiar Cache de HuggingFace (Modelos IA)
echo  3) Limpiar Modelos de IA Incompletos / Huerfanos (.safetensors, etc)
echo  4) Analizar y Mover Archivos MP4 a Disco D
echo  5) Analizar y Eliminar Discos Virtuales VHDX
echo  6) Limpiar Caches de Entornos de Desarrollo (Pip, Npm, Conda, Docker)
echo  7) Limpiar Archivos Temporales del Sistema (%%TEMP%%, Prefetch)
echo  8) Limpiar Instalaciones Erroneas en C: (Python, Java, Whisper, Torch)
echo  9) Ejecutar Toda la Limpieza Recomendada (Opciones 2, 3, 6, 7)
echo 10) Salir
echo.
set /p opcion="Elige una opcion (1-10): "

if "%opcion%"=="1" goto OP_1
if "%opcion%"=="2" goto OP_2
if "%opcion%"=="3" goto OP_3
if "%opcion%"=="4" goto OP_4
if "%opcion%"=="5" goto OP_5
if "%opcion%"=="6" goto OP_6
if "%opcion%"=="7" goto OP_7
if "%opcion%"=="8" goto OP_8
if "%opcion%"=="9" goto OP_9
if "%opcion%"=="10" goto SALIR

echo Opcion no valida.
pause
goto MENU

:OP_1
cls
call "%~dp0General_Disco\analisis_sin_ext.bat"
goto MENU

:OP_2
cls
call "%~dp0Modelos_IA\limpiar_huggingface.bat"
goto MENU

:OP_3
cls
call "%~dp0Modelos_IA\limpiar_modelos_ia.bat"
goto MENU

:OP_4
cls
call "%~dp0Videos_MP4\mover_mp4.bat"
goto MENU

:OP_5
cls
call "%~dp0Discos_VHDX\eliminar_vhdx.bat"
goto MENU

:OP_6
cls
call "%~dp0Entornos_Dev\limpiar_entornos_dev.bat"
goto MENU

:OP_7
cls
call "%~dp0Entornos_Dev\limpiar_temporales.bat"
goto MENU

:OP_8
cls
call "%~dp0Entornos_Dev\limpiar_instalaciones_erroneas.bat"
goto MENU

:OP_9
cls
echo Iniciando limpieza total recomendada...
echo.
echo --- PASO 1: Cache HuggingFace ---
call "%~dp0Modelos_IA\limpiar_huggingface.bat"
echo.
echo --- PASO 2: Modelos IA Incompletos ---
call "%~dp0Modelos_IA\limpiar_modelos_ia.bat"
echo.
echo --- PASO 3: Entornos de Desarrollo ---
call "%~dp0Entornos_Dev\limpiar_entornos_dev.bat"
echo.
echo --- PASO 4: Archivos Temporales ---
call "%~dp0Entornos_Dev\limpiar_temporales.bat"
echo.
echo Limpieza total finalizada.
pause
goto MENU

:SALIR
exit /b
