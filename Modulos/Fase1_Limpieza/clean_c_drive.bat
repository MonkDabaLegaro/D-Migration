@echo off
setlocal enabledelayedexpansion

:: 1. Auto-Elevación de Privilegios
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [INFO] Solicitando permisos de Administrador para limpieza profunda...
    powershell -Command "Start-Process '%~dpnx0' -Verb RunAs"
    exit /b 0
)

echo =======================================================
echo          LIMPIADOR SUPREMO DEL DISCO C
echo =======================================================
echo.

:: 2. Aniquilación de la Instalación Intrusa de Python
set BAD_PYTHON=C:\Python314
if exist "%BAD_PYTHON%" (
    echo [INFO] Destruyendo instalacion intrusa de Python en %BAD_PYTHON%...
    :: Tomar propiedad y dar permisos totales
    takeown /f "%BAD_PYTHON%" /r /d y >nul 2>&1
    icacls "%BAD_PYTHON%" /grant administradores:F /t >nul 2>&1
    :: Eliminar a la fuerza
    rmdir /s /q "%BAD_PYTHON%"
    if not exist "%BAD_PYTHON%" (
        echo [OK] Python intruso eliminado con exito.
    ) else (
        echo [ERROR] No se pudo eliminar completamente. Algunos archivos podrian estar en uso.
    )
) else (
    echo [OK] No se encontro %BAD_PYTHON%. Todo limpio.
)
echo.

:: 3. Migracion Masiva de Carpetas de Desarrollo Ocultas
echo [INFO] Migrando entornos de desarrollo ocultos hacia D:\Componentes\Desarrollo...
set DEV_DEST=D:\Componentes\Desarrollo

call :MoveAndLink "%USERPROFILE%\.android" "%DEV_DEST%\Android"
call :MoveAndLink "%USERPROFILE%\.bun" "%DEV_DEST%\Bun"
call :MoveAndLink "%USERPROFILE%\.docker" "%DEV_DEST%\Docker"
call :MoveAndLink "%USERPROFILE%\.npm" "%DEV_DEST%\NPM_User"
call :MoveAndLink "%USERPROFILE%\.virtualenvs" "%DEV_DEST%\Python_VirtualEnvs"
call :MoveAndLink "%USERPROFILE%\.cache" "%DEV_DEST%\General_Cache"
call :MoveAndLink "%USERPROFILE%\.nuget" "%DEV_DEST%\Nuget_User"
call :MoveAndLink "%USERPROFILE%\source\repos" "%DEV_DEST%\Repositorios"

echo.
:: 4. Migracion de Carpetas Personales
echo [INFO] Migrando carpetas personales masivas hacia D:\Personal...
set PERS_DEST=D:\Personal

call :MoveAndLink "%USERPROFILE%\Downloads" "%PERS_DEST%\Descargas"
call :MoveAndLink "%USERPROFILE%\Documents" "%PERS_DEST%\Documentos"
call :MoveAndLink "%USERPROFILE%\Pictures" "%PERS_DEST%\Imagenes"
call :MoveAndLink "%USERPROFILE%\Videos" "%PERS_DEST%\Videos"
call :MoveAndLink "%USERPROFILE%\Music" "%PERS_DEST%\Musica"
call :MoveAndLink "%USERPROFILE%\Desktop" "%PERS_DEST%\Escritorio"

echo.
echo =======================================================
echo Limpieza Finalizada. El Disco C: ha vuelto a su estado base.
echo =======================================================
pause
exit /b 0

:: ==========================================
:: FUNCION: Mover y crear Enlace Simbolico
:: ==========================================
:MoveAndLink
set "SRC=%~1"
set "DST=%~2"
if not exist "%SRC%" exit /b 0

:: Si ya es un enlace simbolico, no hacemos nada
fsutil reparsepoint query "%SRC%" >nul 2>&1
if %errorLevel% equ 0 (
    echo [OMITIDO] %SRC% ya es un enlace simbolico.
    exit /b 0
)

echo [MIGRANDO] %SRC% -^> %DST%
if not exist "%DST%" mkdir "%DST%"

:: Mover archivos con robustez
robocopy "%SRC%" "%DST%" /E /MOVE /R:3 /W:2 /MT:8 >nul 2>&1

:: Eliminar carpeta origen vacia
rmdir /s /q "%SRC%" >nul 2>&1

:: Crear el tunel (Junction)
mklink /J "%SRC%" "%DST%" >nul
if exist "%SRC%" (
    echo   -^> Listo.
) else (
    echo   -^> [ERROR] No se pudo crear el enlace.
)
exit /b 0
