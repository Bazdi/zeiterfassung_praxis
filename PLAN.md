# Plan: Zeiterfassung für Arztpraxis (GoBD-konform)

## Context

Eine Arztpraxis mit ~6 Mitarbeitern führt aktuell die Zeiterfassung in einer Excel-Datei — diese ist weder revisionssicher noch manipulationssicher.

Rechtlicher Rahmen (zur Abgrenzung):
- **Vollständige Arbeitszeiterfassung** ist Pflicht aus dem BAG-Beschluss vom 13.09.2022 (1 ABR 22/21) in Verbindung mit § 3 Abs. 2 Nr. 1 ArbSchG (vorbereitend ergänzt durch den Referentenentwurf zum ArbZG). § 16 ArbZG selbst regelt nur Aushang und die Aufzeichnung von Mehrarbeit/Sonn-/Feiertagsarbeit.
- **GoBD** gilt für die daraus resultierenden, steuer- und lohnrechtlich relevanten Belege (Lohnabrechnungs-Grundlagen). Daraus folgt die Notwendigkeit der Unveränderbarkeit (Hash-Chain) für alle Datensätze, die in einen Lohnbeleg einfließen.
- **DSGVO/BDSG** trifft das System als Verarbeitung von Mitarbeiterdaten (inkl. Gesundheitsdaten bei Krankmeldungen, Art. 9 DSGVO).

Ziel: Eine eigenständige Anwendung, die das Excel ablöst, revisionssicher (lückenloses Audit-Log, unveränderliche Stempelungen, Manipulationsnachweis), für 6 Mitarbeiter angemessen dimensioniert ist und sowohl am Empfang (Stempelterminal) als auch im Admin-Bereich (Praxismanagement) bedienbar ist. Lohnbuchhaltung/Steuerberater erhält monatliche Exporte.

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
| Auth | 6-stellige PIN je MA (Argon2id-Hash) + Passwort+2FA für Admin | Keine zusätzliche Hardware nötig |
| PDF-Export | **QuestPDF** (Community-License) | OSS, moderne API. Community-License gilt bei Unternehmensumsatz < 1 Mio. USD — passt für Arztpraxis, dokumentieren |
| Excel-Export | **ClosedXML** | DATEV-/Steuerberater-tauglich |
| Zeitzonen | **NodaTime** | Robust gegen DST/Sommerzeit, klare UTC/Local-Trennung |
| Feiertage | **Nager.Date** | Deutscher Feiertagskatalog inkl. Bundesland-Differenzierung |
| Deployment | Windows Service via `sc.exe` oder NSSM, Auto-Start | Läuft headless im Hintergrund |
| Backup | Tägliches SQLite-Backup via `BackupDatabase()` + AES-256-Verschlüsselung (`age`/GPG), PowerShell + Aufgabenplaner | WAL-kompatibel; siehe Backup-Verfahren weiter unten |

### GoBD-/Revisionssicherheit (Kernanforderung)

1. **Append-only Stempelungen**: `TimeEntry`-Tabelle ist write-once. Keine UPDATEs/DELETEs.
2. **Korrekturen als neue Einträge**: Vergessenes Stempeln → MA stellt Antrag → Admin gibt frei → es entsteht ein neuer `Correction`-Eintrag mit Referenz auf Original (falls vorhanden), Begründung, Antragsteller, Freigeber, Zeitstempel. Original bleibt unangetastet.
3. **Audit-Log-Tabelle** (`AuditLog`): jede Mutation an irgendeiner Tabelle wird mit User, UTC-Timestamp, Action, Old-JSON, New-JSON protokolliert (über EF Core `SaveChangesInterceptor`).
4. **UTC-Zeitstempel** in DB, Anzeige in lokaler Zeit (Europe/Berlin) via NodaTime.
5. **Export-Signatur**: Monats-Exporte enthalten Hash der enthaltenen Datensätze + Kettenkopf-Hash → reproduzierbar, fälschungssicher.
6. **Datenaufbewahrung**: 10 Jahre per Default (GoBD), Löschung nur via Admin-Funktion mit Bestätigung und Audit-Eintrag.

