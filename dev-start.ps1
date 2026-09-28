#Requires -Version 7.0
<#
.SYNOPSIS
    Levanta el entorno de desarrollo de ORKIN Cotizador.

.DESCRIPTION
    1. Arranca SQL Server con docker compose (si no está corriendo).
    2. Espera a que la base esté saludable.
    3. Aplica migraciones de EF Core.
    4. Lanza la API y la UI en ventanas separadas.

    La API escucha en http://localhost:5031 y la UI en http://localhost:59793.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$composeFile = Join-Path $repoRoot 'docker-compose.yml'

Write-Host '=== ORKIN Cotizador - dev-start ===' -ForegroundColor Green

# 1. SQL Server
Write-Host 'Levantando SQL Server...' -ForegroundColor Cyan
& docker compose -f $composeFile up -d

# 2. Esperar a que responda
$maxIntentos = 30
$intentos = 0
$saludable = $false

while ($intentos -lt $maxIntentos -and -not $saludable) {
    $intentos++
    try {
        $resultado = docker exec orkin-cotizador-db /opt/mssql-tools18/bin/sqlcmd `
            -S localhost -U sa -P 'OrkinDev_2026' -C -Q 'SELECT 1' 2>$null
        if ($resultado -match '1') {
            $saludable = $true
            Write-Host 'SQL Server listo.' -ForegroundColor Green
        }
    }
    catch {
        Write-Host "  esperando base de datos... ($intentos/$maxIntentos)" -ForegroundColor DarkGray
        Start-Sleep -Seconds 2
    }
}

if (-not $saludable) {
    throw 'SQL Server no respondio a tiempo. Revise docker logs orkin-cotizador-db.'
}

# 3. Aplicar migraciones
Write-Host 'Aplicando migraciones...' -ForegroundColor Cyan
& dotnet ef database update `
    --project (Join-Path $repoRoot 'Orkin.Cotizador.Core.Infrastructure\Orkin.Cotizador.Core.Infrastructure.csproj') `
    --startup-project (Join-Path $repoRoot 'Orkin.Cotizador.Api\Orkin.Cotizador.Api.csproj') `
    --no-build

# 4. Arrancar API y UI
Write-Host 'Iniciando API (http://localhost:5031)...' -ForegroundColor Cyan
$apiArgs = @(
    'run',
    '--project', (Join-Path $repoRoot 'Orkin.Cotizador.Api\Orkin.Cotizador.Api.csproj'),
    '--no-build',
    '--no-launch-profile'
)
Start-Process -FilePath 'dotnet' -ArgumentList $apiArgs -WorkingDirectory $repoRoot `
    -WindowStyle Normal -Environment @{ ASPNETCORE_ENVIRONMENT = 'Development'; ASPNETCORE_URLS = 'http://localhost:5031' }

Write-Host 'Iniciando UI (http://localhost:59793)...' -ForegroundColor Cyan
$uiArgs = @(
    'run',
    '--project', (Join-Path $repoRoot 'Orkin.Cotizador.UI\Orkin.Cotizador.UI.Web\Orkin.Cotizador.UI.Web.csproj'),
    '--no-build',
    '--no-launch-profile'
)
Start-Process -FilePath 'dotnet' -ArgumentList $uiArgs -WorkingDirectory $repoRoot `
    -WindowStyle Normal -Environment @{ ASPNETCORE_ENVIRONMENT = 'Development'; ASPNETCORE_URLS = 'http://localhost:59793' }

Write-Host ''
Write-Host 'Ambiente de desarrollo listo:' -ForegroundColor Green
Write-Host '  API: http://localhost:5031/swagger' -ForegroundColor White
Write-Host '  UI:  http://localhost:59793/login' -ForegroundColor White
