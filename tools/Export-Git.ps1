<#
.SYNOPSIS
    Copies the committed project into a clean folder that can be uploaded (for example to GitHub).

.DESCRIPTION
    - Only files that are committed in this repository are copied. Build output, local logs and everything in .gitignore stay out.
    - The history is not copied. The new folder gets a fresh, empty Git repository (no commit), so no old author names,
      mail addresses or messages are carried along. Commit with your own identity.
    - The folder is checked for secrets: private keys, key files, license lists, user paths and company data.
      If something is found the script stops and says where.
    - Nobody gets your license key: the program only contains the PUBLIC key. Anyone who builds the program on their own
      creates their own key pair (docs\LICENSING.md, "keygen") and enters the public key in LicenseToken.PublicKey.

.EXAMPLE
    .\tools\Export-Git.ps1
    .\tools\Export-Git.ps1 -Target D:\Workspace-Manager-Git
#>
param(
    [string]$Target = 'C:\Workspace-Manager-Git'
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$marker = '.workspace-manager-export'

if (git status --porcelain) { throw 'Es gibt nicht committete Aenderungen. Bitte erst committen, es wird nur der committete Stand exportiert.' }

# An existing target is only emptied if it was made by this script.
if (Test-Path $Target) {
    $items = @(Get-ChildItem $Target -Force)
    if ($items.Count -gt 0 -and -not (Test-Path (Join-Path $Target $marker))) {
        throw "Der Ordner $Target ist nicht leer und stammt nicht von diesem Skript. Bitte einen leeren oder neuen Ordner angeben."
    }
    # An existing Git repository in the folder (with its history and the link to GitHub) is kept: only the files are replaced.
    $items | Where-Object { $_.Name -ne '.git' } | Remove-Item -Recurse -Force
}
else { New-Item -ItemType Directory -Force $Target | Out-Null }

$files = git ls-files
foreach ($file in $files) {
    $destination = Join-Path $Target $file
    New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
    Copy-Item $file $destination -Force
}
Set-Content (Join-Path $Target $marker) 'Created by tools\Export-Git.ps1. The script may empty this folder when it runs again.' -Encoding ASCII
Write-Host ("{0} Dateien kopiert nach {1}" -f @($files).Count, $Target) -ForegroundColor Cyan

# ---- Check for secrets ----
$problems = @()
$badNames = '*.key', '*.pem', '*.pfx', '*.p12', 'issued-licenses*', 'license-private*', '*.log', 'license.json'
foreach ($pattern in $badNames) {
    Get-ChildItem $Target -Recurse -Force -File -Filter $pattern | ForEach-Object { $problems += "Datei, die nicht hineingehoert: $($_.FullName.Substring($Target.Length + 1))" }
}
$patterns = [ordered]@{
    'privater Schluessel'    = 'BEGIN (EC |RSA |OPENSSH )?PRIVATE KEY'
    'Windows-Benutzerpfad'   = '(?i)[A-Z]:\\Users\\(?!Public|Default|%|\$|<|\w*Benutzer)[^\\\s"]+'
    'Firmen-Mailadresse'     = '(?i)[\w.-]+@[\w-]+\.local'
    'Firmen-Domain'          = '(?i)lk' + '-aur'
    'Geheimnis im Klartext'  = '(?i)(password|passwort|secret|token)\s*[:=]\s*"(?!geheim"|s3cret"|falsch"|")[^"\s]{6,}"'
}
$textFiles = Get-ChildItem $Target -Recurse -Force -File | Where-Object {
    $_.Extension -in '.cs', '.csproj', '.ps1', '.wxs', '.wxi', '.md', '.txt', '.slnx', '.json', '.xml', '.gitignore', '' -and
    $_.Name -ne 'Export-Git.ps1'
}
foreach ($file in $textFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($entry in $patterns.GetEnumerator()) {
        if ($text -match $entry.Value) { $problems += "$($entry.Key) in $($file.FullName.Substring($Target.Length + 1)): $($Matches[0])" }
    }
}
if ($problems.Count -gt 0) {
    Write-Host "`nDie Pruefung hat etwas gefunden. Der Ordner ist NICHT zum Hochladen gedacht:" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    throw 'Pruefung fehlgeschlagen.'
}
Write-Host 'Pruefung bestanden: keine Schluessel, Benutzerpfade, Firmen-Adressen oder Klartext-Passwoerter gefunden.' -ForegroundColor Green

# ---- Fresh repository without history ----
Push-Location $Target
try {
    if (Test-Path (Join-Path $Target '.git')) {
        # Git writes line-ending warnings to stderr; they are not errors.
        $before = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        git add -A 2>&1 | Out-Null
        $ErrorActionPreference = $before
        Write-Host 'Vorhandenes Git-Repository beibehalten. Geaenderte Dateien:' -ForegroundColor Green
        git status --short
        Write-Host 'Jetzt mit git commit und git push hochladen.' -ForegroundColor Cyan
        return
    }
    git init -b main 2>&1 | Out-Null
    # The marker file is only for this script and must not be uploaded.
    Add-Content (Join-Path $Target '.git\info\exclude') $marker -Encoding ASCII
    Write-Host 'Leeres Git-Repository angelegt (ohne Commit, ohne Historie).' -ForegroundColor Green
}
finally { Pop-Location }

Write-Host "`nFertig: $Target" -ForegroundColor Green
