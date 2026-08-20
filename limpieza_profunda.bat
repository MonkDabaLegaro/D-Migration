@echo off
echo ===================================================
echo     LIMPIEZA PROFUNDA DE VS CODE Y OLLAMA
echo ===================================================
echo.
echo ADVERTENCIA: Esto cerrara VS Code y borrara todas sus 
echo configuraciones, extensiones, y los modelos de Ollama.
echo No borrara tus proyectos de codigo.
echo.
pause

echo Cerrando aplicaciones...
taskkill /F /IM Code.exe >nul 2>&1
taskkill /F /IM ollama.exe >nul 2>&1
timeout /t 2 >nul

echo.
echo Borrando modelos y configuracion de Ollama...
rmdir /S /Q "%USERPROFILE%\.ollama" >nul 2>&1
rmdir /S /Q "%LOCALAPPDATA%\Ollama" >nul 2>&1

echo.
echo Borrando estado corrupto de VS Code...
rmdir /S /Q "%APPDATA%\Code" >nul 2>&1

echo.
echo Borrando todas las extensiones de VS Code...
rmdir /S /Q "%USERPROFILE%\.vscode" >nul 2>&1

echo.
echo ===================================================
echo LIMPIEZA COMPLETADA CON EXITO.
echo Ya puedes desinstalar los programas desde Windows y 
echo volver a instalarlos limpiamente.
echo ===================================================
pause
