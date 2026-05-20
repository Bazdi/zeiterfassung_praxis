# Zeiterfassung Arztpraxis — Claude Code Context

## Projektübersicht

GoBD-konforme Zeiterfassungsanwendung für eine Arztpraxis (~6 Mitarbeiter).

**Tech-Stack:**
- Backend: .NET 8 / ASP.NET Core
- Frontend: Blazor Server
- Datenbank: SQLite (WAL-Mode)
- Authentifizierung: 6-stellige PIN (Argon2id) + Admin-Passwort
- Hash-Chain: SHA-256 für GoBD-Manipulationserkennung

## Projektstruktur

```
src/
  Zeiterfassung.Core/          # Domain Models + Services (HashChain, Stempel, Saldo, etc.)
    Models/                    # Enums + Entities
    Services/                  # Core business logic
  Zeiterfassung.Data/          # EF Core DbContext + Migrations
  Zeiterfassung.Web/           # Blazor Server App (Terminal, Admin, SelfService)
  Zeiterfassung.Export/        # QuestPDF + ClosedXML Reports
  Zeiterfassung.Service/       # Windows Service Host (optional, can integrate into Web)
tests/
  Zeiterfassung.Core.Tests/    # Unit Tests (Hash-Chain, Saldo, ArbZG, etc.)
  Zeiterfassung.Integration/   # E2E Tests with SQLite
```

## Kritische Services

1. **HashChainService** — SHA-256 Hash-Chain für Manipulationserkennung
   - Payload-Serialisierung: `Id|EmployeeId|Type|TimestampUtc|Source|CorrectionOfId|CreatedByUserId|CreatedAtUtc`
   - Append-only, Verifizierung via `VerifyChainAsync()`

2. **StempelService** — Validiert Stempelreihenfolge (KOMMEN→PAUSE_START→PAUSE_END→GEHEN)
   - Duplikat-Schutz <5 Sekunden
   - ArbZG-Warnungen live

3. **SaldoService** — Überstunden-Berechnung (Ist − Soll), DST-sicher via NodaTime

4. **ArbZGValidator** — Warnt vor Gesetzesverletzungen (>10h, <30min Pause ab 6h, <11h Ruhezeit)

5. **PinService** — Argon2id Hashing + Brute-Force-Schutz (5 Versuche → 60s/5min Sperre)

## Datenmodell

**Append-Only Tabellen:**
- `TimeEntry` — PrevHash + Hash für Kettenverifikation
- `AuditLog` — Automatisch via EF Core SaveChangesInterceptor

**Historisierbare Tabellen:**
- `WorkingTimePattern` — ValidFrom/ValidUntil für Vertragsänderungen
- `LeaveEntitlement` — Pro MA + Jahr

**Weitere Kernentitäten:**
- `Employee` — 6-stellige PIN + Admin-Flag
- `CorrectionRequest` — Nachträge mit Admin-Genehmigung
- `LeaveRequest` — Urlaub/Krank (Notiz bei KRANK gesperrt → Art. 9 DSGVO)
- `User` — Admin-Account mit optional TOTP-2FA

## UI-Seiten (geplant)

**Terminal (Kiosk):**
- `/terminal/stempel` — Große Touch-Buttons, MA auswählen → PIN → Aktion

**Self-Service:**
- `/selfservice/meinezeiten` — Tages-/Wochen-/Monatsansicht + Saldo
- `/selfservice/korrekturantrag` — Antrag für vergessene Stempelung

**Admin:**
- `/admin/mitarbeiter` — Stammdaten, PIN-Reset
- `/admin/korrekturen` — Anträge genehmigen
- `/admin/exporte` — PDF-Stundenzettel, Excel-Bericht für Steuerberater
- `/admin/integritaetspruefen` — Hash-Chain-Verifizierung

## Authentifizierung

- **Kiosk**: 6-stellige PIN (Argon2id, unique salt), Auto-Logout nach 5s
- **Admin**: Benutzername + Passwort (≥10 Zeichen) + optional TOTP
- **Brute-Force**: 5 Fehlversuche → 60s Sperre → 5 Min → Admin-Reset

## Datenschutz (DSGVO/BDSG)

- **Art. 9 (Gesundheitsdaten)**: Keine Krankmeldungs-Notizen in DB speichern
- **Verschlüsselung at rest**: SQLite auf BitLocker-Volume
- **Backup**: Daily backup via SqliteConnection.BackupDatabase() → AES-256-Verschlüsselt via `age`
- **Verfahrensdokumentation**: Pflicht in `docs/verfahrensdokumentation.md` (Teil des MVP)

## Getestete Szenarien

(Unit + Integration Tests)
- Hash-Chain: Manipulation erkennen, Concurrency (50 parallele Tasks)
- Stempel-Validierung: Reihenfolge, Duplikat-Schutz
- Saldo: Vertragswechsel, DST (23h/25h Tage)
- ArbZG: Warnungen bei >10h, <30/45min Pause, <11h Ruhezeit

## Konfiguration

Keine besonderen Konfigurationen außer der Standard-ASP.NET Core `appsettings.json`.
SQLite-Datei liegt lokal im App-Verzeichnis (`zeiterfassung.db`).

## Bekannte Einschränkungen (Phase 1)

- Kein RFID-Reader (PIN-basiert, kann später hinzugefügt werden)
- Automatische Pausenabzüge nicht implementiert (GoBD-problematisch)
- Excel-Format mit Steuerberater noch abzustimmen

## Nächste Schritte nach MVP

1. Integration Tests erweitern (E2E Szenarien)
2. Blazor-UI vollständig implementieren + Styling
3. PDF-Export mit QuestPDF
4. DATEV LODAS Export
5. Backup-Automation + Restore-Drill dokumentieren
