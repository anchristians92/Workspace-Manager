<#
.SYNOPSIS
    Baut den Lizenzgenerator (LizenzGenerator.exe) und legt ihn neben den privaten Schlüssel.

.DESCRIPTION
    Die EXE ist eine einzelne, kleine Datei (braucht die .NET-10-Desktop-Runtime). Sie gehört nur dir:
    nicht weitergeben und nicht ins Repository legen, sie arbeitet mit deinem privaten Schlüssel.

.EXAMPLE
    .\tools\Build-LicenseGenerator.ps1
    .\tools\Build-LicenseGenerator.ps1 -Destination D:\Lizenzen
#>
param(
    [string]$Destination = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'WorkspaceManager-Lizenzen')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts\LicenseGenerator'

dotnet publish (Join-Path $root 'tools\LicenseGenerator') -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ist fehlgeschlagen (Exit-Code $LASTEXITCODE)." }

New-Item -ItemType Directory -Force $Destination | Out-Null
Copy-Item (Join-Path $output 'LizenzGenerator.exe') $Destination -Force
Write-Host "`nFertig: $(Join-Path $Destination 'LizenzGenerator.exe')" -ForegroundColor Green
