# Utilidades de Análisis y Limpieza de Disco

Conjunto de scripts organizados para analizar el uso del disco y realizar limpiezas profundas del sistema, optimizados para ejecutar en Windows / PowerShell.

## Uso Principal

**La forma recomendada de utilizar estas utilidades es mediante el script principal:**

```powershell
.\limpieza_maestra.bat
```
Al abrir este archivo, se presentará un menú interactivo desde el que podrás invocar todas las funciones de análisis y limpieza.

## Estructura de Carpetas

Los scripts se dividen en 5 categorías principales:

### `General_Disco\`
Análisis general de uso del disco C, detecta qué carpetas y archivos grandes ocupan espacio.
- `analisis_disco.py`
- `analisis_sin_ext.bat`

### `Modelos_IA\`
Enfocado en limpiar archivos residuales gigantes relacionados con Machine Learning.
- `limpiar_modelos_ia.bat`: Elimina archivos `.safetensors`, `.incomplete`, `.bin`, `.body`.
- `limpiar_huggingface.bat`: Borra la carpeta de caché `~/.cache/huggingface/hub`.
- `verificar_origen.bat`: Busca ubicaciones de estos archivos pesados.

### `Videos_MP4\`
- `analisis_mp4.py` y `mover_mp4.bat`: Localiza archivos `.mp4` en el disco C y los mueve a la unidad D.

### `Discos_VHDX\`
- `analisis_vhdx.py` y `eliminar_vhdx.bat`: Localiza y permite eliminar discos duros virtuales pesados.

### `Entornos_Dev\`
Herramientas avanzadas para limpieza de desarrollo y sistema.
- `limpiar_entornos_dev.bat`: Purga cachés de **pip**, **npm**, **conda**, y elimina contenedores de **docker** sin usar.
- `limpiar_temporales.bat`: Elimina la basura de `%TEMP%`, `C:\Windows\Temp` y `Prefetch`.
- `limpiar_instalaciones_erroneas.bat`: Busca instalaciones accidentales de Python y Java en C:, además de cachés gigantescos de librerías de IA como Whisper o Torch, y te permite borrarlos de forma interactiva.

---
> Nota: Si hay rutas muy largas durante los análisis, se utiliza internamente `\\?\` para escapar las limitaciones de Windows.