#### Hash-Chain-Spezifikation

- **Zwei separate Ketten**: `TimeEntry`-Kette und `AuditLog`-Kette laufen parallel. `AuditLog` referenziert per FK auf die betroffene Zeile, bildet aber eine eigene Kette über alle Mutationen.
- **Payload `TimeEntry`** (Reihenfolge fest, UTF-8, `|`-getrennt, `null` → leerer String):
  `Id|EmployeeId|Type|TimestampUtc(ISO-8601, Nanosekunden)|Source|CorrectionOfId|CreatedByUserId|CreatedAtUtc`
- **Payload `AuditLog`** (analog): `Id|UserId|EntityName|EntityId|Action|OldJson|NewJson|TimestampUtc`
- **Hash** = `lowercase(hex(SHA-256(PrevHash || "\n" || Payload)))`. Genesis: `PrevHash = "0" × 64`.
- **Concurrency**: Anhängen je Kette serialisiert über `SemaphoreSlim` in `HashChainService`. EF-Transaktion umschließt (a) letzten Hash lesen, (b) neuen Hash berechnen, (c) INSERT. Bei 6 MA ist Kontention vernachlässigbar.
- **Verifikation**: `VerifyChainAsync(chain, fromId?, toId?)` iteriert in Insert-Reihenfolge, vergleicht jeden Hash neu berechnet gegen DB. Rückgabe: erster abweichender Eintrag oder `null` (Kette intakt).
- **Bei nachträglicher Manipulation** an einer Zeile bricht die Kette → maschinell prüfbar via Admin-Funktion „Integrität prüfen".

### Datenschutz (DSGVO/BDSG)

- **DSFA (Art. 35 DSGVO)** prüfen — systematische Mitarbeiterüberwachung erfordert i.d.R. eine Datenschutz-Folgenabschätzung. Mindestens dokumentierte Risikobewertung als Artefakt (`docs/dsfa.md`).
- **Art. 9 DSGVO**: Krankmeldungen sind Gesundheitsdaten. `LeaveRequest.Notiz` wird bei `Typ=KRANK` **nicht** gespeichert (DB-Constraint oder Service-Validierung); gespeichert werden nur Status, Von, Bis. Keine Diagnose, kein Attest-Inhalt.
- **Verschlüsselung at rest**: SQLite-Datei auf BitLocker-verschlüsseltem Volume. Backups vor Ablage auf NAS/USB AES-256-verschlüsselt (`age` oder GPG, asymmetrisch — Private-Key auf separatem Medium).
- **Berechtigungskonzept** als Dokument (`docs/berechtigungskonzept.md`): MA sieht nur eigene Daten, Admin sieht alle, Audit-Lese-Rolle separat.
- **Information der Beschäftigten nach § 26 BDSG** bei Einführung; falls Betriebsrat vorhanden, Mitbestimmung nach § 87 Abs. 1 Nr. 6 BetrVG (Praxis mit < 5 wahlberechtigten MA i.d.R. nicht relevant, aber dokumentieren).

### Backup-Verfahren

- **Kein File-Copy** der `.db`-Datei (WAL-Inkonsistenzen). Stattdessen `SqliteConnection.BackupDatabase()` (SQLite Online-Backup-API) in temporäre Datei.
- PowerShell-Job (Aufgabenplaner, täglich 23:30 Uhr):
  1. Backup-API → `temp\zeiterfassung.bak.db`
  2. AES-256-Verschlüsselung via `age` mit Public-Key der Praxis → `zeiterfassung-YYYYMMDD.age`
  3. Upload/Kopie auf NAS, zusätzlich auf USB-Stick (rotierend wöchentlich)
  4. Aufbewahrung: 30 tägliche, 12 monatliche, 7 jährliche
- **Restore-Drill quartalsweise**: ein Backup auf Testinstanz wiederherstellen, Integritätsprüfung durchlaufen, Stichproben-Saldo vergleichen. Ergebnis ins Verfahrensdoku-Protokoll.

### Authentifizierungs-Strategie

