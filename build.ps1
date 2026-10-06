<#
.SYNOPSIS
    Baut Workspace Manager und erzeugt den MSI-Installer.

.DESCRIPTION
    1. Veröffentlicht die App eigenständig (self-contained, win-x64) nach artifacts\publish
    2. Führt den eingebauten Selbsttest aus
    3. Erzeugt artifacts\WorkspaceManager-<Version>-x64.msi mit WiX

    Voraussetzungen: .NET 10 SDK und das WiX-Tool (dotnet tool install --global wix).

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Version 1.1.0
    .\build.ps1 -SkipTests
    .\build.ps1 -Quick        # App + Selbsttest + Addon bauen, ohne Addon-Tests und ohne MSIs (zum Weiterentwickeln)
    .\build.ps1 -AddonOnly    # nur Addon bauen und testen, in den neuesten Build kopieren
#>
param(
    [string]$Version = '1.0.0',
    [switch]$SkipTests,
    [switch]$Quick,
    [switch]$AddonOnly
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$msi = Join-Path $PSScriptRoot "artifacts\WorkspaceManager-$Version-x64.msi"

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Check($what) { if ($LASTEXITCODE -ne 0) { throw "$what ist fehlgeschlagen (Exit-Code $LASTEXITCODE)." } }

if ($AddonOnly) {
    $latest = Get-ChildItem 'artifacts' -Directory -Filter 'publish-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    if (-not $latest) { throw 'Es gibt noch keinen Build. Bitte zuerst .\build.ps1 -Quick ausführen.' }
    $publish = $latest.FullName
}
else {
    Step 'Alte Build-Ausgabe aufräumen'
    # Every build publishes into a fresh folder, so a still running copy of an older build cannot block it.
    # Folders that are still in use are skipped.
    if (Test-Path 'artifacts') {
        Get-ChildItem 'artifacts' -Force | Where-Object { $_.Name -ne 'LicenseGenerator' } |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    }
    $publish = Join-Path $PSScriptRoot ('artifacts\publish-' + (Get-Date -Format 'yyyyMMddHHmmss'))
    New-Item -ItemType Directory -Force $publish | Out-Null

    Step 'App veröffentlichen'
    dotnet publish src\WorkspaceManager -c Release -r win-x64 --self-contained true `
        -p:Version=$Version -p:DebugType=none -p:DebugSymbols=false -o $publish
    Check 'dotnet publish'
}

if (-not $SkipTests -and -not $AddonOnly) {
    Step 'Selbsttest'
    $report = Join-Path $PSScriptRoot 'artifacts\self-test.txt'
    $test = Start-Process (Join-Path $publish 'WorkspaceManager.exe') -ArgumentList '--self-test', '--no-input-test', '--report', $report -PassThru -Wait
    if ($test.ExitCode -ne 0) { throw "Selbsttest fehlgeschlagen (Exit-Code $($test.ExitCode)). Details: $report" }
    Remove-Item (Join-Path $publish 'test-results.txt') -ErrorAction SilentlyContinue
}

# Every addon: folder name, display name (for the installer file), test project.
$addons = @(
    @{ Folder = 'Remote'; Name = 'Fernwartung'; Tests = 'tests\Remote.Tests' },
    @{ Folder = 'Vault'; Name = 'Passwort-Tresor'; Tests = 'tests\Vault.Tests' }
)
foreach ($addon in $addons) {
    Step "Addon $($addon.Name) bauen"
    dotnet build "addons\$($addon.Folder)" -c Release -p:Version=$Version
    Check "dotnet build (Addon $($addon.Name))"
    $addon.Dir = Join-Path $PSScriptRoot "artifacts\addons\$($addon.Folder)"
    New-Item -ItemType Directory -Force $addon.Dir | Out-Null
    $dll = "WorkspaceManager.Addon.$($addon.Folder).dll"
    Copy-Item "addons\$($addon.Folder)\bin\Release\net10.0-windows\$dll" $addon.Dir -Force
    if (-not $SkipTests -and -not $Quick) {
        Step "Addon-Tests $($addon.Name)"
        dotnet run --project $addon.Tests -c Release
        Check "Addon-Tests $($addon.Name)"
    }

    # Development convenience: the unpacked build gets the addons too, so it can be started directly.
    $devAddons = Join-Path $publish "Addons\$($addon.Folder)"
    New-Item -ItemType Directory -Force $devAddons | Out-Null
    try { Copy-Item (Join-Path $addon.Dir $dll) $devAddons -Force -ErrorAction Stop }
    catch { Write-Warning 'Ein Addon im laufenden Build ist gesperrt. Bitte Workspace Manager beenden und erneut ausführen.'; if ($AddonOnly) { throw } }
}

if ($Quick -or $AddonOnly) {
    Write-Host "`nFertig (ohne MSIs): $publish" -ForegroundColor Green
    return
}

Step 'WiX-Erweiterungen bereitstellen'
$wixVersion = (wix --version).Split('+')[0]
foreach ($extension in 'WixToolset.UI.wixext', 'WixToolset.Util.wixext') {
    wix extension add -g "$extension/$wixVersion" 2>&1 | Out-Null
}

Step 'Lizenztext für den Installer erzeugen'
function ConvertTo-Rtf([string]$text) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $text.ToCharArray()) {
        $code = [int]$ch
        if ($ch -eq '\') { [void]$sb.Append('\\') }
        elseif ($ch -eq '{') { [void]$sb.Append('\{') }
        elseif ($ch -eq '}') { [void]$sb.Append('\}') }
        elseif ($ch -eq "`n") { [void]$sb.Append("\par`n") }
        elseif ($ch -eq "`r") { }
        elseif ($code -gt 127) { if ($code -gt 32767) { $code -= 65536 }; [void]$sb.Append("\u$code?") }
        else { [void]$sb.Append($ch) }
    }
    $sb.ToString()
}
$licenseDir = Join-Path $PSScriptRoot 'artifacts\installer'
New-Item -ItemType Directory -Force $licenseDir | Out-Null
$summary = Get-Content 'installer\LicenseSummary.de.txt' -Raw -Encoding UTF8
$license = Get-Content 'LICENSE' -Raw -Encoding UTF8
$rtf = '{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs18 ' + (ConvertTo-Rtf ($summary + "`n`n----------------------------------------`n`n" + $license)) + '}'
[System.IO.File]::WriteAllText((Join-Path $licenseDir 'License.rtf'), $rtf, [System.Text.Encoding]::ASCII)

Step 'MSI erzeugen'
wix build installer\Package.wxs -arch x64 -culture de-DE `
    -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext `
    -bindpath installer -bindpath $licenseDir `
    -d Version=$Version -d PublishDir=$publish `
    -d RemoteDir=$($addons[0].Dir) -d VaultDir=$($addons[1].Dir) `
    -o $msi
Check 'wix build'

$addonMsis = @()
foreach ($addon in $addons) {
    Step "Addon-MSI erzeugen ($($addon.Name))"
    $addonMsi = Join-Path $PSScriptRoot "artifacts\WorkspaceManager-Addon-$($addon.Name)-$Version-x64.msi"
    wix build "installer\Addon-$($addon.Folder).wxs" -arch x64 -culture de-DE `
        -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext `
        -bindpath installer -bindpath $licenseDir `
        -d Version=$Version -d "$($addon.Folder)Dir=$($addon.Dir)" `
        -o $addonMsi
    Check "wix build (Addon $($addon.Name))"
    $addonMsis += $addonMsi
}

Write-Host "`nFertig:" -ForegroundColor Green
Get-Item (@($msi) + $addonMsis) | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 2) } } | Format-Table -AutoSize
