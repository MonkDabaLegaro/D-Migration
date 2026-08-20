<#
.SYNOPSIS
    Migrador Inteligente de Aplicaciones Pesadas (Winget)
#>
Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "   FASE 3: MIGRACION DE SOFTWARE PESADO (WINGET)" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

$Confirm = Read-Host "¿Deseas escanear tu sistema en busca de software pesado para migrar a D:? [S/N]"
if ($Confirm -notmatch "^[Ss]") {
    Write-Host "[OMITIDO] Saltando Fase 3..." -ForegroundColor Gray
    exit
}

Write-Host "[INFO] Cargando lista de programas instalados (esto puede tardar unos segundos)..." -ForegroundColor Yellow
$InstalledApps = winget list --accept-source-agreements | Select-String -Pattern "BlueStacks|Epic Games|Riot|Vanguard|Docker|Discord|OBS|Office|EaseUS|MuseHub|TikTok"

if (-not $InstalledApps) {
    Write-Host "[OK] No se encontraron aplicaciones pesadas reconocidas en C:" -ForegroundColor Green
    exit
}

Write-Host "`nSoftware Pesado Detectado:" -ForegroundColor Cyan
$InstalledApps | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
Write-Host ""

$Proceed = Read-Host "¿Deseas intentar migrar estas aplicaciones a D:\Componentes\Aplicaciones automáticamente? (ATENCION: Se desinstalarán y se volverán a descargar) [S/N]"

if ($Proceed -match "^[Ss]") {
    Write-Host "`n[ADVERTENCIA] Función de migración automatizada por Winget." -ForegroundColor Red
    Write-Host "Dado que cada instalador es diferente, Winget intentará forzar la ruta, pero algunos programas (como Office o Riot) ignoran la ruta y se instalan en C: de todos modos." -ForegroundColor Yellow
    Write-Host "Para una migración 100% segura, se recomienda desinstalar manualmente desde el Panel de Control y usar el instalador web oficial eligiendo la ruta 'D:'." -ForegroundColor Yellow
    $FinalConfirm = Read-Host "¿Continuar de todos modos con la automatización experimental? [S/N]"
    
    if ($FinalConfirm -match "^[Ss]") {
        # Lista de IDs comunes para automatizar
        $AppIds = @("BlueStacks.BlueStacks", "EpicGames.EpicGamesLauncher", "Discord.Discord", "OBSProject.OBSStudio")
        foreach ($App in $AppIds) {
            Write-Host "`n[WINGET] Procesando: $App..." -ForegroundColor Cyan
            Write-Host "  -> Desinstalando..."
            winget uninstall --id $App --silent --accept-source-agreements
            Write-Host "  -> Reinstalando en D:\Componentes\Aplicaciones..."
            winget install --id $App --location "D:\Componentes\Aplicaciones\$App" --silent --accept-source-agreements
            Add-Content -Path "D:\Descargas\MigracionDesarrollo\logs\pipeline_master.log" -Value "[$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))] Winget procesó $App"
        }
        Write-Host "`n[EXITO] Migración automatizada completada." -ForegroundColor Green
        Add-Content -Path "D:\Descargas\MigracionDesarrollo\logs\pipeline_master.log" -Value "[$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))] Migración automatizada completada."
    } else {
        Write-Host "[OMITIDO] Migración automatizada cancelada." -ForegroundColor Gray
        Add-Content -Path "D:\Descargas\MigracionDesarrollo\logs\pipeline_master.log" -Value "[$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))] Migración automatizada cancelada por el usuario."
    }
} else {
    Write-Host "[OMITIDO] Saltando reinstalación automática." -ForegroundColor Gray
    Add-Content -Path "D:\Descargas\MigracionDesarrollo\logs\pipeline_master.log" -Value "[$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))] Saltando reinstalación automática de Winget."
}
