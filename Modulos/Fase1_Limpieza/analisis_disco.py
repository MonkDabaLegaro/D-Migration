# -*- coding: utf-8 -*-
"""
Análisis detallado de uso del disco local C
Muestra el espacio utilizado por carpetas y archivos grandes
"""

import os
import sys
import subprocess
import time
import binascii
from pathlib import Path
from collections import defaultdict

def clear_line():
    """Limpia la línea actual para actualizar la barra"""
    sys.stdout.write('\r' + ' ' * 100 + '\r')
    sys.stdout.flush()

def print_progress_bar(percent, prefix='', suffix='', bar_length=40):
    """Muestra una barra de progreso en la misma línea"""
    filled = int(bar_length * percent / 100)
    bar = '█' * filled + '░' * (bar_length - filled)
    sys.stdout.write(f"\r[{bar}] {percent:3d}% {prefix} {suffix}")
    sys.stdout.flush()

def format_size(size_bytes):
    """Formatea el tamaño en bytes a una cadena legible"""
    for unit in ['B', 'KB', 'MB', 'GB', 'TB']:
        if size_bytes < 1024.0:
            return f"{size_bytes:.2f} {unit}"
        size_bytes /= 1024.0
    return f"{size_bytes:.2f} PB"

def detect_file_type(filepath):
    """Identifica tipo de archivo por contenido (firmas y texto)"""
    try:
        with open(filepath, 'rb') as f:
            header = f.read(32)
    except Exception:
        try:
            return 'ERROR_LECTURA', 'No se pudo leer'
        except Exception:
            return 'ACCESO_DENEGADO', 'Acceso denegado'
    
    # Firmas conocidas (magic bytes)
    tests = [
        ('PK\x03\x04', 'ZIP/DOC/XLSX/JAR'),
        ('%PDF', 'PDF'),
        ('\x89PNG\r\n\x1a\n', 'PNG'),
        ('\xff\xd8\xff', 'JPEG'),
        ('GIF8', 'GIF'),
        ('BM', 'BMP'),
        ('RIFF', 'RIFF/AVI/WEBP'),
        ('ID3', 'MP3'),
        ('OggS', 'OGG'),
        ('MThd', 'MIDI'),
        ('SQLite format', 'SQLite'),
        ('\x1f\x8b\x08', 'GZIP'),
        ('\x75\x73\x74\x61\x72', 'TAR'),
        ('\xfd7zXZ', 'XZ'),
        ('7z\xbc\xaf\x27\x1c', '7Z'),
        ('<!DOCTYPE', 'HTML'),
        ('<html', 'HTML'),
        ('<?xml', 'XML'),
        ('{\n', 'JSON'),
        ('{"', 'JSON'),
        ('{', 'JSON/OBJETO'),
        ('[', 'LISTA/ARRAY'),
        ('\ufeff', 'Texto con BOM'),
        ('', None),
    ]
    
    try:
        head = header[:8]
    except Exception:
        return 'VACIO', 'Archivo vacio o corrupto'
    
    for magic, name in tests:
        if magic and head.startswith(magic.encode('latin-1', errors='ignore') if isinstance(magic, str) else magic):
            return 'BINARIO', name
    
    # Comprobar si es texto imprimible (UTF-8 / ASCII)
    text = head.decode('utf-8', errors='ignore')
    printable = sum(1 for c in text if c.isprintable() or c in '\r\n\t')
    
    if printable > 4 and len(text) > 0:
        if any(marker in text for marker in ['MZ', 'PE\x00\x00']):
            try:
                if header[:2] == b'MZ':
                    return 'EJECUTABLE', 'EXE/DLL (PE)'
            except Exception:
                pass
        
        try:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as ft:
                sample = ft.read(4096)
            if '\x00' in sample:
                return 'BINARIO', 'Contiene bytes nulos (probablemente binario)'
            if len(sample) > 0 and all(ord(c) < 128 or c.isprintable() for c in sample[:4000] if ord(c) < 128 or c in '\r\n\t'):
                return 'TEXTO', sample.splitlines()[0][:60] if sample.splitlines() else 'Archivo de texto'
        except Exception:
            pass
        
        return 'TEXTO_LEGIBLE', 'Texto / datos legibles'
    
    return 'BINARIO_DESCONOCIDO', 'Binario sin firma conocida'

