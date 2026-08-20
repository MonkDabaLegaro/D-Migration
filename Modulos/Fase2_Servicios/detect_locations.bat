@echo off
:: Detección de ubicaciones actuales (sin setlocal para conservar las variables)

:: Ollama (Instalación y Modelos)
set OLLAMA_PATH=C:\Program Files\Ollama
set OLLAMA_MODELS_PATH=%USERPROFILE%\.ollama

:: VS Code (Instalación, Extensiones y Datos de Usuario)
set VS_CODE_PATH=%LOCALAPPDATA%\Programs\Microsoft VS Code
set VS_CODE_EXT_PATH=%USERPROFILE%\.vscode\extensions
set VS_CODE_DATA_PATH=%APPDATA%\Code

:: Otras cachés y librerías
set PIP_CACHE_PATH=%LOCALAPPDATA%\pip\Cache
set NPM_CACHE_PATH=%APPDATA%\npm-cache
set PNPM_PATH=%APPDATA%\npm\node_modules\.pnpm-store
set YARN_PATH=%LOCALAPPDATA%\Yarn
set GRADLE_PATH=C:\Program Files\Gradle
set MAVEN_PATH=C:\Program Files\Maven
set NUGET_PATH=%APPDATA%\nuget
set CARGO_PATH=%LOCALAPPDATA%\rustup\toolchains
set COMPOSER_PATH=%APPDATA%\Composer

echo Detección de ubicaciones completada. >> "%LOG_FILE%"