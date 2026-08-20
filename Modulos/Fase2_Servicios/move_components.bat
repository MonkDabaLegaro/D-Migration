@echo off
:: Mover componentes seguros y crear Junctions
echo Iniciando movimiento de componentes... >> "%LOG_FILE%"

call :MoveAndLink "%OLLAMA_MODELS_PATH%" "D:\Componentes\Desarrollo\Ollama\.ollama"
call :MoveAndLink "%OLLAMA_PATH%" "D:\Componentes\Desarrollo\Ollama\App"
call :MoveAndLink "%VS_CODE_EXT_PATH%" "D:\Componentes\Desarrollo\VSCode\extensions"
call :MoveAndLink "%VS_CODE_DATA_PATH%" "D:\Componentes\Desarrollo\VSCode\data"
call :MoveAndLink "%PIP_CACHE_PATH%" "D:\Librerias\CachesPython"
call :MoveAndLink "%NPM_CACHE_PATH%" "D:\Librerias\CacheNpm"
call :MoveAndLink "%PNPM_PATH%" "D:\Componentes\Desarrollo\PNPM\node_modules\.pnpm-store"
call :MoveAndLink "%YARN_PATH%" "D:\Componentes\Desarrollo\Yarn"
call :MoveAndLink "%GRADLE_PATH%" "D:\Componentes\Desarrollo\Gradle"
call :MoveAndLink "%MAVEN_PATH%" "D:\Componentes\Desarrollo\Maven"
call :MoveAndLink "%NUGET_PATH%" "D:\Librerias\Nuget"
call :MoveAndLink "%CARGO_PATH%" "D:\Librerias\Cargo"
call :MoveAndLink "%COMPOSER_PATH%" "D:\Librerias\Composer"

echo Componentes movidos exitosamente. >> "%LOG_FILE%"
exit /b 0

:MoveAndLink
set "SRC=%~1"
set "DST=%~2"
if "%SRC%"=="" exit /b 0
if not exist "%SRC%" (
    echo [Info] Origen no existe, se omite: %SRC%
    echo [Info] Origen no existe, se omite: %SRC% >> "%LOG_FILE%"
    exit /b 0
)
echo =======================================================
echo Moviendo %SRC% a %DST%...
echo =======================================================
echo Moviendo %SRC% a %DST% >> "%LOG_FILE%"
if not exist "%DST%" mkdir "%DST%"

:: Usar robocopy con reintentos (/R:3) y espera de 2 segundos (/W:2)
robocopy "%SRC%" "%DST%" /E /R:3 /W:2 /MT:8

:: Intentar eliminar la carpeta original
rmdir /s /q "%SRC%" >> "%LOG_FILE%" 2>&1

:: Verificar si se pudo eliminar completamente
if exist "%SRC%" (
    echo [ADVERTENCIA] No se pudo eliminar completamente %SRC% porque algunos archivos siguen en uso.
    echo Intentando renombrar la carpeta bloqueada...
    ren "%SRC%" "%~nx1_locked_%RANDOM%" >> "%LOG_FILE%" 2>&1
)

:: Crear el enlace simbólico
if not exist "%SRC%" (
    mklink /J "%SRC%" "%DST%"
) else (
    echo [ERROR CRITICO] No se pudo crear el enlace para %SRC% porque la carpeta original sigue existiendo y bloqueada.
)
exit /b 0