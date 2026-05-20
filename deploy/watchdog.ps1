#Requires -Version 5.1
<#
.SYNOPSIS
    Watchdog: Prüft alle 60s ob Service und Kiosk-Browser laufen.
    Wird vom Aufgabenplaner jede Minute ausgeführt.
#>

param(
    [string]$ServiceName = "ZeiterfassungService",
    [string]$Port        = "5000",
    [string]$KioskUser   = "praxiskiosk"
)

function Write-Log($msg) {
    $logDir = "C:\Zeiterfassung\logs"
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory $logDir | Out-Null }
    Add-Content "$logDir\watchdog.log" "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $msg"
}

# ── Service prüfen ────────────────────────────────────────────────────────────
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -ne 'Running') {
    Write-Log "Service nicht aktiv — starte neu..."
    Start-Service -Name $ServiceName -ErrorAction SilentlyContinue
    Write-Log "Service neugestartet."
}

# ── HTTP-Erreichbarkeit prüfen ────────────────────────────────────────────────
try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/" -UseBasicParsing -TimeoutSec 5
    if ($response.StatusCode -ne 200) {
        Write-Log "HTTP-Check fehlgeschlagen (Status $($response.StatusCode))."
    }
} catch {
    Write-Log "HTTP-Check Exception: $_"
}

# ── Kiosk-Browser prüfen ─────────────────────────────────────────────────────
$kioskProc = Get-Process -Name "msedge" -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -like "*Zeiterfassung*" -or $_.SessionId -gt 0 }

if (-not $kioskProc) {
    # Versuche Browser für praxiskiosk-Session neu zu starten
    $shortcut = "C:\Users\$KioskUser\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Zeiterfassung Terminal.lnk"
    if (Test-Path $shortcut) {
        try {
            $session = (query session $KioskUser 2>$null | Select-String "\d+").Matches[0]?.Value
            if ($session) {
                Write-Log "Kiosk-Browser nicht aktiv — versuche Neustart..."
                # Hinweis: psexec oder runas für andere Session nötig
            }
        } catch {}
    }
}
