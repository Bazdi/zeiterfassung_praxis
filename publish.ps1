# Single-EXE Build fuer Zeiterfassung Arztpraxis.
# Ausgabe: publish\Zeiterfassung.Web.exe (eigenstaendig, ~80 MB).
# Auf dem Zielrechner muss .NET NICHT installiert sein.
#
# Aufruf:
#   .\publish.ps1            # x64 (Standard)
#   .\publish.ps1 -Rid arm64 # ARM64

param(
    [ValidateSet('win-x64','win-arm64','win-x86')]
    [string]$Rid = 'win-x64',
    [string]$Out = 'publish'
)

$ErrorActionPreference = 'Stop'
$proj = 'src\Zeiterfassung.Web\Zeiterfassung.Web.csproj'

Write-Host ""
Write-Host " ───────────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "  Zeiterfassung Arztpraxis  ·  Publish $Rid"
Write-Host " ───────────────────────────────────────────────────────────" -ForegroundColor DarkGray

if (Test-Path $Out) {
    Write-Host "  alten Output entfernen ..."
    Remove-Item -Recurse -Force $Out
}

dotnet publish $proj `
    -c Release `
    -r $Rid `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $Out `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "  Fehler beim Publish." -ForegroundColor Red
    exit 1
}

$exe = Join-Path $Out 'Zeiterfassung.Web.exe'
$size = if (Test-Path $exe) { '{0:N1} MB' -f ((Get-Item $exe).Length / 1MB) } else { '?' }

Write-Host ""
Write-Host " ───────────────────────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host "  Fertig.   Single-EXE: $exe   ($size)" -ForegroundColor Green
Write-Host ""
Write-Host "  Starten:    .\$exe"
Write-Host "  URL:        http://localhost:5000  (oeffnet sich automatisch)"
Write-Host ""
Write-Host "  Tipp: Den kompletten Ordner '$Out\' auf den Praxis-PC kopieren."
Write-Host "  Eine SQLite-Datei 'zeiterfassung.db' wird beim ersten Start"
Write-Host "  neben der .exe angelegt."
Write-Host " ───────────────────────────────────────────────────────────" -ForegroundColor DarkGray
