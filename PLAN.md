# Plan: Zeiterfassung für Arztpraxis (GoBD-konform)

## Context

Eine Arztpraxis mit ~6 Mitarbeitern führt aktuell die Zeiterfassung in einer Excel-Datei — diese ist weder revisionssicher noch manipulationssicher und erfüllt damit nicht die Anforderungen aus §16 ArbZG (Aufzeichnungspflicht) und den GoBD (Grundsätze ordnungsgemäßer Buchführung).

Ziel: Eine eigenständige Anwendung, die das Excel ablöst, GoBD-konform ist (lückenloses Audit-Log, unveränderliche Stempelungen, Manipulationsnachweis), für 6 Mitarbeiter angemessen dimensioniert ist und sowohl am Empfang (Stempelterminal) als auch im Admin-Bereich (Praxismanagement) bedienbar ist. Lohnbuchhaltung/Steuerberater erhält monatliche Exporte.

## Empfohlene Architektur

**Lokale Anwendung auf einem Standrechner (Praxis-PC) — keine zusätzliche Hardware.**

Setup: Die Anwendung läuft auf einem vorhandenen Praxisrechner. Stempeln erfolgt direkt an diesem Rechner via persönlicher PIN je Mitarbeiter — keine RFID-Chips, kein Kartenleser, kein zusätzliches Tablet nötig.

Technisch umgesetzt als ASP.NET Core Webapp, die als **Windows-Service** im Hintergrund läuft. Bedienung im Browser unter `http://localhost:PORT`:
- **Stempel-Bildschirm** (Vollbild im Browser, Auto-Start beim Windows-Login): Liste aller MA → Klick auf eigenen Namen → PIN eingeben → Aktion (Kommen / Pause / Gehen) bestätigen
- **Self-Service**: Nach PIN-Login eigene Zeiten/Saldo/Urlaubsantrag einsehen
- **Admin-Bereich**: Praxismanagement (Stammdaten, Korrektur-Freigaben, Exporte, Integritätsprüfung)

Falls weitere Praxisrechner Zugriff brauchen sollen, ist das über LAN möglich (`http://praxis-pc:PORT`) — aber Default-Setup ist Single-Machine.

Keine Cloud — alle Daten bleiben in der Praxis (DSGVO/Patientenkontext einfacher).

### Tech-Stack

