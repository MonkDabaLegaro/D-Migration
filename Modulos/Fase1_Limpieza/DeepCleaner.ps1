<#
.SYNOPSIS
    DeepCleaner - Script avanzado para limpiar el disco C: y migrar componentes a D:
#>

# 1. Auto-Elevacion
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[INFO] Solicitando permisos de Administrador..." -ForegroundColor Yellow
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

# Configuracion de Rutas
$LogFile = "D:\Descargas\MigracionDesarrollo\logs\deep_cleaner_audit.log"
$DestRoot = "D:\Componentes"
$DestPersonal = "D:\Personal"

# Categorias y Listas (Nombres de carpetas a buscar)
$SystemWhitelist = @("Microsoft", "Windows", "Packages", "Temp", "Programs", "History", "CrashDumps", "SystemCertificates", "ConnectedDevicesPlatform", "Comms", "Archivos temporales de Internet", "Historial", "Publishers", "VirtualStore")
$GamesList = @("Epic Games", "EpicGamesLauncher", "Riot Games", "Steam", "Bluestacks", "BlueStacks X", "BlueStacksSetup", "HD-Player", "HD-MultiInstanceManager", "TslGame", "VALORANT", "UnrealEngine", "UnrealEngineLauncher", ".minecraft", "curseforge", "Goldberg SteamEmu Saves", "GSE Saves", "Riot Client", "riot-client-ux", "Godot", "Sloppy_Fields", "Princess_Savior", "Magical_Monstergirls_Academy")
$DevList = @("Docker", "Docker Desktop", "docker-secrets-engine", "npm", "npm-cache", "pnpm", "pnpm-cache", "yarn", "node-gyp", "uv", "pip", "pypa", "CMakeTools", "GitHubDesktop", "GitHub Desktop", "jupyter", "ollama", ".bun", ".docker", ".npm", ".virtualenvs", ".nuget", ".android", ".antigravity", ".antigravity-ide", ".continue", ".copilot", "biomejs", "ms-playwright-go")
$CacheList = @("cache", "D3DSCache", "CEF", ".cache", "app_shell_cache_8311")
$PersonalFolders = @("Documents", "Downloads", "Pictures", "Videos", "Music", "Desktop")

# Inicializar Log
function Write-Log {
    param([string]$Message, [string]$Color = "White")
    $TimeStamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $LogMessage = "[$TimeStamp] $Message"
    Add-Content -Path $LogFile -Value $LogMessage
    Write-Host $LogMessage -ForegroundColor $Color
}

New-Item -ItemType Directory -Force -Path "D:\Descargas\MigracionDesarrollo\logs" | Out-Null
Set-Content -Path $LogFile -Value "=== INICIO DE DEEP CLEANER ==="

Write-Log "Iniciando analisis de directorios..." "Cyan"

# Funcion Principal de Migracion
function Migrate-Folder {
    param([string]$SourcePath, [string]$DestPath, [string]$Category)
    
    if (-not (Test-Path $SourcePath)) { return }
    
    # Comprobar si ya es un ReparsePoint (Junction)
    $isJunction = (Get-Item $SourcePath -Force).Attributes -match "ReparsePoint"
    if ($isJunction) {
        Write-Log "[OMITIDO] [$Category] $SourcePath (Ya es un Enlace Simbolico)" "DarkGray"
        return
    }

    Write-Log "[MIGRANDO] [$Category] Moviendo $SourcePath a $DestPath..." "Yellow"
    
    # Crear destino
    New-Item -ItemType Directory -Force -Path $DestPath | Out-Null
    
    # Mover usando robocopy
    $roboArgs = @("`"$SourcePath`"", "`"$DestPath`"", "/E", "/MOVE", "/R:3", "/W:2", "/MT:8")
    $roboProcess = Start-Process -FilePath "robocopy" -ArgumentList $roboArgs -Wait -NoNewWindow -PassThru
    
    # Forzar borrado del origen
    if (Test-Path $SourcePath) {
        Remove-Item -Path $SourcePath -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    # Crear Enlace
    if (-not (Test-Path $SourcePath)) {
        $mklinkProc = Start-Process -FilePath "cmd.exe" -ArgumentList "/c mklink /J `"$SourcePath`" `"$DestPath`"" -Wait -NoNewWindow -PassThru
        Write-Log "  -> [EXITO] Enlace creado en C:" "Green"
    } else {
        Write-Log "  -> [ERROR] No se pudo limpiar la carpeta origen." "Red"
    }
}

# 1. Escanear AppData (Local y Roaming)
$ScanPaths = @("$env:LOCALAPPDATA", "$env:APPDATA", "$env:USERPROFILE")

foreach ($RootPath in $ScanPaths) {
    Write-Log "Escaneando: $RootPath" "Magenta"
    $SubDirs = Get-ChildItem -Path $RootPath -Directory -Force -ErrorAction SilentlyContinue
    
    foreach ($Dir in $SubDirs) {
        $Name = $Dir.Name
        $FullPath = $Dir.FullName
        
        # Omitir AppData si estamos en UserProfile
        if ($Name -eq "AppData" -or $Name -eq "Local Settings" -or $Name -eq "Application Data") { continue }
        
        # Categorizar
        if ($SystemWhitelist -contains $Name) {
            Write-Log "[SISTEMA] $FullPath (Intocable)" "DarkCyan"
        } elseif ($GamesList -contains $Name) {
            Migrate-Folder -SourcePath $FullPath -DestPath "$DestRoot\Aplicaciones_Juegos\$Name" -Category "JUEGOS"
        } elseif ($DevList -contains $Name) {
            Migrate-Folder -SourcePath $FullPath -DestPath "$DestRoot\Desarrollo\$Name" -Category "DESARROLLO"
        } elseif ($CacheList -contains $Name) {
            Migrate-Folder -SourcePath $FullPath -DestPath "$DestRoot\Caches\$Name" -Category "CACHES"
        } elseif ($PersonalFolders -contains $Name) {
            Migrate-Folder -SourcePath $FullPath -DestPath "$DestPersonal\$Name" -Category "PERSONAL"
        } else {
            # Carpetas desconocidas se quedan en C: para no romper nada, pero las registramos.
            Write-Log "[DESCONOCIDO/SEGURO] $FullPath (Mantenida en C: por seguridad)" "Gray"
        }
    }
}

Write-Log "=== LIMPIEZA FINALIZADA ===" "Cyan"
Write-Host "Presiona cualquier tecla para salir..."
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
