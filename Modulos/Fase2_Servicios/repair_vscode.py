import os
import sys
import shutil
import logging

# Configurar logging
logging.basicConfig(level=logging.INFO, format='[%(levelname)s] %(message)s')

def repair_vscode(vscode_path):
    if not os.path.isdir(vscode_path):
        logging.error(f"La ruta de VS Code no existe o no es una carpeta: {vscode_path}")
        return False

    logging.info(f"Analizando instalacion de VS Code en: {vscode_path}")
    
    # Comprobar si falta la carpeta 'resources' en la raiz (signo principal de instalacion rota)
    resources_path = os.path.join(vscode_path, 'resources')
    if os.path.isdir(resources_path):
        logging.info("La instalacion de VS Code parece estar intacta (la carpeta 'resources' existe). No se requiere reparacion.")
        return True

    logging.warning("Falta la carpeta 'resources' en la raiz. Buscando restos de actualizaciones interrumpidas...")
    
    # Buscar una carpeta huerfana de actualizacion (suelen ser un hash corto o tener archivos clave)
    update_dir = None
    for item in os.listdir(vscode_path):
        item_path = os.path.join(vscode_path, item)
        if os.path.isdir(item_path) and item not in ['bin', 'data', 'extensions', 'App']:
            # Verificar si dentro de esta carpeta esta 'resources'
            if os.path.isdir(os.path.join(item_path, 'resources')):
                update_dir = item_path
                break
    
    if not update_dir:
        logging.error("No se encontro ninguna carpeta de actualizacion que contenga 'resources'. La instalacion esta severamente danada.")
        return False
        
    logging.info(f"Se encontro una actualizacion interrumpida en: {update_dir}")
    logging.info("Iniciando fusion de archivos hacia el directorio principal...")
    
    # Mover todo desde update_dir hacia vscode_path
    items_to_move = os.listdir(update_dir)
    success_count = 0
    
    for item in items_to_move:
        src = os.path.join(update_dir, item)
        dst = os.path.join(vscode_path, item)
        
        try:
            # Si el destino existe, eliminarlo primero (por ejemplo, reemplazar un .dll viejo si quedo atascado)
            if os.path.exists(dst):
                if os.path.isdir(dst):
                    shutil.rmtree(dst)
                else:
                    os.remove(dst)
                    
            shutil.move(src, dst)
            logging.info(f"Movido: {item}")
            success_count += 1
        except Exception as e:
            logging.error(f"Error moviendo {item}: {e}")
            
    # Intentar eliminar la carpeta de actualizacion huerfana
    try:
        if not os.listdir(update_dir):
            os.rmdir(update_dir)
            logging.info("Se ha limpiado la carpeta de actualizacion temporal.")
    except Exception as e:
        logging.warning(f"No se pudo eliminar la carpeta de actualizacion temporal: {e}")

    logging.info(f"Reparacion completada. Se movieron {success_count} elementos.")
    return True

if __name__ == "__main__":
    if len(sys.argv) < 2:
        logging.error("Uso: python repair_vscode.py <ruta_a_vscode>")
        sys.exit(1)
        
    target_path = sys.argv[1]
    success = repair_vscode(target_path)
    if not success:
        sys.exit(1)
