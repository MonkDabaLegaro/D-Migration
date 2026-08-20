@echo off
:: Crear copias de seguridad (sin setlocal)

echo Creando copias de seguridad de las instalaciones...
echo Creando copias de seguridad de las instalaciones... >> "%LOG_FILE%"
if exist "%OLLAMA_PATH%" robocopy "%OLLAMA_PATH%" "%BACKUP_DIR%\Ollama" /E /R:3 /W:2 /MT:8
if exist "%VS_CODE_PATH%" robocopy "%VS_CODE_PATH%" "%BACKUP_DIR%\VSCode" /E /R:3 /W:2 /MT:8

echo Copias de seguridad completadas. >> "%LOG_FILE%"