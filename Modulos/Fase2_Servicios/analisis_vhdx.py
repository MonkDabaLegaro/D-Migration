# -*- coding: utf-8 -*-
"""
Analisis y limpieza de archivos .VHDX (Virtual Hard Disks)
Busca y muestra archivos VHDX grandes y permite eliminarlos de forma segura.
"""

import os
import sys
import time
import ctypes
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
    print("     ANALISIS DE ARCHIVOS .VHDX (Virtual Hard Disks)")
    print("=" * 70)
    print()

def get_disk_space():
    print("[1] ESPACIO TOTAL Y LIBRE EN EL DISCO C")
    print("-" * 70)
    
    try:
        result = os.popen('wmic logicaldisk where "DeviceID=\'C:\'" get Size,FreeSpace /value').read()
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
            print(f"  Espacio total:    {format_size(total_size)}")
            print(f"  Espacio libre:    {format_size(free_space)}")
            print(f"  Espacio usado:    {format_size(used)}")
            print(f"  Porcentaje usado: {(used/total_size)*100:.1f}%")
            
    except Exception as e:
        print(f"Error al obtener espacio del disco: {e}")
    print()

def find_all_vhdx():
    """Busca TODOS los archivos .VHDX"""
    print("[2] BUSQUEDA DE ARCHIVOS .VHDX")
    print("=" * 70)
    
    search_paths = [f"C:\\Users\\{os.getenv('USERNAME', 'Usuario')}", "C:\\Users\\Public"]
    
    print("Buscando archivos .VHDX en el sistema...")
    print()
    
    all_vhdx = []
    
    for search_path in search_paths:
        if not os.path.exists(search_path):
            continue
            
        for dirpath, dirnames, filenames in os.walk(search_path):
            for filename in filenames:
                if filename.lower().endswith('.vhdx'):
                    filepath = os.path.join(dirpath, filename)
                    try:
                        size = os.path.getsize(filepath)
                        all_vhdx.append({
                            'path': filepath,
                            'size': size,
                            'name': filename,
                            'folder': os.path.dirname(filepath)
                        })
                    except (OSError, FileNotFoundError):
                        pass
    
    if not all_vhdx:
        print("  🟢 No se encontraron archivos .VHDX")
        print()
        return
    
    all_vhdx.sort(key=lambda x: x['size'], reverse=True)
    
    total_size = sum(f['size'] for f in all_vhdx)
    
    print(f"  ✓ Encontrados {len(all_vhdx)} archivos .VHDX")
    print(f"  Tamaño total: {format_size(total_size)}")
    print()
    
    print("=" * 70)
    print("ARCHIVOS .VHDX ENCONTRADOS (ordenados por tamaño):")
    print("=" * 70)
    print()
    
    print(f"{'#':<4} {'Tamano':<15} {'Nombre':<30} {'Carpeta'}")
    print("-" * 80)
    
    for i, f in enumerate(all_vhdx, 1):
        size_str = format_size(f['size'])
        name = f['name']
        if len(name) > 28:
            name = name[:25] + "..."
        folder = f['folder']
        if len(folder) > 30:
            folder = "..." + folder[-27:]
        print(f"{i:<4} {size_str:<15} {name:<30} {folder}")
    
    print()
    print("-" * 70)
    print(f"TOTAL: {len(all_vhdx)} archivos, {format_size(total_size)}")
    print()
    
    return all_vhdx

def remove_vhdx_files(all_vhdx):
    """Elimina archivos .VHDX seleccionados"""
    if not all_vhdx:
        return
    
    print()
    print("=" * 70)
    print("OPCIONES DE ELIMINACION")
    print("=" * 70)
    print()
    print("  Puedes eliminar archivos de varias formas:")
    print()
    
    while True:
        print("  1) Eliminar TODOS los archivos .VHDX")
        print("  2) Eliminar solo archivos GRANDES (mayores a 1 GB)")
        print("  3) Eliminar un archivo ESPECIFICO")
        print("  4) Salir sin eliminar nada")
        print()
        
        opcion = input("  Elige una opcion (1-4): ").strip()
        
        if opcion == '1':
            confirm = input(f"\n  ADVERTENCIA: Vas a eliminar {len(all_vhdx)} archivos.\n  Escribe 'CONFIRMAR' para continuar: ").strip()
            if confirm == 'CONFIRMAR':
                delete_files([f['path'] for f in all_vhdx])
            else:
                print("  Cancelado.")
            break
            
        elif opcion == '2':
            grandes = [f for f in all_vhdx if f['size'] > 1*1024**3]
            if not grandes:
                print("\n  No hay archivos mayores a 1 GB.")
                continue
            total = sum(f['size'] for f in grandes)
            print(f"\n  Se eliminaran {len(grandes)} archivos (total {format_size(total)})")
            confirm = input("  Escribe 'CONFIRMAR' para continuar: ").strip()
            if confirm == 'CONFIRMAR':
                delete_files([f['path'] for f in grandes])
            else:
                print("  Cancelado.")
            break
            
        elif opcion == '3':
            print("\n  Archivos disponibles:")
            for i, f in enumerate(all_vhdx, 1):
                print(f"    {i}) {f['name']} ({format_size(f['size'])})")
            
            try:
                num = int(input("\n  Numero de archivo a eliminar: ").strip())
                if 1 <= num <= len(all_vhdx):
                    archivo = all_vhdx[num - 1]
                    confirm = input(f"\n  Vas a eliminar:\n    {archivo['path']}\n  Escribe 'CONFIRMAR' para continuar: ").strip()
                    if confirm == 'CONFIRMAR':
                        delete_files([archivo['path']])
                    else:
                        print("  Cancelado.")
                else:
                    print("  Numero invalido.")
            except ValueError:
                print("  Entrada invalida.")
            break
            
        elif opcion == '4':
            print("\n  Saliendo sin cambios...")
            break
        else:
            print("\n  Opcion no valida. Intenta de nuevo.")

def delete_files(paths):
    """Elimina archivos con seguimiento de progreso"""
    deleted = 0
    errors = 0
    total_freed = 0
    
    print("\n  Eliminando archivos...")
    
    for i, path in enumerate(paths, 1):
        percent = int((i / len(paths)) * 100)
        print_progress_bar(percent, prefix=f"Eliminando", suffix=f"({i}/{len(paths)})")
        time.sleep(0.05)
        
        try:
            size = os.path.getsize(path)
            os.remove(path)
            deleted += 1
            total_freed += size
            clear_line()
        except Exception as e:
            errors += 1
            print(f"\n  ✗ Error eliminando {path}: {e}")
    
    print()
    print(f"  ✓ Eliminados: {deleted} archivos")
    if errors:
        print(f"  ✗ Errores: {errors}")
    print(f"  Espacio liberado: {format_size(total_freed)}")

def main():
    try:
        print_header()
        get_disk_space()
        vhdx_files = find_all_vhdx()
        
        if vhdx_files:
            remove_vhdx_files(vhdx_files)
        
        print()
        print("=" * 70)
        print("     FIN DEL ANALISIS")
        print("=" * 70)
        print()
        
    except KeyboardInterrupt:
        print("\n\nCancelado por el usuario.")
    except Exception as e:
        import traceback
        print(f"Error durante el analisis: {e}")
        traceback.print_exc()

if __name__ == "__main__":
    main()
