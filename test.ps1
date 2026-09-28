#Requires -Version 7.0
<#
.SYNOPSIS
    Ejecuta la suite de tests del proyecto.

.DESCRIPTION
    xunit.v3 con Microsoft Testing Platform no se ejecuta con `dotnet test` en el SDK 10.0.401
    porque ese comando todavía usa el runner de VSTest. Este script corre el proyecto de tests
    directamente con `dotnet run`, que sí usa el runner de MTP.
#>

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Write-Host 'Ejecutando tests...' -ForegroundColor Cyan
& dotnet run --project (Join-Path $repoRoot 'Orkin.Cotizador.Tests\Orkin.Cotizador.Tests.csproj') -c Release

if ($LASTEXITCODE -ne 0) {
    throw "La suite de tests fallo con codigo $LASTEXITCODE."
}

Write-Host 'Tests correctos.' -ForegroundColor Green
