@echo off
REM Single-EXE Build fuer Zeiterfassung Arztpraxis (Windows x64).
REM Ausgabe: publish\Zeiterfassung.Web.exe (eigenstaendig, ~80 MB)
REM Auf dem Zielrechner muss .NET NICHT installiert sein.

setlocal

set RID=win-x64
if /I "%~1"=="arm64" set RID=win-arm64
if /I "%~1"=="x86"   set RID=win-x86

set PROJ=src\Zeiterfassung.Web\Zeiterfassung.Web.csproj
set OUT=publish

echo.
echo  Zeiterfassung Arztpraxis - Publish %RID%
echo.

if exist "%OUT%" (
    echo   alten Output entfernen ...
    rmdir /S /Q "%OUT%"
)

dotnet publish "%PROJ%" -c Release -r %RID% --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%OUT%" --nologo

if errorlevel 1 (
    echo.
    echo   Fehler beim Publish. Siehe Ausgabe oben.
    endlocal
    exit /b 1
)

echo.
echo   Fertig. Starten mit:
echo.
echo       %OUT%\Zeiterfassung.Web.exe
echo.
echo   Beim ersten Start oeffnet sich der Browser automatisch auf
echo   http://localhost:5000 - dann /setup zum Anlegen des Admins.
echo.
echo   Tipp: Den kompletten Ordner %OUT%\ auf den Praxis-PC kopieren.
echo   Eine SQLite-Datei zeiterfassung.db wird beim ersten Start neben
echo   der .exe angelegt.

endlocal
