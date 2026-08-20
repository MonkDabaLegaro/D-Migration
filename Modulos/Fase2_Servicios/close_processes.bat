@echo off
:: Cerrar procesos abiertos

echo Cerrando procesos que puedan bloquear los archivos... >> "%LOG_FILE%"
taskkill /F /IM code.exe >nul 2>&1
taskkill /F /IM ollama.exe >nul 2>&1
taskkill /F /IM "ollama app.exe" >nul 2>&1
taskkill /F /IM python.exe >nul 2>&1
taskkill /F /IM node.exe >nul 2>&1
taskkill /F /IM java.exe >nul 2>&1

:: Esperar un par de segundos para que los procesos liberen los archivos
timeout /T 3 /NOBREAK >nul

echo Procesos cerrados. >> "%LOG_FILE%"