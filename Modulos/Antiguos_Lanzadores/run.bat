@echo off
:: Cambiar al directorio donde está el script (necesario al Ejecutar como Administrador)
cd /d "%~dp0"
setlocal enabledelayedexpansion

:: Variables
set LOG_FILE=D:\Descargas\MigracionDesarrollo\logs\migracion.log
set BACKUP_DIR=D:\Descargas\MigracionDesarrollo\backup

:: Crear directorios necesarios
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
if not exist "D:\Descargas\MigracionDesarrollo\logs" mkdir "D:\Descargas\MigracionDesarrollo\logs"
if not exist "%LOG_FILE%" type nul > "%LOG_FILE%"
echo Iniciando migración > "%LOG_FILE%"

:: Paso 1: Detectar ubicaciones actuales
echo Detectando ubicaciones actuales...
echo Detectando ubicaciones actuales >> "%LOG_FILE%"
call detect_locations.bat

:: Paso 2: Comprobar permisos de administrador
echo Verificando permisos de administrador...
echo Verificando permisos de administrador >> "%LOG_FILE%"
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [INFO] Solicitando permisos de Administrador...
    powershell -Command "Start-Process '%~dpnx0' -Verb RunAs"
    exit /b 0
)

:: Paso 3: Comprobar espacio disponible
echo Comprobando espacio disponible en C:\...
echo Comprobando espacio disponible en C:\ >> "%LOG_FILE%"
:: call check_space.bat (Script no encontrado, omitido)

:: Paso 4: Cerrar procesos abiertos
echo Cerrando procesos abiertos...
echo Cerrando procesos abiertos >> "%LOG_FILE%"
call close_processes.bat

:: Paso 5: Crear copias de seguridad
echo Creando copias de seguridad...
echo Creando copias de seguridad >> "%LOG_FILE%"
call backup_config.bat

:: Paso 6: Mover componentes seguros
echo Moviendo componentes (esto puede tardar unos minutos)...
echo Moviendo componentes seguros >> "%LOG_FILE%"
call move_components.bat

:: Paso 6.5: Reparar posibles actualizaciones rotas de VS Code
echo Verificando integridad de VS Code (Python)...
echo Verificando integridad de VS Code (Python)... >> "%LOG_FILE%"
if exist "D:\Componentes\Desarrollo\VSCode\App" (
    python repair_vscode.py "D:\Componentes\Desarrollo\VSCode\App"
)

:: Paso 7: Actualizar variables de entorno
echo Actualizando variables de entorno...
echo Actualizando variables de entorno >> "%LOG_FILE%"
call update_env_vars.bat

:: Paso 8: Verificar instalaciones
echo Verificando instalaciones...
echo Verificando instalaciones >> "%LOG_FILE%"
call verify_installations.bat

:: Fin del script
echo =======================================================
echo Migración finalizada exitosamente.
echo Revisa los detalles en: %LOG_FILE%
echo =======================================================
pause