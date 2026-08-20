<#
.SYNOPSIS
    Pipeline Maestro: Orquestador Lineal de Migración y Limpieza (C: a D:)
#>
$ErrorActionPreference = "SilentlyContinue"

# 1. Auto-Elevacion
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "[INFO] Solicitando permisos de Administrador..." -ForegroundColor Yellow
    Start-Process powershell.exe "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$GlobalLog = "D:\Descargas\MigracionDesarrollo\logs\pipeline_master.log"

function Write-MasterLog {
    param([string]$Msg)
    $TimeStamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    Add-Content -Path $GlobalLog -Value "[$TimeStamp] $Msg"
}

Write-MasterLog "=== NUEVA EJECUCION DEL PIPELINE MAESTRO ==="
Clear-Host
Write-Host "=================================================================" -ForegroundColor Magenta
Write-Host "          PIPELINE MAESTRO: MIGRACION Y LIMPIEZA TOTAL           " -ForegroundColor Magenta
Write-Host "=================================================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "Este programa te guiará paso a paso por 4 Fases de optimización."
Write-Host "En cada fase podrás decidir si deseas ejecutarla o saltarla."
Write-Host ""
$Start = Read-Host "¿Comenzar el proceso? [S/N]"
if ($Start -notmatch "^[Ss]") { exit }

$BaseDir = $PSScriptRoot
$ModsDir = "$BaseDir\Modulos"

# ==========================================
# FASE 1: Limpieza Profunda
# ==========================================
Write-Host "`n-----------------------------------------------------------" -ForegroundColor Cyan
Write-Host " FASE 1: Limpieza Profunda (AppData y Entornos Dev)" -ForegroundColor Cyan
Write-Host "-----------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Esto ejecutará el DeepCleaner para vaciar cachés y migrar perfiles de Node/Python."
$Fase1 = Read-Host "¿Ejecutar Fase 1? [S/N]"
if ($Fase1 -match "^[Ss]") {
    if (Test-Path "$ModsDir\Fase1_Limpieza\DeepCleaner.ps1") {
        & "$ModsDir\Fase1_Limpieza\DeepCleaner.ps1"
        Write-MasterLog "Fase 1 (Limpieza Profunda) ejecutada exitosamente."
    } else {
        Write-Host "[ERROR] Script no encontrado." -ForegroundColor Red
        Write-MasterLog "ERROR: Script Fase 1 no encontrado."
    }
} else {
    Write-Host "[OMITIDO] Fase 1 saltada." -ForegroundColor Gray
    Write-MasterLog "Fase 1 omitida por el usuario."
}

# ==========================================
# FASE 2: Servicios Críticos
# ==========================================
Write-Host "`n-----------------------------------------------------------" -ForegroundColor Cyan
Write-Host " FASE 2: Servicios Críticos y VHDX (Docker, MongoDB, etc)" -ForegroundColor Cyan
Write-Host "-----------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Mueve archivos protegidos o bloqueados. ASEGURATE DE QUE ESTEN CERRADOS."
$Fase2 = Read-Host "¿Ejecutar Fase 2? [S/N]"
if ($Fase2 -match "^[Ss]") {
    if (Test-Path "$ModsDir\Fase2_Servicios\move_components.bat") {
        Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$ModsDir\Fase2_Servicios\move_components.bat`"" -Wait
    }
    # Ejecutar scripts viejos de VHDX si existen
    $vhdxScripts = Get-ChildItem "$ModsDir\Fase2_Servicios" -Filter "*vhdx*.bat"
    foreach ($script in $vhdxScripts) {
        $r = Read-Host "¿Ejecutar script heredado: $($script.Name)? [S/N]"
        if ($r -match "^[Ss]") { 
            Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$($script.FullName)`"" -Wait
            Write-MasterLog "Script heredado ejecutado: $($script.Name)"
        }
    }
    Write-MasterLog "Fase 2 (Servicios Criticos) ejecutada exitosamente."
} else {
    Write-Host "[OMITIDO] Fase 2 saltada." -ForegroundColor Gray
    Write-MasterLog "Fase 2 omitida por el usuario."
}

# ==========================================
# FASE 3: Software Pesado (Winget)
# ==========================================
# Llamar al script MigradorWinget.ps1
if (Test-Path "$ModsDir\Fase3_Software\MigradorWinget.ps1") {
    Write-MasterLog "Iniciando Fase 3 (Winget Software)..."
    & "$ModsDir\Fase3_Software\MigradorWinget.ps1"
    Write-MasterLog "Fase 3 finalizada."
} else {
    Write-MasterLog "ERROR: Script de Fase 3 no encontrado."
}

# ==========================================
# FASE 4: Multimedia y Modelos IA
# ==========================================
Write-Host "`n-----------------------------------------------------------" -ForegroundColor Cyan
Write-Host " FASE 4: Multimedia y Modelos de Inteligencia Artificial" -ForegroundColor Cyan
Write-Host "-----------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Limpiará cachés de HuggingFace y organizará archivos MP4."
$Fase4 = Read-Host "¿Ejecutar Fase 4? [S/N]"
if ($Fase4 -match "^[Ss]") {
    $iaScripts = Get-ChildItem "$ModsDir\Fase4_Multimedia_IA" -Filter "*.bat"
    foreach ($script in $iaScripts) {
        $r = Read-Host "¿Ejecutar organizador: $($script.Name)? [S/N]"
        if ($r -match "^[Ss]") { 
            Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$($script.FullName)`"" -Wait 
            Write-MasterLog "Script IA/Multimedia ejecutado: $($script.Name)"
        }
    }
    Write-MasterLog "Fase 4 (Multimedia e IA) ejecutada exitosamente."
} else {
    Write-Host "[OMITIDO] Fase 4 saltada." -ForegroundColor Gray
    Write-MasterLog "Fase 4 omitida por el usuario."
}

Write-Host "`n=================================================================" -ForegroundColor Magenta
Write-Host "                  PIPELINE COMPLETADO CON EXITO                  " -ForegroundColor Magenta
Write-Host "=================================================================" -ForegroundColor Magenta
Write-Host "Todo el flujo de trabajo ha finalizado. Presiona cualquier tecla para salir."
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
