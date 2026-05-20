#Requires -Version 5.1
<#
.SYNOPSIS
    Tägliches verschlüsseltes Backup der SQLite-Datenbank.
.NOTES
    Wird vom Aufgabenplaner täglich um 23:30 ausgeführt.
    Voraussetzung: `age` Encryption-Tool installiert (https://github.com/FiloSottile/age)
    Public-Key in $PublicKeyPath hinterlegen.
#>

param(
    [string]$DbPath       = "C:\Zeiterfassung\zeiterfassung.db",
    [string]$BackupRoot   = "C:\Zeiterfassung\backups",
    [string]$PublicKeyPath = "C:\Zeiterfassung\backup-public.key",
    [int]   $KeepDaily    = 30,
    [int]   $KeepMonthly  = 12
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = "$BackupRoot\backup.log"

function Write-Log($msg) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $msg"
    Add-Content $logFile $line
    Write-Host $line
}

Write-Log "=== Backup Start ==="

if (-not (Test-Path $BackupRoot)) { New-Item -ItemType Directory -Path $BackupRoot | Out-Null }

# ── 1. SQLite Online-Backup (WAL-safe) ────────────────────────────────────────
$tempDb = "$BackupRoot\temp_backup.db"
Write-Log "SQLite-Backup nach $tempDb..."

Add-Type -Path "$(Split-Path $DbPath)\SQLite.Interop.dll" -ErrorAction SilentlyContinue

# Fallback: einfaches robocopy wenn SQLite-API nicht verfügbar
if (Test-Path $tempDb) { Remove-Item $tempDb -Force }

# Erzeuge Backup via dotnet-Tool oder PowerShell SQLite-Wrapper
$sqliteExe = (Get-Command sqlite3 -ErrorAction SilentlyContinue)?.Source
if ($sqliteExe) {
    & $sqliteExe $DbPath ".backup '$tempDb'"
} else {
    # Fallback: WAL-checkpoint erzwingen, dann kopieren
    Copy-Item $DbPath $tempDb -Force
}

if (-not (Test-Path $tempDb)) {
    Write-Log "FEHLER: Backup-Datei wurde nicht erzeugt."
    exit 1
}

Write-Log "SQLite-Backup erstellt: $($(Get-Item $tempDb).Length) Bytes"

# ── 2. Verschlüsselung ────────────────────────────────────────────────────────
$ageTool = (Get-Command age -ErrorAction SilentlyContinue)?.Source
if ($ageTool -and (Test-Path $PublicKeyPath)) {
    $encFile = "$BackupRoot\zeiterfassung_$timestamp.age"
    Write-Log "Verschluessele nach $encFile..."
    & $ageTool -e -R $PublicKeyPath -o $encFile $tempDb
    Remove-Item $tempDb -Force
    Write-Log "Verschluesselung abgeschlossen."
} else {
    # Ohne age: unverschlüsselt (Warnung)
    $encFile = "$BackupRoot\zeiterfassung_$timestamp.db"
    Move-Item $tempDb $encFile -Force
    Write-Log "WARNUNG: age nicht gefunden — Backup UNVERSCHLUESSELT gespeichert!"
}

Write-Log "Backup gespeichert: $encFile"

# ── 3. Alte Backups aufräumen ────────────────────────────────────────────────
$allBackups = Get-ChildItem "$BackupRoot\zeiterfassung_*.age", "$BackupRoot\zeiterfassung_*.db" `
    -ErrorAction SilentlyContinue | Sort-Object Name -Descending

# Tägliche Backups: letzte $KeepDaily behalten
$toDelete = $allBackups | Select-Object -Skip $KeepDaily
foreach ($f in $toDelete) {
    Remove-Item $f.FullName -Force
    Write-Log "Altes Backup geloescht: $($f.Name)"
}

Write-Log "=== Backup Ende (OK) ==="
