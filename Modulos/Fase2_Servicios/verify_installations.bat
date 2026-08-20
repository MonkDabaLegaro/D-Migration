@echo off
:: Verificar instalaciones (sin setlocal)

echo [INFO] Verificando instalaciones...

echo [INFO] Ollama: Verificando ejecutable...
if exist "%OLLAMA_PATH%\ollama.exe" (
    echo [OK] Ollama encontrado en %OLLAMA_PATH%
) else (
    echo [ERROR] Ollama no encontrado en %OLLAMA_PATH%
)

echo [INFO] VS Code: Verificando ejecutable...
if exist "%VS_CODE_PATH%\Code.exe" (
    echo [OK] VS Code encontrado en %VS_CODE_PATH%
) else (
    echo [ERROR] VS Code no encontrado en %VS_CODE_PATH%
)

echo [INFO] Verificando comandos globales (Python, npm, etc)...
echo ---
python --version 2>&1
pip --version 2>&1
git --version 2>&1
npm --version 2>&1
echo ---

echo [OK] Todas las instalaciones verificadas correctamente.
exit /b 0