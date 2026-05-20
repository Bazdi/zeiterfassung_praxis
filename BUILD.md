# Starten & Verteilen — Zeiterfassung Arztpraxis

Zwei Wege: aus dem Quellcode (Entwicklung) oder als eigenstaendige `.exe`
(Auslieferung an die Praxis).

---

## 1) Entwicklung — direkt aus dem Quellcode starten

Voraussetzung: **.NET 8 SDK** ([Download](https://dotnet.microsoft.com/download/dotnet/8.0)).

```bat
dotnet run --project src\Zeiterfassung.Web\Zeiterfassung.Web.csproj
```

Der Browser oeffnet automatisch unter **http://localhost:5033** (siehe
`launchSettings.json`). Beim ersten Start: `/setup` → Admin-Konto anlegen,
dann `/login`. Mitarbeiter stempeln am **`/terminal`** Kiosk.

Stoppen: `STRG+C` im Terminal.

Die SQLite-Datei `zeiterfassung.db` wird automatisch im Arbeitsverzeichnis
angelegt.

---

## 2) Single-EXE bauen (Auslieferung)

Voraussetzung: .NET 8 SDK auf **deinem** Rechner. Der Praxis-PC braucht
**nichts** — die Runtime wird mitgebundelt.

```bat
publish.cmd
```

Alternativ PowerShell:

```powershell
.\publish.ps1
```

Standard ist **win-x64**. Fuer ARM64-Windows: `publish.cmd arm64`.

### Ergebnis

Im Ordner `publish\` liegt nach ca. 60–90 Sekunden:

```
publish\
├── Zeiterfassung.Web.exe   ← Doppelklick startet alles (~80 MB)
├── wwwroot\                ← Bootstrap, favicon, statische Dateien
├── appsettings.json
└── appsettings.Development.json
```

Den **kompletten Ordner** auf den Praxis-PC kopieren (USB-Stick, SMB, was
auch immer). Beim ersten Doppelklick auf die `.exe`:

1. Console-Fenster oeffnet sich (kann minimiert werden, nicht schliessen).
2. Standard-Browser oeffnet sich automatisch auf **http://localhost:5000**.
3. `/setup` fragt nach Admin-Daten.
4. Danach laeuft die App. `zeiterfassung.db` wird neben der `.exe` angelegt.

### Beenden

`STRG+C` im Console-Fenster oder Fenster schliessen.

### Autostart in der Praxis (optional)

Verknuepfung der `Zeiterfassung.Web.exe` in den Windows Autostart-Ordner
(`shell:startup`) legen — dann startet sie beim Hochfahren des PCs.

Fuer einen echten Service (laeuft ohne Login, ohne Konsole) gibt es zwei
Optionen:

- **NSSM** (klein, kostenlos): `nssm install Zeiterfassung "C:\Pfad\Zeiterfassung.Web.exe"`
- **Windows Service direkt im Code**: ASP.NET Core unterstuetzt `UseWindowsService()`.
  Bei Bedarf in `Program.cs` ergaenzen, dann mit `sc create` registrieren.

---

## Daten sichern

Die komplette Datenbank ist eine einzige Datei: `zeiterfassung.db` neben
der `.exe`. Backup = diese Datei kopieren (besser bei gestoppter App).

GoBD-relevante Hash-Chain ist Teil der Tabellen — beim Restore unbedingt
die `/admin/integritaet`-Seite anwerfen, um zu pruefen ob die Kette intakt
ist.

---

## Andere Plattformen

Linux/Mac: das gleiche Schema, andere Runtime-ID:

```bat
dotnet publish src\Zeiterfassung.Web\Zeiterfassung.Web.csproj ^
  -c Release -r linux-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

(Bisher nur Windows getestet.)