| Komponente | Wahl | Begründung |
|---|---|---|
| Backend | **.NET 8 / ASP.NET Core (C#)** | Stabil unter Windows, einfach als Windows-Service deploybar, ausgereifte GoBD-/Reporting-Bibliotheken |
| Frontend | **Blazor Server** | Keine separate JS-Build-Pipeline, Touch-tauglich, MA-Anzahl gering → Server-Render unkritisch |
| Datenbank | **SQLite** (mit WAL-Mode) | Single-File, einfaches Backup, ausreichend für 6 MA. Migration zu Postgres möglich falls nötig |
| Auth | Individuelle PIN je MA (Argon2id-Hash) + Passwort+2FA für Admin | Keine zusätzliche Hardware nötig |
| PDF-Export | **QuestPDF** | Open Source, moderne API, GoBD-taugliche Stundenzettel |
| Excel-Export | **ClosedXML** | DATEV-/Steuerberater-tauglich |
| Deployment | Windows Service via `sc.exe` oder NSSM, Auto-Start | Läuft headless im Hintergrund |
| Backup | Tägliches verschlüsseltes SQLite-Backup auf NAS/USB | Tooling: einfaches PowerShell-Script + Aufgabenplaner |

### GoBD-/Revisionssicherheit (Kernanforderung)

1. **Append-only Stempelungen**: `TimeEntry`-Tabelle ist write-once. Keine UPDATEs/DELETEs.
2. **Korrekturen als neue Einträge**: Vergessenes Stempeln → MA stellt Antrag → Admin gibt frei → es entsteht ein neuer `Correction`-Eintrag mit Referenz auf Original (falls vorhanden), Begründung, Antragsteller, Freigeber, Zeitstempel. Original bleibt unangetastet.
3. **Audit-Log-Tabelle** (`AuditLog`): jede Mutation an irgendeiner Tabelle wird mit User, UTC-Timestamp, Action, Old-JSON, New-JSON protokolliert (über EF Core `SaveChangesInterceptor`).
4. **Hash-Chain**: Jeder `TimeEntry` und `AuditLog`-Eintrag enthält `PrevHash` + `Hash = SHA256(PrevHash || Payload)`. Bei nachträglicher Manipulation an einer Zeile bricht die Kette → maschinell prüfbar via Admin-Funktion „Integrität prüfen".
5. **UTC-Zeitstempel** in DB, Anzeige in lokaler Zeit (Europe/Berlin).
6. **Export-Signatur**: Monats-Exporte enthalten Hash der enthaltenen Datensätze + Kettenkopf-Hash → reproduzierbar, fälschungssicher.
7. **Datenaufbewahrung**: 10 Jahre per Default (GoBD), Löschung nur via Admin-Funktion mit Bestätigung und Audit-Eintrag.

### Authentifizierungs-Strategie

| Kontext | Methode |
|---|---|
| Stempeln am Praxisrechner | **Individuelle PIN pro MA** (mind. 4-stellig, von Admin gesetzt oder von MA bei erstem Login geändert) |
| Self-Service (eigene Zeiten/Anträge) | Gleiche PIN — nach Stempel-Login direkt zugänglich |
| Admin (Praxismanagement) | Eigenes Admin-Konto mit Benutzername + Passwort (≥10 Zeichen) + optional TOTP-2FA |

PINs werden gehashed gespeichert (**Argon2id**, eindeutiger Salt pro MA). Brute-Force-Schutz: 5 Fehlversuche je MA → 60 s Sperre, danach 5 Min, danach Admin-Reset erforderlich. PIN-Eingabe-Feld ist `type=password` (verdeckt), Tastatur-Layout-Unabhängig (nur Ziffern).

### Funktionsumfang

**MVP (Phase 1)**
- Kommen / Gehen / Pause-Start / Pause-Ende (am Terminal, ein-Klick je Aktion)
- Aktueller Status pro MA sichtbar (wer ist da, wer in Pause)
- Tages-/Wochen-/Monatsansicht pro MA
- Sollstunden (pro MA konfigurierbar, ggf. unterschiedlich je Wochentag — Teilzeit!)
- Überstunden-Saldo (Ist − Soll, fortlaufend)
- Manuelle Nachträge mit Begründung → Admin-Freigabe-Workflow
- Monats-Export: PDF-Stundenzettel pro MA + Excel-Sammeldatei für Steuerberater
- GoBD-Integritätsprüfung (Admin-Knopf)

**Phase 2**
- Urlaubsantrag (MA stellt, Admin genehmigt) + Urlaubskonto
- Krankmeldungs-Erfassung (Admin trägt ein)
- Feiertage automatisch (NodaTime + Bundesland-Konfiguration)
- ArbZG-Warnungen: Live-Hinweis bei > 10 h/Tag, fehlende Pause ab 6 h, < 11 h Ruhezeit
- DATEV-LODAS-Export (CSV-Format)

**Phase 3 (optional)**
- RFID-Reader-Integration (falls Hardware angeschafft)
- Backup-Automation mit Verschlüsselung + Mail-Bericht
- E-Mail-Benachrichtigung an Admin bei Korrekturanträgen

## Datenmodell (Kern)

```
Employee(Id, Vorname, Nachname, Email?, PinHash, PinSalt, PinChangedAt,
         WochenSollStunden, IsActive, IsAdmin, UrlaubsanspruchTage,
         FailedPinAttempts, LockedUntil?)

TimeEntry(Id, EmployeeId, Type[KOMMEN|GEHEN|PAUSE_START|PAUSE_ENDE],
          TimestampUtc, Source[TERMINAL|SELFSERVICE|KORREKTUR],
          CorrectionOfId?, CreatedByUserId, PrevHash, Hash)
  -- append-only, nie UPDATE/DELETE

CorrectionRequest(Id, EmployeeId, RequestedTimestamp, Type, Begründung,
                  Status[OFFEN|GENEHMIGT|ABGELEHNT], ApprovedByUserId?,
                  ApprovedAt?, ResultingEntryId?)

LeaveRequest(Id, EmployeeId, Von, Bis, Typ[URLAUB|KRANK|SONSTIGES],
             Status, ApprovedByUserId?, Notiz)

Holiday(Datum, Name, Bundesland)

AuditLog(Id, UserId, EntityName, EntityId, Action, OldJson, NewJson,
         TimestampUtc, PrevHash, Hash)

User(Id, EmployeeId?, Username, PasswordHash, Roles, TotpSecret?)
```

## Projekt-Struktur (geplant)

```
Zeiterfassung/
  Zeiterfassung.sln
  src/
    Zeiterfassung.Core/          # Domain-Modelle, GoBD-Logik (Hash-Chain), Services
    Zeiterfassung.Data/          # EF Core DbContext, Migrations, AuditInterceptor
    Zeiterfassung.Web/           # Blazor Server App
      Pages/
        Terminal/                # Kiosk-UI (groß, touch-tauglich)
        SelfService/             # MA-Eigenansicht
        Admin/                   # Stammdaten, Freigaben, Exporte, Integritätsprüfung
      Components/
    Zeiterfassung.Export/        # QuestPDF + ClosedXML Reports
    Zeiterfassung.Service/       # Windows-Service-Host (oder integriert in Web)
  tests/
    Zeiterfassung.Core.Tests/    # xUnit, Fokus: Hash-Chain, ArbZG-Regeln, Saldo-Berechnung
    Zeiterfassung.Integration/   # End-to-End mit SQLite in-memory
  deploy/
    install-service.ps1
    backup.ps1
  README.md
```

## Hardware-Empfehlung

| Gerät | Zweck | Richtpreis |
|---|---|---|
| Vorhandener Praxisrechner (Windows 10/11, ≥4 GB RAM) | Anwendungshost — Stempeln + Admin | 0 € |
| NAS oder verschlüsselter USB-Stick | Tägliches automatisches Backup | 50–200 € |

Kein Touch-Display, kein RFID-Reader, keine Chipkarten nötig.

## Kritische Dateien (anzulegen)

- `src/Zeiterfassung.Core/Services/HashChainService.cs` — Kern der Manipulationssicherheit
- `src/Zeiterfassung.Data/Interceptors/AuditInterceptor.cs` — automatisches Audit-Log via EF Core
- `src/Zeiterfassung.Core/Services/StempelService.cs` — Stempel-Logik mit Validierung (kein Doppel-Kommen, Pause nur während Arbeit etc.)
- `src/Zeiterfassung.Core/Services/SaldoService.cs` — Überstunden-/Urlaubsberechnung
- `src/Zeiterfassung.Core/Services/ArbZGValidator.cs` — gesetzliche Warnungen
- `src/Zeiterfassung.Export/PdfStundenzettelReport.cs` — QuestPDF-Layout
- `src/Zeiterfassung.Export/DatevExporter.cs`
- `src/Zeiterfassung.Web/Pages/Terminal/Stempel.razor` — Kiosk-UI
- `src/Zeiterfassung.Web/Pages/Admin/IntegritaetPruefen.razor` — Hash-Chain-Check-UI

## Verifikation / Test-Plan

**Unit-Tests** (`Zeiterfassung.Core.Tests`)
- Hash-Chain: nach Manipulation eines Eintrags schlägt `VerifyChainAsync()` fehl mit korrektem Index
- Stempel-Reihenfolge: KOMMEN ohne vorheriges GEHEN → Fehler; PAUSE_ENDE ohne PAUSE_START → Fehler
- Saldo: Teilzeit 20 h/Woche, 5 Tage × 4 h Soll, ein Tag 6 h Ist → +2 h Saldo
- ArbZG: 10:01 h-Schicht → Warnung; 6:01 h ohne Pause → Warnung

**Integrationstest** (E2E mit SQLite)
- Vollständiger Tag: 4 MA stempeln Kommen, Pause-Start, Pause-Ende, Gehen → Monatsbericht enthält alle Einträge mit korrektem Saldo, PDF generiert ohne Fehler, Integritätsprüfung grün.

**Manueller Akzeptanztest in der Praxis**
1. Mini-PC aufstellen, Service starten, Touch-Display per Browser-Vollbild auf Terminal-Seite öffnen.
2. Alle 6 MA stempeln über eine Woche real.
3. Eine vergessene Stempelung simulieren → Korrekturantrag → Admin-Freigabe → Eintrag erscheint mit Markierung „Korrektur".
4. Monats-PDF + Excel an Steuerberater zur Probe schicken — Format-Feedback einholen.
5. Integritätsprüfung ausführen → muss grün sein.
6. Backup-Job einmal manuell laufen lassen → wiederherstellbar verifizieren (Restore auf Test-Instanz).

## Phasen-Aufwand (Schätzung)

| Phase | Inhalt | Aufwand |
|---|---|---|
| 1 — MVP | Stempel + Saldo + Admin + Export + GoBD-Kern | 3–4 Wochen |
| 2 — Erweitert | Urlaub/Krank, ArbZG, Feiertage, DATEV | 2–3 Wochen |
| 3 — Polish/Optional | RFID, Backup-Automation, E-Mail | 1–2 Wochen |

## Offene Punkte für nächste Iteration (nach Phase 1)

- Genaues Format Steuerberater (DATEV LODAS vs. einfache Excel) — beim Steuerberater erfragen
- Bundesland für Feiertage festlegen
- Soll-Stunden pro MA klären (Voll-/Teilzeit-Modell)
- Entscheidung RFID ja/nein nach MVP-Testbetrieb mit PIN
