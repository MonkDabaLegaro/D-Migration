# -*- coding: utf-8 -*-
"""
Analisis y movimiento de archivos .MP4 al disco D
Busca archivos MP4 en disco C y los mueve a Descargas de disco D
"""

import os
import sys
import time
import shutil
from pathlib import Path
from collections import defaultdict

def clear_line():
    sys.stdout.write('\r' + ' ' * 100 + '\r')
    sys.stdout.flush()

def print_progress_bar(percent, prefix='', suffix='', bar_length=40):
    filled = int(bar_length * percent / 100)
    bar = '█' * filled + '░' * (bar_length - filled)
    sys.stdout.write(f"\r[{bar}] {percent:3d}% {prefix} {suffix}")
    sys.stdout.flush()

def format_size(size_bytes):
    for unit in ['B', 'KB', 'MB', 'GB', 'TB']:
        if size_bytes < 1024.0:
            return f"{size_bytes:.2f} {unit}"
        size_bytes /= 1024.0
    return f"{size_bytes:.2f} PB"

def print_header():
    print("=" * 70)
    print("     ANALISIS Y MOVIMIENTO DE ARCHIVOS .MP4")
    print("=" * 70)
    print()

def get_disk_space():
    print("[1] ESPACIO EN DISCOS C Y D")
    print("-" * 70)
    
    disks = ['C', 'D']
    disk_info = {}
    
    for disk in disks:
        try:
            result = os.popen(f'wmic logicaldisk where "DeviceID=\'{disk}:\'" get Size,FreeSpace /value').read()
            lines = result.strip().split('\n')
            free_space = 0
            total_size = 0
            
            for line in lines:
                if 'FreeSpace' in line:
                    free_space = int(line.split('=')[1].strip())
                elif 'Size' in line and 'FreeSpace' not in line:
                    total_size = int(line.split('=')[1].strip())
            
            if total_size > 0:
                used = total_size - free_space
                disk_info[disk] = {
                    'total': total_size,
                    'free': free_space,
                    'used': used
                }
                print(f"  Disco {disk}:")
                print(f"    Total:    {format_size(total_size)}")
                print(f"    Libre:    {format_size(free_space)}")
                print(f"    Usado:    {format_size(used)}")
                print(f"    Uso:      {(used/total_size)*100:.1f}%")
                print()
        except Exception as e:
            print(f"  Error en disco {disk}: {e}")
    
    return disk_info

def detect_downloads_folder():
    return r"D:\Descargas"

def find_all_mp4():
    """Busca TODOS los archivos .MP4 en el disco C"""
    print("[2] BUSQUEDA DE ARCHIVOS .MP4")
    print("=" * 70)
    
    search_paths = [f"C:\\Users\\{os.getenv('USERNAME', 'Usuario')}"]
    
    print("Buscando archivos .MP4 en el disco C...")
    print()
    
    all_mp4 = []
    
    for search_path in search_paths:
        if not os.path.exists(search_path):
            continue
            
        for dirpath, dirnames, filenames in os.walk(search_path):
            for filename in filenames:
                if filename.lower().endswith('.mp4'):
                    filepath = os.path.join(dirpath, filename)
                    try:
                        size = os.path.getsize(filepath)
                        all_mp4.append({
                            'path': filepath,
                            'size': size,
                            'name': filename,
                            'folder': os.path.dirname(filepath)
                        })
                    except (OSError, FileNotFoundError):
                        pass
    
    if not all_mp4:
        print("  🟢 No se encontraron archivos .MP4")
        print()
        return None
    
    all_mp4.sort(key=lambda x: x['size'], reverse=True)
    
    total_size = sum(f['size'] for f in all_mp4)
    
    print(f"  ✓ Encontrados {len(all_mp4)} archivos .MP4")
    print(f"  Tamaño total: {format_size(total_size)}")
    print()
    
    print("=" * 70)
    print("ARCHIVOS .MP4 ENCONTRADOS (ordenados por tamaño):")
    print("=" * 70)
    print()
    
    print(f"{'#':<4} {'Tamano':<15} {'Nombre':<30} {'Carpeta original'}")
    print("-" * 80)
    
    for i, f in enumerate(all_mp4[:50], 1):
        size_str = format_size(f['size'])
        name = f['name']
        if len(name) > 28:
            name = name[:25] + "..."
        folder = f['folder']
        if len(folder) > 32:
            folder = "..." + folder[-29:]
        print(f"{i:<4} {size_str:<15} {name:<30} {folder}")
    
    if len(all_mp4) > 50:
        print(f"\n  ... y {len(all_mp4) - 50} archivos mas")
    
    print()
    print("-" * 70)
    print(f"TOTAL: {len(all_mp4)} archivos, {format_size(total_size)}")
    print()
    
    return all_mp4

