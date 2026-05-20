@echo off
REM Startet die App im DEMO-MODUS (Mock-Mandant mit Testdaten).
REM Eigene SQLite-Datei (zeiterfassung-demo.db), Port 5001 -
REM laeuft parallel zur Produktiv-Instanz auf Port 5000.

setlocal

REM Falls die gepublishte .exe existiert, nimm die (kein dotnet SDK noetig).
if exist "publish\Zeiterfassung.Web.exe" (
    echo Starte gepublishte .exe im Demo-Modus ...
    publish\Zeiterfassung.Web.exe --demo
    goto :eof
)

REM Sonst: aus dem Quellcode (Entwicklung).
echo Starte aus dem Quellcode im Demo-Modus ...
dotnet run --project src\Zeiterfassung.Web\Zeiterfassung.Web.csproj -- --demo

endlocal