def print_header():
    print("=" * 60)
    print("       ANALISIS DETALLADO DEL DISCO C:")
    print("=" * 60)
    print()

def get_disk_space():
    """Obtiene el espacio total y libre del disco C"""
    print("[1] ESPACIO TOTAL Y LIBRE EN EL DISCO C")
    print("-" * 60)
    
    try:
        result = subprocess.run(
            ['wmic', 'logicaldisk', 'where', "DeviceID='C:'", 'get', 'Size,FreeSpace', '/value'],
            capture_output=True, text=True, encoding='utf-8', timeout=30
        )
        
        # Parsear los valores
        lines = result.stdout.strip().split('\n')
        free_space = 0
        total_size = 0
        
        for line in lines:
            if 'FreeSpace' in line:
                free_space = int(line.split('=')[1].strip())
            elif 'Size' in line:
                total_size = int(line.split('=')[1].strip())
        
        if total_size > 0:
            used = total_size - free_space
            print(f"  Espacio total:    {format_size(total_size)}")
            print(f"  Espacio libre:    {format_size(free_space)}")
            print(f"  Espacio usado:    {format_size(used)}")
            print(f"  Porcentaje usado: {(used/total_size)*100:.1f}%")
            
    except Exception as e:
        print(f"Error al obtener espacio del disco: {e}")
    print()

def analyze_all_folders_detailed():
    """Análisis detallado de TODAS las carpetas"""
    print("[2] ANALISIS DETALLADO DE CARPETAS")
    print("-" * 60)
    
    # Carpetas principales a analizar
    folders_to_analyze = [
        "C:\\Windows",
        "C:\\Program Files",
        "C:\\Program Files (x86)", 
        "C:\\ProgramData",
        "C:\\Users",
        "C:\\PerfLogs",
    ]
    
    results = []
    
    print("Analizando carpetas del sistema...")
    print()
    
    for i, folder in enumerate(folders_to_analyze):
        folder_name = os.path.basename(folder)
        
        # Barra de progreso
        for p in range(0, 101, 15):
            print_progress_bar(p, prefix=f"Analizando {folder_name}")
            time.sleep(0.05)
        
        clear_line()
        
        if os.path.exists(folder):
            try:
                total_size = 0
                file_count = 0
                folder_count = 0
                
                for dirpath, dirnames, filenames in os.walk(folder):
                    folder_count += len(dirnames)
                    for filename in filenames:
                        filepath = os.path.join(dirpath, filename)
                        try:
                            total_size += os.path.getsize(filepath)
                            file_count += 1
                        except (OSError, FileNotFoundError):
                            pass
                        
                        # Actualizar progreso cada 5000 archivos
                        if file_count % 5000 == 0:
                            print_progress_bar(
                                min(95, int((file_count % 10000) / 100)), 
                                prefix=f"Procesando archivos ({file_count})"
                            )
                
                size_gb = total_size / (1024**3)
                results.append({
                    'name': folder_name,
                    'size': total_size,
                    'size_gb': size_gb,
                    'files': file_count,
                    'folders': folder_count
                })
                print(f"  ✓ {folder_name:<25} {size_gb:>8.2f} GB  ({file_count:,} archivos)")
                
            except Exception as e:
                print(f"  ✗ Error analizando {folder_name}: {e}")
        else:
            print(f"  - {folder_name:<25} No existe")
    
    print()
    
    # Análisis de carpetas de usuario
    print("Analizando carpetas de usuario...")
    print()
    
    username = os.getenv('USERNAME', 'Usuario')
    user_folders = [
        "Downloads",
        "Documents", 
        "Desktop",
        "Videos",
        "Pictures",
        "Music",
        "AppData",
        "OneDrive",
        "Google Drive",
    ]
    
    user_home = Path(f"C:\\Users\\{username}")
    
    for folder in user_folders:
        folder_path = user_home / folder
        
        for p in range(0, 101, 20):
            print_progress_bar(p, prefix=f"Analizando {folder}")
            time.sleep(0.1)
        
        clear_line()
        
        if folder_path.exists():
            try:
                total_size = 0
                file_count = 0
                folder_count = 0
                
                for dirpath, dirnames, filenames in os.walk(folder_path):
                    folder_count += len(dirnames)
                    for filename in filenames:
                        filepath = os.path.join(dirpath, filename)
                        try:
                            total_size += os.path.getsize(filepath)
                            file_count += 1
                        except (OSError, FileNotFoundError):
                            pass
                
                size_gb = total_size / (1024**3)
                results.append({
                    'name': f"Users\\{username}\\{folder}",
                    'size': total_size,
                    'size_gb': size_gb,
                    'files': file_count,
                    'folders': folder_count
                })
                
                icon = "🔴" if size_gb > 10 else "🟡" if size_gb > 1 else "🟢"
                print(f"  {icon} {folder:<25} {size_gb:>8.2f} GB  ({file_count:,} archivos)")
                
            except Exception as e:
                print(f"  ✗ Error: {e}")
    
    print()
    print("=" * 60)
    print("RESUMEN - CARPETAS ORDENADAS POR TAMAÑO:")
    print("=" * 60)
    print()
    print(f"{'Carpeta':<40} {'Tamano':>12} {'Archivos':>10}")
    print("-" * 65)
    
    results.sort(key=lambda x: x['size'], reverse=True)
    total_analyzed = 0
    
    for r in results:
        total_analyzed += r['size']
        print(f"{r['name']:<40} {r['size_gb']:>10.2f} GB {r['files']:>10,}")
    
    print("-" * 65)
    print(f"{'TOTAL ANALIZADO':<40} {total_analyzed/(1024**3):>10.2f} GB")
    print()

