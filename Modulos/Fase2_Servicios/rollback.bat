@echo off
:: Cambiar al directorio donde está el script (necesario al Ejecutar como Administrador)
cd /d "%~dp0"
:: Restaurar componentes
:: Primero detectamos las ubicaciones para tener las variables (sin setlocal)
call detect_locations.bat

echo Deshaciendo cambios (Restaurando Junctions)... >> "%LOG_FILE%"

call :UnlinkAndRestore "%OLLAMA_MODELS_PATH%" "D:\Componentes\Desarrollo\Ollama\.ollama"
call :UnlinkAndRestore "%OLLAMA_PATH%" "D:\Componentes\Desarrollo\Ollama\App"
call :UnlinkAndRestore "%VS_CODE_EXT_PATH%" "D:\Componentes\Desarrollo\VSCode\extensions"
call :UnlinkAndRestore "%VS_CODE_DATA_PATH%" "D:\Componentes\Desarrollo\VSCode\data"
call :UnlinkAndRestore "%PIP_CACHE_PATH%" "D:\Librerias\CachesPython"
call :UnlinkAndRestore "%NPM_CACHE_PATH%" "D:\Librerias\CacheNpm"
call :UnlinkAndRestore "%PNPM_PATH%" "D:\Componentes\Desarrollo\PNPM\node_modules\.pnpm-store"
call :UnlinkAndRestore "%YARN_PATH%" "D:\Componentes\Desarrollo\Yarn"
call :UnlinkAndRestore "%GRADLE_PATH%" "D:\Componentes\Desarrollo\Gradle"
call :UnlinkAndRestore "%MAVEN_PATH%" "D:\Componentes\Desarrollo\Maven"
call :UnlinkAndRestore "%NUGET_PATH%" "D:\Librerias\Nuget"
call :UnlinkAndRestore "%CARGO_PATH%" "D:\Librerias\Cargo"
call :UnlinkAndRestore "%COMPOSER_PATH%" "D:\Librerias\Composer"

echo =======================================================
echo Cambios deshechos exitosamente. >> "%LOG_FILE%"
echo Cambios deshechos exitosamente. Revisa el log para más detalles.
echo =======================================================
pause
exit /b 0

:UnlinkAndRestore
set "SRC=%~1"
set "DST=%~2"
if "%SRC%"=="" exit /b 0
if not exist "%DST%" (
    echo [Info] Destino en D: no existe, se omite: %DST% >> "%LOG_FILE%"
    exit /b 0
)

echo Restaurando %SRC% desde %DST% >> "%LOG_FILE%"
:: Eliminar el enlace simbólico (rmdir no borra los archivos de destino si es un junction)
if exist "%SRC%" rmdir "%SRC%" >> "%LOG_FILE%" 2>&1
:: Copiar de vuelta
robocopy "%DST%" "%SRC%" /E /R:3 /W:2 /MT:8 >> "%LOG_FILE%" 2>&1
:: Eliminar la carpeta en D:
rmdir /s /q "%DST%" >> "%LOG_FILE%" 2>&1
exit /b 0