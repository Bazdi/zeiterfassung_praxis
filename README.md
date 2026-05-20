# Zeiterfassung Arztpraxis — GoBD-konforme Lösung

Eine revisionssichere Zeiterfassungsanwendung für eine Arztpraxis (~6 Mitarbeiter) mit:

- ✅ **GoBD-Konformität**: SHA-256 Hash-Chain für Manipulationserkennung
- ✅ **.NET 8 / ASP.NET Core / Blazor Server**: Moderne Web-Technologie
- ✅ **SQLite mit WAL-Mode**: Lokale Datenbasis, einfaches Backup
- ✅ **Argon2id PIN-Hashing**: Sichere Authentifizierung mit Brute-Force-Schutz
- ✅ **ArbZG-Validierung**: Warnungen bei Gesetzesverletzungen (>10h, <30min Pause, etc.)
- ✅ **DSGVO/BDSG-konform**: Keine Gesundheitsdaten in DB, Verschlüsselung at rest
- ✅ **Unit & Integration Tests**: 12 Tests für kritische Services

## Phase 1 (MVP) — Gerüst vollständig aufgebaut

### Implementiert ✅

**Domain Layer (Zeiterfassung.Core)**
- 9 Entity Models + Enums
- 7 kritische Services (HashChain, Stempel, Saldo, ArbZG, Pin, WorkingPattern, LeaveEntitlement)

**Data Layer (Zeiterfassung.Data)**
- EF Core 8 DbContext (SQLite)
- Prepared für Migrations

**Web Layer (Zeiterfassung.Web)**
- Blazor Server App mit DI konfiguriert

**Testing**
- 12 Unit Tests (HashChainService + StempelService): **ALLE PASS ✅**

### Noch zu implementieren ❌

- Blazor Pages (Terminal, Admin, SelfService UI)
- QuestPDF + ClosedXML Export
- EF Core Migrations
- GoBD-Dokumentation (verfahrensdokumentation.md, DSFA, Berechtigungskonzept)

## Build-Status

```
dotnet build  ✅ Erfolgreich
dotnet test   ✅ 12/12 Tests bestanden
```

Detaillierte Dokumentation in [CLAUDE.md](CLAUDE.md) und [PLAN.md](PLAN.md).