| Kontext | Methode |
|---|---|
| Stempeln am Praxisrechner | **6-stellige PIN pro MA** (von Admin als Initial-PIN gesetzt, MA muss bei Erst-Login ändern) |
| Self-Service (eigene Zeiten/Anträge) | Gleiche PIN — **kein automatischer Übergang** vom Stempel-Login: nach Stempelung Auto-Logout nach 5 s. Self-Service nur über expliziten Button + erneute PIN-Eingabe, 2 min Inaktivitäts-Timeout. |
| Admin (Praxismanagement) | Eigenes Admin-Konto mit Benutzername + Passwort (≥10 Zeichen) + optional TOTP-2FA |

PINs werden gehashed gespeichert (**Argon2id**, eindeutiger Salt pro MA). Brute-Force-Schutz: 5 Fehlversuche je MA → 60 s Sperre, danach 5 Min, danach Admin-Reset erforderlich. PIN-Eingabe-Feld ist `type=password` (verdeckt), Tastatur-Layout-unabhängig (nur Ziffern).

**PIN-Reset-Prozedur**: Admin generiert zufällige Initial-PIN (z.B. via `RandomNumberGenerator`) und teilt sie persönlich mit — kein E-Mail-Versand. `Employee.PinChangedAt = null` zwingt MA beim nächsten Login zur Änderung. Reset erzeugt zwei Audit-Einträge: Admin-Aktion + erstmalige MA-Änderung.

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
- Feiertage automatisch (Nager.Date + Bundesland-Konfiguration)
- ArbZG-Warnungen: Live-Hinweis bei > 10 h/Tag, fehlende Pause ab 6 h, < 11 h Ruhezeit
- DATEV-LODAS-Export (CSV-Format)

**Phase 3 (optional)**
- RFID-Reader-Integration (falls Hardware angeschafft)
- Backup-Automation mit Verschlüsselung + Mail-Bericht
- E-Mail-Benachrichtigung an Admin bei Korrekturanträgen

### Pausen- & ArbZG-Logik

