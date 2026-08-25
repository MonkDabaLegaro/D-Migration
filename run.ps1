$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error 'D-Migration requiere .NET 10 SDK. Instálalo desde https://dotnet.microsoft.com/download/dotnet/10.0 y vuelve a ejecutar este script.'
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\DMigration.Cli\DMigration.Cli.csproj'

Write-Host 'D-Migration - iniciando CLI modular...' -ForegroundColor Cyan
& dotnet run --project $project -- @args
exit $LASTEXITCODE