def find_all_large_files():
    """Busca TODOS los archivos grandes"""
    print("[3] ARCHIVOS GRANDES ENCONTRADOS")
    print("=" * 60)
    
    # Categorías de tamaño a buscar
    size_limits = [
        (1 * 1024**3, "1 GB+"),    # Mayor a 1 GB
        (500 * 1024**2, "500 MB - 1 GB"),  # 500 MB a 1 GB
        (200 * 1024**2, "200 - 500 MB"),   # 200 a 500 MB
        (100 * 1024**2, "100 - 200 MB"),   # 100 a 200 MB
    ]
    
    # Carpetas donde buscar (todas las importantes)
    search_paths = [
        f"C:\\Users\\{os.getenv('USERNAME', 'Usuario')}",
        "C:\\Users\\Public",
    ]
    
    all_large_files = []
    
    for min_size, size_label in size_limits:
        print()
        print(f"Buscando archivos de {size_label}...")
        
        # Animación de carga
        for p in range(0, 101, 10):
            print_progress_bar(p, prefix=f"Escaneando {size_label}")
            time.sleep(0.05)
        
        clear_line()
        
        files_found = []
        
        for search_path in search_paths:
            if not os.path.exists(search_path):
                continue
                
            try:
                for dirpath, dirnames, filenames in os.walk(search_path):
                    # No limitar profundidad
                    for filename in filenames:
                        filepath = os.path.join(dirpath, filename)
                        try:
                            size = os.path.getsize(filepath)
                            if size >= min_size:
                                files_found.append({
                                    'path': filepath,
                                    'size': size,
                                    'folder': os.path.dirname(filepath)
                                })
                        except (OSError, FileNotFoundError):
                            pass
            except Exception:
                pass
        
        all_large_files.extend(files_found)
        print(f"  ✓ Encontrados {len(files_found)} archivos de {size_label}")
    
    # Ordenar todos los archivos por tamaño
    all_large_files.sort(key=lambda x: x['size'], reverse=True)
    
    print()
    print("=" * 60)
    print("TOP 50 ARCHIVOS MAS GRANDES:")
    print("=" * 60)
    print()
    
    print(f"{'#':<4} {'Tamano':<12} {'Tipo':<10} {'Carpeta'}")
    print("-" * 80)
    
    file_types = defaultdict(int)
    
    for i, f in enumerate(all_large_files[:50], 1):
        ext = os.path.splitext(f['path'])[1].upper() or 'SIN EXT'
        file_types[ext] += 1
        
        folder = f['folder']
        if len(folder) > 35:
            folder = "..." + folder[-32:]
        
        print(f"{i:<4} {format_size(f['size']):<12} {ext:<10} {folder}")
    
    print()
    print("=" * 60)
    print("RESUMEN POR TIPO DE ARCHIVO:")
    print("=" * 60)
    print()
    
    # Agrupar por extensión
    type_summary = defaultdict(lambda: {'count': 0, 'size': 0})
    
    for f in all_large_files:
        ext = os.path.splitext(f['path'])[1].upper() or 'SIN EXT'
        type_summary[ext]['count'] += 1
        type_summary[ext]['size'] += f['size']
    
    # Ordenar por tamaño total
    type_list = [(k, v['count'], v['size']) for k, v in type_summary.items()]
    type_list.sort(key=lambda x: x[2], reverse=True)
    
    print(f"{'Tipo':<15} {'Cantidad':>10} {'Tamano Total':>15}")
    print("-" * 45)
    
    for ext, count, size in type_list[:15]:
        print(f"{ext:<15} {count:>10} {format_size(size):>15}")
    
    print()

