#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installiert Zeiterfassung als Windows-Service und richtet den Kiosk-Browser ein.
.DESCRIPTION
    Führt folgende Schritte aus:
    1. Publiziert die Anwendung nach $InstallPath
    2. Installiert als Windows-Service (startet automatisch)
    3. Erstellt Kiosk-Browser-Shortcut für praxiskiosk-Benutzer
    4. Richtet Aufgabenplaner-Tasks ein (Backup + Watchdog)
.NOTES
    Vor dem ersten Ausführen: Variablen unten anpassen.
#>

param(
    [string]$InstallPath   = "C:\Zeiterfassung",
    [string]$Port          = "5000",
    [string]$ServiceName   = "ZeiterfassungService",
    [string]$DisplayName   = "Zeiterfassung Arztpraxis",
    [string]$KioskUser     = "praxiskiosk",
    [string]$ProjectPath   = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = "Stop"

Write-Host "=== Zeiterfassung Installation ===" -ForegroundColor Cyan

# ── 1. Publish ────────────────────────────────────────────────────────────────
Write-Host "`n[1/5] Anwendung wird publiziert nach $InstallPath..."
if (Test-Path $InstallPath) {
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    Start-Sleep 2
}

dotnet publish "$ProjectPath\src\Zeiterfassung.Web\Zeiterfassung.Web.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o $InstallPath `
    -p:ApplicationUrl="http://127.0.0.1:$Port"

# appsettings für Produktion
$connStr = "Data Source=$InstallPath\zeiterfassung.db"
$appsettings = @{
    ConnectionStrings = @{ DefaultConnection = $connStr }
    Logging = @{ LogLevel = @{ Default = "Warning"; "Microsoft.AspNetCore" = "Warning" } }
    AllowedHosts = "*"
} | ConvertTo-Json -Depth 5
Set-Content "$InstallPath\appsettings.Production.json" $appsettings

# ── 2. Windows-Service ────────────────────────────────────────────────────────
Write-Host "`n[2/5] Windows-Service wird eingerichtet..."
$exePath = "$InstallPath\Zeiterfassung.Web.exe"

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep 2
}

New-Service -Name $ServiceName `
    -DisplayName $DisplayName `
    -BinaryPathName "`"$exePath`" --contentRoot `"$InstallPath`"" `
    -StartupType Automatic `
    -Description "GoBD-konforme Zeiterfassung fuer Arztpraxis"

Start-Service -Name $ServiceName
Write-Host "Service gestartet. URL: http://127.0.0.1:$Port" -ForegroundColor Green

# ── 3. Kiosk-Browser ─────────────────────────────────────────────────────────
Write-Host "`n[3/5] Kiosk-Browser-Shortcut wird erstellt..."
$kioskStartup = "C:\Users\$KioskUser\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup"

if (Test-Path "C:\Users\$KioskUser") {
    $edgePath = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
    if (-not (Test-Path $edgePath)) {
        $edgePath = "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
    }

    $wsh = New-Object -ComObject WScript.Shell
    $shortcut = $wsh.CreateShortcut("$kioskStartup\Zeiterfassung Terminal.lnk")
    $shortcut.TargetPath = $edgePath
    $shortcut.Arguments = "--kiosk http://127.0.0.1:$Port/terminal --edge-kiosk-type=fullscreen --no-first-run"
    $shortcut.Description = "Zeiterfassung Stempel-Terminal"
    $shortcut.Save()
    Write-Host "Kiosk-Shortcut erstellt fuer Benutzer $KioskUser" -ForegroundColor Green
} else {
    Write-Host "Benutzer $KioskUser nicht gefunden - Shortcut manuell anlegen." -ForegroundColor Yellow
}

# ── 4. Backup-Task ────────────────────────────────────────────────────────────
Write-Host "`n[4/5] Backup-Aufgabe wird eingerichtet..."
$backupScript = "$PSScriptRoot\backup.ps1"
$action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NonInteractive -File `"$backupScript`" -DbPath `"$InstallPath\zeiterfassung.db`""
$trigger = New-ScheduledTaskTrigger -Daily -At "23:30"
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable
Register-ScheduledTask -TaskName "Zeiterfassung Backup" `
    -Action $action -Trigger $trigger -Settings $settings `
    -RunLevel Highest -Force | Out-Null
Write-Host "Backup-Task registriert (taeglich 23:30)" -ForegroundColor Green

# ── 5. Watchdog-Task ─────────────────────────────────────────────────────────
Write-Host "`n[5/5] Watchdog-Aufgabe wird eingerichtet..."
$watchdogScript = "$PSScriptRoot\watchdog.ps1"
$wdAction = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NonInteractive -File `"$watchdogScript`" -ServiceName `"$ServiceName`" -Port `"$Port`""
$wdTrigger = New-ScheduledTaskTrigger -RepetitionInterval (New-TimeSpan -Minutes 1) `
    -Once -At (Get-Date)
Register-ScheduledTask -TaskName "Zeiterfassung Watchdog" `
    -Action $wdAction -Trigger $wdTrigger -Settings $settings `
    -RunLevel Highest -Force | Out-Null
Write-Host "Watchdog-Task registriert (jede Minute)" -ForegroundColor Green

Write-Host "`n=== Installation abgeschlossen ===" -ForegroundColor Cyan
Write-Host "App: http://127.0.0.1:$Port" -ForegroundColor White
Write-Host "Erster Start: /setup aufrufen um Admin-Konto anzulegen" -ForegroundColor White
