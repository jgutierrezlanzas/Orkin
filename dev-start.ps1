#Requires -Version 7.0
<#
.SYNOPSIS
    Levanta el entorno de desarrollo de ORKIN Cotizador.

.DESCRIPTION
    Levanta SQL Server y la API con docker compose, y luego arranca la web
    localmente para poder depurarla.

    - SQL Server + API → Docker (docker-compose.yml)
    - Web (Blazor WASM) → dotnet run local (debug, hot-reload)

    Al terminar:
      - API:  http://localhost:5031 (Swagger: /swagger)
      - Web:  http://localhost:59793
      - BD:   localhost:1433 (OrkinCotizador)

.USAGE
    .\dev-start.ps1          # levanta todo
    .\dev-start.ps1 -SoloWeb # solo la web (si ya tienes docker compose corriendo)

#>

param(
    [switch]$SoloWeb
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Write-Host '=== ORKIN Cotizador - dev-start ===' -ForegroundColor Green

if (-not $SoloWeb) {
    # 1. Levantar SQL Server + API en Docker
    Write-Host 'Levantando SQL Server + API en Docker...' -ForegroundColor Cyan
    & docker compose -f (Join-Path $repoRoot 'docker-compose.yml') up -d --build

    if ($LASTEXITCODE -ne 0) {
        throw 'docker compose fallo. Revise el output arriba.'
    }

    # 2. Esperar a que la API responda
    Write-Host 'Esperando a la API...' -ForegroundColor Cyan
    $maxIntentos = 30
    $intentos = 0
    $apiLista = $false

    while ($intentos -lt $maxIntentos -and -not $apiLista) {
        $intentos++
        try {
            $response = Invoke-RestMethod -Uri 'http://localhost:5031/health' -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
            if ($response.datos.estado -eq 'VIVO') {
                $apiLista = $true
                Write-Host "API lista (intento $intentos)." -ForegroundColor Green
            }
        }
        catch {
            Write-Host "  esperando... ($intentos/$maxIntentos)" -ForegroundColor DarkGray
            Start-Sleep -Seconds 2
        }
    }

    if (-not $apiLista) {
        throw 'La API no respondio a tiempo. Revise: docker compose logs api'
    }
}

# 3. Arrancar la Web localmente (debug)
Write-Host 'Iniciando Web (http://localhost:59793)...' -ForegroundColor Cyan

$webProject = Join-Path $repoRoot 'Orkin.Cotizador.UI\Orkin.Cotizador.UI.Web\Orkin.Cotizador.UI.Web.csproj'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:59793'

& dotnet run --project $webProject

Write-Host ''
Write-Host 'Ambiente de desarrollo listo:' -ForegroundColor Green
Write-Host '  API:     http://localhost:5031/swagger' -ForegroundColor White
Write-Host '  Web:     http://localhost:59793/login' -ForegroundColor White
Write-Host '  Admin:   admin@orkin.local / OrkinDev_2026' -ForegroundColor White
Write-Host ''
Write-Host 'Para detener:' -ForegroundColor Yellow
Write-Host '  Ctrl+C detiene la web' -ForegroundColor Yellow
Write-Host '  docker compose down detiene SQL Server y la API' -ForegroundColor Yellow
