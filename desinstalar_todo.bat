@echo off
echo ===================================================
echo     DESINSTALACION TOTAL DE VS CODE Y OLLAMA
echo ===================================================
echo.
echo ADVERTENCIA: Este script desinstalara por completo Visual Studio 
echo Code y Ollama de tu computadora, ademas de borrar todas tus 
echo configuraciones, extensiones y los modelos pesados.
echo (No borrara tus proyectos de codigo).
echo.
pause

echo Cerrando aplicaciones...
taskkill /F /IM Code.exe >nul 2>&1
taskkill /F /IM ollama.exe >nul 2>&1
timeout /t 2 >nul

echo.
echo Desinstalando Ollama...
if exist "%LOCALAPPDATA%\Programs\Ollama\unins000.exe" (
    "%LOCALAPPDATA%\Programs\Ollama\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
    echo Ollama desinstalado.
) else (
    echo Ollama no se encontro o ya fue desinstalado.
)

echo.
echo Desinstalando Visual Studio Code...
if exist "%LOCALAPPDATA%\Programs\Microsoft VS Code\unins000.exe" (
    "%LOCALAPPDATA%\Programs\Microsoft VS Code\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
    echo VS Code desinstalado.
) else if exist "%PROGRAMFILES%\Microsoft VS Code\unins000.exe" (
    "%PROGRAMFILES%\Microsoft VS Code\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
    echo VS Code desinstalado.
) else (
    echo Visual Studio Code no se encontro o ya fue desinstalado.
)

echo.
echo Borrando carpetas residuales y modelos de 5GB...
rmdir /S /Q "%USERPROFILE%\.ollama" >nul 2>&1
rmdir /S /Q "%LOCALAPPDATA%\Ollama" >nul 2>&1
rmdir /S /Q "%APPDATA%\Code" >nul 2>&1
rmdir /S /Q "%USERPROFILE%\.vscode" >nul 2>&1

echo.
echo ===================================================
echo DESINSTALACION Y LIMPIEZA PROFUNDA COMPLETADA.
echo Tu sistema esta totalmente limpio. Puedes volver a 
echo descargar e instalar todo desde cero.
echo ===================================================
pause
