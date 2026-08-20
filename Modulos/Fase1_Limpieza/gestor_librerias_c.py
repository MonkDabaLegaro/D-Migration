# -*- coding: utf-8 -*-
import os
import shutil
import sys
from pathlib import Path

def get_size(path):
    total_size = 0
    if os.path.isfile(path):
        total_size = os.path.getsize(path)
    elif os.path.isdir(path):
        for dirpath, _, filenames in os.walk(path):
            for f in filenames:
                fp = os.path.join(dirpath, f)
                if not os.path.islink(fp):
                    try:
                        total_size += os.path.getsize(fp)
                    except OSError:
                        pass
    return total_size

def format_size(size_bytes):
    for unit in ['B', 'KB', 'MB', 'GB', 'TB']:
        if size_bytes < 1024.0:
            return f"{size_bytes:.2f} {unit}"
        size_bytes /= 1024.0

def main():
    print("============================================================")
    print("   GESTOR INTERACTIVO DE LIBRERIAS Y CACHES EN C:")
    print("============================================================\n")

    user_home = Path.home()
    
    # Rutas base a escanear
    base_paths = [
        user_home / ".cache",
        user_home / "AppData" / "Roaming" / "Python",
        user_home / "AppData" / "Local" / "Programs" / "Python",
        Path("C:/Program Files/Java"),
        Path("C:/Program Files (x86)/Java")
    ]
    
    items_to_review = []
    
    print("Escaneando directorios en busca de librerias y caches... (esto puede tardar unos segundos)")
    
    for base in base_paths:
        if base.exists() and base.is_dir():
            try:
                for sub_item in base.iterdir():
                    if sub_item.is_dir():
                        size = get_size(sub_item)
                        items_to_review.append((sub_item, size))
            except PermissionError:
                print(f"[!] Permiso denegado para leer {base}")

    # Ordenar por tamaño descendente
    items_to_review.sort(key=lambda x: x[1], reverse=True)
    
    if not items_to_review:
        print("No se encontraron carpetas sospechosas en las ubicaciones tipicas.")
        input("Presiona Enter para salir...")
        return
        
    print("\nResultados encontrados (ordenados por tamaño):\n")
    for i, (path, size) in enumerate(items_to_review, 1):
        print(f"[{i}] {format_size(size).rjust(10)} - {path}")
        
    print("\n------------------------------------------------------------")
    print("Escribe los numeros de las carpetas que deseas eliminar.")
    print("Puedes separar por comas (ejemplo: 1, 3, 5) o rangos (ejemplo: 1-3).")
    print("Presiona Enter sin escribir nada para cancelar y salir.")
    print("------------------------------------------------------------")
    
    choice = input("\nSeleccion: ").strip()
    
    if not choice:
        print("Operacion cancelada. Saliendo...")
        return
        
    to_delete_indices = set()
    parts = choice.split(',')
    
    try:
        for part in parts:
            part = part.strip()
            if '-' in part:
                start, end = map(int, part.split('-'))
                for j in range(start, end + 1):
                    to_delete_indices.add(j)
            else:
                if part.isdigit():
                    to_delete_indices.add(int(part))
    except ValueError:
        print("Entrada invalida. Saliendo...")
        return
        
    valid_indices = [idx for idx in to_delete_indices if 1 <= idx <= len(items_to_review)]
    
    if not valid_indices:
        print("Ninguna opcion valida seleccionada. Saliendo...")
        return
        
    print("\nSe eliminaran las siguientes carpetas:")
    for idx in valid_indices:
        path, size = items_to_review[idx - 1]
        print(f" - {path} ({format_size(size)})")
        
    confirm = input("\nEstas COMPLETAMENTE SEGURO? (Escribe 'SI' para proceder): ").strip()
    
    if confirm.upper() == "SI":
        print("\nEliminando...")
        for idx in valid_indices:
            path, _ = items_to_review[idx - 1]
            print(f"Borrando {path}...")
            try:
                shutil.rmtree(path)
            except Exception as e:
                print(f"[ERROR] No se pudo borrar {path}: {e}")
        print("\nLimpieza completada.")
    else:
        print("\nOperacion cancelada.")
        
    input("\nPresiona Enter para continuar...")

if __name__ == "__main__":
    main()