def show_recommendations():
    """Muestra recomendaciones para liberar espacio"""
    print("[4] RECOMENDACIONES PARA LIBERAR ESPACIO")
    print("=" * 60)
    print()
    
    username = os.getenv('USERNAME', 'Usuario')
    
    recommendations = []
    
    # 1. Papelera de reciclaje
    recycle_path = f"C:\\$Recycle.Bin"
    if os.path.exists(recycle_path):
        recommendations.append(("Vaciar papelera de reciclaje", "C:\\$Recycle.Bin", "Alto"))
    
    # 2. Carpeta Temp
    temp_paths = [
        f"C:\\Users\\{username}\\AppData\\Local\\Temp",
        "C:\\Temp",
        "C:\\Windows\\Temp",
    ]
    for tp in temp_paths:
        if os.path.exists(tp):
            recommendations.append(("Limpiar archivos temporales", tp, "Medio"))
    
    # 3. Windows Update
    update_path = "C:\\Windows\\SoftwareDistribution\\Download"
    if os.path.exists(update_path):
        recommendations.append(("Limpiar actualizaciones de Windows", update_path, "Medio"))
    
    # 4. OneDrive
    onedrive_path = f"C:\\Users\\{username}\\OneDrive"
    if os.path.exists(onedrive_path):
        recommendations.append(("Revisar OneDrive local", onedrive_path, "Bajo"))
    
    # 5. Caché de navegadores
    browser_cache = [
        f"C:\\Users\\{username}\\AppData\\Local\\Google\\Chrome\\User Data\\Default\\Cache",
        f"C:\\Users\\{username}\\AppData\\Local\\Microsoft\\Edge\\User Data\\Default\\Cache",
    ]
    for bc in browser_cache:
        if os.path.exists(bc):
            recommendations.append(("Limpiar caché del navegador", bc, "Bajo"))
    
    # Mostrar recomendaciones
    print(f"{'Accion':<40} {'Ruta':<25} {'Prioridad'}")
    print("-" * 70)
    
    priority_colors = {
        "Alto": "🔴",
        "Medio": "🟡", 
        "Bajo": "🟢"
    }
    
    for action, path, priority in recommendations:
        icon = priority_colors.get(priority, "⚪")
        display_path = path if len(path) <= 25 else "..." + path[-22:]
        print(f"{action:<40} {display_path:<25} {icon} {priority}")
    
    print()
    print("Para ejecutar como administrador y limpiar:")
    print("  - Papelera: cls /c rd /s /q C:\\$Recycle.Bin")
    print("  - Temp: del /q /s C:\\Users\\...\\AppData\\Local\\Temp\\*.*")
    print()