def move_mp4_files(all_mp4):
    """Mueve archivos .MP4 a Descargas del disco D"""
    if not all_mp4:
        return
    
    downloads = detect_downloads_folder()
    
    print()
    print("=" * 70)
    print("CONFIGURACION DE MOVIMIENTO")
    print("=" * 70)
    print()
    print(f"  Origen:   Disco C - Archivos .MP4 encontrados")
    print(f"  Destino:  {downloads}")
    print()
    
    if not os.path.exists(downloads):
        print(f"  ADVERTENCIA: La carpeta destino no existe.")
        crear = input("  Deseas crearla? (S/N): ").strip().upper()
        if crear == 'S':
            try:
                os.makedirs(downloads, exist_ok=True)
                print(f"  Carpeta creada: {downloads}")
            except Exception as e:
                print(f"  Error creando carpeta: {e}")
                return
        else:
            print("  Cancelado.")
            return
    
    disk_info = get_disk_space()
    if 'D' in disk_info:
        free_d = disk_info['D']['free']
        total_mp4 = sum(f['size'] for f in all_mp4)
        if free_d < total_mp4:
            print(f"\n  ADVERTENCIA: Espacio insuficiente en disco D.")
            print(f"  Espacio libre: {format_size(free_d)}")
            print(f"  Espacio necesario: {format_size(total_mp4)}")
            confirm = input("  Continuar de todas formas? (S/N): ").strip().upper()
            if confirm != 'S':
                print("  Cancelado.")
                return
    
    while True:
        print()
        print("[3] OPCIONES DE MOVIMIENTO")
        print("-" * 70)
        print()
        print("  1) Mover TODOS los archivos .MP4")
        print("  2) Mover solo archivos GRANDES (mayores a 500 MB)")
        print("  3) Mover un archivo ESPECIFICO")
        print("  4) Mover archivos de una CARPETA especifica")
        print("  5) Cancelar")
        print()
        
        opcion = input("  Elige una opcion (1-5): ").strip()
        
        if opcion == '1':
            files_to_move = all_mp4
            break
        elif opcion == '2':
            files_to_move = [f for f in all_mp4 if f['size'] > 500*1024*1024]
            if not files_to_move:
                print("\n  No hay archivos mayores a 500 MB.")
                continue
            print(f"\n  Se moveran {len(files_to_move)} archivos")
            confirm = input("  Continuar? (S/N): ").strip().upper()
            if confirm == 'S':
                break
            else:
                continue
        elif opcion == '3':
            print("\n  Archivos disponibles (primeros 20):")
            for i, f in enumerate(all_mp4[:20], 1):
                print(f"    {i}) {f['name']} ({format_size(f['size'])})")
            try:
                num = int(input("\n  Numero de archivo a mover: ").strip())
                if 1 <= num <= min(20, len(all_mp4)):
                    files_to_move = [all_mp4[num - 1]]
                    break
                else:
                    print("  Numero invalido.")
            except ValueError:
                print("  Entrada invalida.")
        elif opcion == '4':
            folder = input("\n  Escribe parte de la ruta o nombre de carpeta: ").strip()
            files_to_move = [f for f in all_mp4 if folder.lower() in f['folder'].lower()]
            if not files_to_move:
                print(f"\n  No se encontraron archivos en carpetas que contengan '{folder}'")
                continue
            print(f"\n  Se moveran {len(files_to_move)} archivos")
            confirm = input("  Continuar? (S/N): ").strip().upper()
            if confirm == 'S':
                break
            else:
                continue
        elif opcion == '5':
            print("\n  Cancelado.")
            return
        else:
            print("\n  Opcion no valida.")
    
    if not files_to_move:
        print("\n  No hay archivos para mover.")
        return
    
    confirm = input(f"\n  ADVERTENCIA: Se moveran {len(files_to_move)} archivos a:\n  {downloads}\n  Escribe 'CONFIRMAR' para continuar: ").strip()
    
    if confirm != 'CONFIRMAR':
        print("  Cancelado.")
        return
    
    moved = 0
    errors = 0
    skipped = 0
    total_freed = 0
    
    print("\n  Moviendo archivos...")
    print()
    
    for i, f in enumerate(files_to_move, 1):
        percent = int((i / len(files_to_move)) * 100)
        print_progress_bar(percent, prefix=f"Moviendo", suffix=f"({i}/{len(files_to_move)})")
        
        dest_path = os.path.join(downloads, f['name'])
        
        try:
            # Si ya existe en destino, agregar numero
            if os.path.exists(dest_path):
                base, ext = os.path.splitext(f['name'])
                counter = 1
                while os.path.exists(os.path.join(downloads, f"{base}_{counter}{ext}")):
                    counter += 1
                dest_path = os.path.join(downloads, f"{base}_{counter}{ext}")
            
            shutil.move(f['path'], dest_path)
            moved += 1
            total_freed += f['size']
            clear_line()
            
        except Exception as e:
            errors += 1
            print(f"\n  ✗ Error moviendo {f['name']}: {e}")
    
    print()
    print("=" * 70)
    print("RESULTADO:")
    print("=" * 70)
    print()
    print(f"  ✓ Movidos correctamente: {moved} archivos")
    if skipped:
        print(f"  - Omitidos (ya existian): {skipped} archivos")
    if errors:
        print(f"  ✗ Errores: {errors} archivos")
    print(f"  Espacio movido de C a D: {format_size(total_freed)}")
    print()

def main():
    try:
        print_header()
        disk_space = get_disk_space()
        mp4_files = find_all_mp4()
        
        if mp4_files:
            move_mp4_files(mp4_files)
        
        print()
        print("=" * 70)
        print("     FIN DEL PROCESO")
        print("=" * 70)
        print()
        
    except KeyboardInterrupt:
        print("\n\nCancelado por el usuario.")
    except Exception as e:
        import traceback
        print(f"Error durante el proceso: {e}")
        traceback.print_exc()

if __name__ == "__main__":
    main()
