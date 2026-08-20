@echo off
:: Actualizar variables de entorno de forma segura

echo [INFO] Configurando variables de entorno...
echo [INFO] Estableciendo OLLAMA_MODELS a D:\Componentes\Desarrollo\Ollama\.ollama
setx OLLAMA_MODELS "D:\Componentes\Desarrollo\Ollama\.ollama"

:: NOTA: No modificamos el PATH porque estamos usando Enlaces Simbólicos (Junctions). 
:: Las rutas originales (C:\...) seguirán funcionando y apuntando a D:\ automáticamente.

echo [OK] Variables de entorno actualizadas de forma segura.