def find_files_without_extension():
    """Busca archivos sin extension y analiza su tipo"""
    print()
    print("=" * 60)
    print("[5] ARCHIVOS SIN EXTENSION EN DISCO C")
    print("=" * 60)
    print()
    
    search_paths = [
        f"C:\\Users\\{os.getenv('USERNAME', 'Usuario')}",
        "C:\\Users\\Public",
    ]
    
    all_no_ext = []
    
    for search_path in search_paths:
        if not os.path.exists(search_path):
            continue
            
        print(f"Buscando en: {search_path}")
        file_count = 0
        
        for dirpath, dirnames, filenames in os.walk(search_path):
            for filename in filenames:
                if '.' not in filename or filename.startswith('.'):
                    filepath = os.path.join(dirpath, filename)
                    try:
                        size = os.path.getsize(filepath)
                        tipo, detalle = detect_file_type(filepath)
                        all_no_ext.append({
                            'path': filepath,
                            'size': size,
                            'name': filename,
                            'tipo': tipo,
                            'detalle': detalle,
                            'folder': os.path.dirname(filepath)
                        })
                    except (OSError, FileNotFoundError):
                        pass
                file_count += 1
                if file_count % 5000 == 0:
                    print_progress_bar(min(95, file_count // 500), prefix='Procesando', suffix=f'({file_count})')
        
        clear_line()
        print()
    
    if not all_no_ext:
        print("  🟢 No se encontraron archivos sin extension")
        print()
        return
    
    all_no_ext.sort(key=lambda x: x['size'], reverse=True)
    
    total_size = sum(f['size'] for f in all_no_ext)
    
    Riesgos = {
        'EJECUTABLE': '🔴 ALTO',
        'ACCESO_DENEGADO': '🔴 ALTO',
        'BINARIO_DESCONOCIDO': '🟡 MEDIO',
        'BINARIO': '🟡 MEDIO',
        'TEXTO_LEGIBLE': '🟢 BAJO',
    }
    
    print(f"✓ Encontrados {len(all_no_ext)} archivos sin extension")
    print(f"  Tamaño total: {format_size(total_size)}")
    print()
    
    print("=" * 60)
    print(f"{'#':<4} {'Tamano':<12} {'Riesgo':<12} {'Tipo detectado':<30} {'Carpeta':<35}")
    print("-" * 90)
    
    for i, f in enumerate(all_no_ext[:50], 1):
        size_str = format_size(f['size'])
        riesgo = Riesgos.get(f['tipo'], '⚪ DESCONOCIDO')
        detalle = f['detalle']
        if len(detalle) > 28:
            detalle = detalle[:25] + "..."
        folder = f['folder']
        if len(folder) > 32:
            folder = "..." + folder[-29:]
        
        print(f"{i:<4} {size_str:<12} {riesgo:<12} {detalle:<30} {folder}")
    
    if len(all_no_ext) > 50:
        print(f"\n  ... y {len(all_no_ext) - 50} archivos mas")
    
    print()
    print("-" * 60)
    print(f"TOTAL: {len(all_no_ext)} archivos, {format_size(total_size)}")
    print()
    print("LEYENDA DE RIESGO:")
    print("  🔴 ALTO   -> Ejecutables, acceso denegado, binario desconocido")
    print("  🟡 MEDIO  -> Binarios comprimidos, sin firma conocida")
    print("  🟢 BAJO   -> Texto legible")
    print()
    
    # Agrupar por tipo detectado
    type_summary = defaultdict(lambda: {'count': 0, 'size': 0})
    for f in all_no_ext:
        type_summary[f['tipo']]['count'] += 1
        type_summary[f['tipo']]['size'] += f['size']
    
    print("=" * 60)
    print("RESUMEN POR TIPO DETECTADO:")
    print("=" * 60)
    print(f"{'Tipo':<20} {'Cantidad':>10} {'Tamano Total':>15}")
    print("-" * 45)
    
    for tipo, data in sorted(type_summary.items(), key=lambda x: x[1]['size'], reverse=True):
        riesgo = Riesgos.get(tipo, '⚪ DESCONOCIDO')
        detalle = f"{tipo} ({riesgo})"
        if len(detalle) > 18:
            detalle = detalle[:15] + "..."
        print(f"{detalle:<20} {data['count']:>10} {format_size(data['size']):>15}")
    
    print()

def main():
    """Función principal"""
    try:
        print_header()
        get_disk_space()
        analyze_all_folders_detailed()
        find_all_large_files()
        show_recommendations()
        find_files_without_extension()
        
        print("=" * 60)
        print("       ANALISIS COMPLETADO")
        print("=" * 60)
        print()
        
    except KeyboardInterrupt:
        print("\nAnalisis cancelado por el usuario.")
    except Exception as e:
        import traceback
        print(f"Error durante el analisis: {e}")
        traceback.print_exc()

if __name__ == "__main__":
    main()