- **6 h-Regel**: ab 6 h Arbeitszeit ohne Pause → Live-Warnung im Stempel-UI + Hinweis am Terminal beim nächsten Stempelversuch.
- **9 h-Regel**: ab 9 h Arbeitszeit gesamt < 45 min Pause → Warnung.
- **11 h Ruhezeit**: Warnung wenn `KOMMEN` < 11 h nach letztem `GEHEN`.
- **Offene Pause beim `GEHEN`**: Wenn MA `GEHEN` stempelt während `PAUSE_START` ohne `PAUSE_ENDE` aktiv ist → System bricht die Pause **nicht still ab**. Status „offene Pause" wird gesetzt, MA muss Korrekturantrag stellen (Pausenende eintragen).
- **Doppel-Stempelung verhindern**: `StempelService` weist identische Aktion innerhalb von 5 s als Duplikat ab (Client-seitiger Idempotenz-Token + Server-seitige Prüfung des letzten Eintrags).
- **Auto-Pause-Abzug** (z.B. „Mittagspause 30 min automatisch") wird im MVP **explizit nicht** umgesetzt — keine Echt-Stempelung wäre GoBD-problematisch. Falls später gewünscht: nur via Admin-Schalter pro MA mit Begründung im Audit.

## Datenmodell (Kern)

```
Employee(Id, Vorname, Nachname, Email?, PinHash, PinSalt, PinChangedAt?,
         IsActive, IsAdmin, FailedPinAttempts, LockedUntil?)
  -- Soll-Stunden und Urlaubsanspruch sind historisierbar → eigene Tabellen

WorkingTimePattern(Id, EmployeeId, GueltigAb, GueltigBis?,
                   MoStunden, DiStunden, MiStunden, DoStunden,
                   FrStunden, SaStunden, SoStunden,
                   CreatedAt, CreatedByUserId)
  -- append-only; Vertragsänderung erzeugt neuen Eintrag, alter erhält GueltigBis
  -- SaldoService joint pro Tag das zu diesem Datum gültige Pattern

LeaveEntitlement(Id, EmployeeId, Jahr, AnspruchTage, UebertragVorjahr,
                 SonderurlaubTage, CreatedAt, CreatedByUserId)
  -- ein Eintrag pro MA pro Jahr; Anpassungen erzeugen neuen Eintrag (historisch)

TimeEntry(Id, EmployeeId, Type[KOMMEN|GEHEN|PAUSE_START|PAUSE_ENDE],
          TimestampUtc, Source[TERMINAL|SELFSERVICE|KORREKTUR|MIGRATION],
          CorrectionOfId?, CreatedByUserId, CreatedAtUtc,
          PrevHash, Hash)
  -- append-only, nie UPDATE/DELETE

CorrectionRequest(Id, EmployeeId, RequestedTimestamp, Type, Begründung,
                  Status[OFFEN|GENEHMIGT|ABGELEHNT], ApprovedByUserId?,
                  ApprovedAt?, ResultingEntryId?)

LeaveRequest(Id, EmployeeId, Von, Bis, Typ[URLAUB|KRANK|SONSTIGES],
             Status, ApprovedByUserId?, Notiz?)
  -- Notiz bei Typ=KRANK gesperrt (Art. 9 DSGVO — keine Gesundheitsdetails)

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
  docs/
    verfahrensdokumentation.md    # GoBD: techn. + organisat. Beschreibung des DV-Systems
    berechtigungskonzept.md       # Rollen, Datenzugriffe
    dsfa.md                       # Datenschutz-Folgenabschätzung (Art. 35 DSGVO)
    betriebsanleitung-stempel.md  # MA-Handreichung
    restore-protokoll.md          # quartalsweise Restore-Drills
  README.md
```

Die Dokumente in `docs/` sind **Teil des MVP**, nicht „später" — die Verfahrensdokumentation ist GoBD-Pflicht.

## Betriebskonzept (Service + Kiosk-Browser)

Zwei getrennte Identitäten auf demselben Rechner:

- **Backend** läuft als Windows-Service unter eigenem Dienstkonto (oder `NetworkService`). Bindet ausschließlich an `http://127.0.0.1:5000`. Kein externer Port offen.
- **Frontend-Kiosk**: lokales Windows-Benutzerkonto `praxiskiosk` mit Auto-Login. Im `Startup`-Ordner Shortcut auf `msedge --kiosk http://127.0.0.1:5000/terminal --edge-kiosk-type=fullscreen`. Bildschirmschoner deaktiviert, Energiesparmodus aus.
- **Watchdog**: PowerShell-Script via Aufgabenplaner, alle 60 s — prüft sowohl Service-Status (`Get-Service`) als auch Kiosk-Browser-Prozess; startet bei Bedarf neu.
- **Versionsanzeige** im Admin-Bereich (Build-Hash + Version + DB-Schema-Version) — wird in der Verfahrensdokumentation referenziert.
- **Notfallformular**: PDF im Admin-Bereich abrufbar (und gedruckt im Empfang). Bei Service-Ausfall stempeln MA auf Papier, Admin pflegt später als Korrektureinträge (`Source=KORREKTUR`) nach.

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
- `src/Zeiterfassung.Core/Services/SaldoService.cs` — Überstunden-/Urlaubsberechnung; joint pro Tag das gültige `WorkingTimePattern`
- `src/Zeiterfassung.Core/Services/ArbZGValidator.cs` — gesetzliche Warnungen (6 h / 9 h / 11 h Ruhezeit)
- `src/Zeiterfassung.Core/Services/WorkingTimePatternService.cs` — Vertragshistorie verwalten
- `src/Zeiterfassung.Core/Services/LeaveEntitlementService.cs` — Urlaubsanspruch pro Jahr
- `src/Zeiterfassung.Export/PdfStundenzettelReport.cs` — QuestPDF-Layout
- `src/Zeiterfassung.Export/DatevExporter.cs`
- `src/Zeiterfassung.Web/Pages/Terminal/Stempel.razor` — Kiosk-UI
- `src/Zeiterfassung.Web/Pages/Admin/IntegritaetPruefen.razor` — Hash-Chain-Check-UI

## Verifikation / Test-Plan

**Unit-Tests** (`Zeiterfassung.Core.Tests`)
- Hash-Chain: nach Manipulation eines Eintrags schlägt `VerifyChainAsync()` fehl mit korrektem Index
- Hash-Chain Concurrency: 50 parallele Stempelungen via `Task.WhenAll` → Kette bleibt konsistent, keine Lücken/Duplikate
- Stempel-Reihenfolge: KOMMEN ohne vorheriges GEHEN → Fehler; PAUSE_ENDE ohne PAUSE_START → Fehler
- Doppel-Klick: zweimal KOMMEN innerhalb 5 s → zweite Aktion abgewiesen
- Saldo bei Vertragswechsel: WorkingTimePattern A bis 30.06. (20 h/Woche), B ab 01.07. (40 h/Woche) → Tag im Juni rechnet gegen A, Tag im Juli gegen B
- Saldo: Teilzeit 20 h/Woche, 5 Tage × 4 h Soll, ein Tag 6 h Ist → +2 h Saldo
- ArbZG: 10:01 h-Schicht → Warnung; 6:01 h ohne Pause → Warnung; KOMMEN < 11 h nach letztem GEHEN → Warnung
- DST/Sommerzeit: Tag-Aggregation am 23 h-Tag (März) und 25 h-Tag (Oktober) → korrekte Stundensumme, keine Off-by-One-Fehler

**Integrationstest** (E2E mit SQLite)
- Vollständiger Tag: 4 MA stempeln Kommen, Pause-Start, Pause-Ende, Gehen → Monatsbericht enthält alle Einträge mit korrektem Saldo, PDF generiert ohne Fehler, Integritätsprüfung grün.

**Manueller Akzeptanztest in der Praxis**
1. Mini-PC aufstellen, Service starten, Touch-Display per Browser-Vollbild auf Terminal-Seite öffnen.
2. Alle 6 MA stempeln über eine Woche real.
3. Eine vergessene Stempelung simulieren → Korrekturantrag → Admin-Freigabe → Eintrag erscheint mit Markierung „Korrektur".
4. Monats-PDF + Excel an Steuerberater zur Probe schicken — Format-Feedback einholen.
5. Integritätsprüfung ausführen → muss grün sein.
6. Backup-Job einmal manuell laufen lassen → wiederherstellbar verifizieren (Restore auf Test-Instanz). Danach quartalsweiser Restore-Drill protokollieren (`docs/restore-protokoll.md`).

## Phasen-Aufwand (Schätzung)

| Phase | Inhalt | Aufwand |
|---|---|---|
| 1 — MVP | Stempel + Saldo + Admin + Export + GoBD-Kern | 3–4 Wochen |
| 2 — Erweitert | Urlaub/Krank, ArbZG, Feiertage, DATEV | 2–3 Wochen |
| 3 — Polish/Optional | RFID, Backup-Automation, E-Mail | 1–2 Wochen |

## Offene Punkte für nächste Iteration (nach Phase 1)

- Genaues Format Steuerberater (DATEV LODAS vs. einfache Excel) — beim Steuerberater erfragen
- Bundesland für Feiertage festlegen
- Soll-Stunden pro MA klären (Voll-/Teilzeit-Modell, ggf. unterschiedlich je Wochentag)
- Urlaubsanspruch pro MA + ggf. Übertrag aus 2025
- Entscheidung RFID ja/nein nach MVP-Testbetrieb mit PIN

### Migration aus dem bestehenden Excel

- **Stichtag-Modell**: Altdaten verbleiben im Excel (Archiv-Kopie als PDF). Neusystem startet zum Stichtag mit Saldo- und Urlaubs-Vortrag, eingetragen als Genesis-`TimeEntry`/`LeaveEntitlement` mit `Source=MIGRATION` und Verweis auf Excel-Archiv im Audit-Log.
- **Kein Reverse-Engineering historischer Excel-Daten in die Hash-Chain** — würde den Genesis-Hash entwerten und Manipulationsnachweis untergraben.
- Excel-Archiv 10 Jahre aufbewahren (GoBD) auf verschlüsseltem Backup-Medium.
