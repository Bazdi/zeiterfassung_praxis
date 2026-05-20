# Verfahrensdokumentation Zeiterfassung
## GoBD-Verfahrensdokumentation gemäß § 147 AO und GoBD Rz. 152 ff.

**Version:** 1.0  
**Erstellt:** 2026-05-20  
**Gültig ab:** [Stichtag eintragen]  
**Verantwortlich:** [Praxisinhaber/-in]

---

## 1. Allgemeine Beschreibung des DV-Systems

### 1.1 Zweck des Systems
Elektronische Arbeitszeiterfassung für die Mitarbeiter der Arztpraxis. Ersetzt die manuelle Excel-Tabelle. Grundlage für Lohnabrechnung und Steuerberater-Exporte.

### 1.2 Rechtsgrundlagen
- **BAG-Beschluss** 13.09.2022 (1 ABR 22/21) i.V.m. § 3 Abs. 2 Nr. 1 ArbSchG — vollständige Arbeitszeiterfassung
- **§ 16 ArbZG** — Aufzeichnung von Mehrarbeit, Sonn- und Feiertagsarbeit
- **GoBD 2019** (BMF-Schreiben vom 28.11.2019) — Revisionssicherheit, Unveränderbarkeit, Nachvollziehbarkeit
- **DSGVO / BDSG** — Datenschutz bei Mitarbeiterdaten
- **§ 147 AO** — Aufbewahrungsfristen (10 Jahre)

### 1.3 Zeitliche Geltung
Das System ist ab dem Stichtag [DATUM] führend für die Zeiterfassung. Altdaten verbleiben in der Excel-Archivkopie, die ebenfalls 10 Jahre aufzubewahren ist.

---

## 2. Systemarchitektur

### 2.1 Technische Umgebung

| Komponente | Details |
|------------|---------|
| Betriebssystem | Windows 10/11 Pro |
| Anwendung | .NET 8 / ASP.NET Core / Blazor Server |
| Datenbank | SQLite (WAL-Mode, lokale Datei) |
| Datenbankdatei | `[Installationspfad]\zeiterfassung.db` |
| Backup | Daily via PowerShell, AES-256-verschlüsselt |
| Netzwerk | Nur Loopback (127.0.0.1:5000), kein Internetzugang |
| Authentifizierung | PIN (6-stellig, Argon2id) + Admin-Passwort |

### 2.2 Installationspfad
`[Zu befüllen nach Installation]`

### 2.3 Datenbankschema-Version
Version 1.0 — [Revisionsdatum]

---

## 3. Datenfluss und Verarbeitungsschritte

### 3.1 Stempelvorgang (KOMMEN / GEHEN / PAUSE)

```
Mitarbeiter wählt sich → PIN-Eingabe (Argon2id-Verifikation) →
Stempel-Validierung (Reihenfolge prüfen) →
TimeEntry in DB anlegen (append-only) →
SHA-256 Hash berechnen und in Kette einreihen →
AuditLog-Eintrag erstellen
```

### 3.2 Korrekturprozess
1. Mitarbeiter stellt Korrekturantrag mit Begründung
2. Admin prüft und genehmigt/lehnt ab
3. Bei Genehmigung: neuer TimeEntry mit `Source=KORREKTUR`, Referenz auf Antrag
4. Original-Eintrag bleibt unverändert
5. Audit-Log dokumentiert beide Schritte

### 3.3 Monatsexport
- Admin generiert PDF-Stundenzettel pro Mitarbeiter
- Excel-Sammeldatei für Steuerberater
- Export enthält Hash-Fingerprint der eingeschlossenen Datensätze

---

## 4. GoBD-Manipulationsschutz (Hash-Chain)

### 4.1 Algorithmus
- **Verfahren:** SHA-256
- **Initialer Hash (Genesis):** `0000...0000` (64 Nullen)
- **Berechnung:** `Hash = lowercase(hex(SHA-256(PrevHash + "\n" + Payload)))`
- **Payload TimeEntry:** `Id|EmployeeId|Type|TimestampUtc|Source|CorrectionOfId|CreatedByUserId|CreatedAtUtc`
- **Payload AuditLog:** `Id|UserId|EntityName|EntityId|Action|OldJson|NewJson|TimestampUtc`

### 4.2 Zwei separate Ketten
1. **TimeEntry-Kette** — alle Zeiterfassungsstempel
2. **AuditLog-Kette** — alle Systemänderungen

### 4.3 Concurrency-Schutz
Aneinanderhängung je Kette über `SemaphoreSlim(1,1)` serialisiert — keine parallelen Schreibzugriffe möglich.

### 4.4 Integritätsprüfung
Admin-Funktion `/admin/integritaet` prüft die gesamte Kette neu. Erkennt:
- Nachträgliche Änderungen an Zeitstempeln
- Lücken in der Kette
- Manipulierte Einträge

**Empfehlung:** Prüfung monatlich vor Lohnabrechnung und quartalsweise im Rahmen des Restore-Drills.

---

## 5. Append-Only und Unveränderbarkeit

### 5.1 Technische Maßnahmen
- **EF Core Interceptor** (`AuditInterceptor`): wirft `InvalidOperationException` bei UPDATE/DELETE auf `TimeEntry` oder `AuditLog`
- **Datenbankebene:** Keine DELETE-Rechte für den Anwendungsnutzer (im produktiven Setup via SQLite-Datei-Permissions)
- Korrekturen werden **nie** als Update alter Einträge durchgeführt, sondern als neue Einträge mit `Source=KORREKTUR`

### 5.2 Korrekturbegründung (§ 146 Abs. 4 AO)
Jeder Korrektureintrag enthält zwingend:
- Begründung (Freitext)
- Antragsteller (EmployeeId)
- Genehmiger (AdminUserId)
- Zeitstempel der Genehmigung
- Referenz auf den Ursprungseintrag (wenn vorhanden)

---

## 6. Datensicherung

### 6.1 Backup-Verfahren
- **Technologie:** `SqliteConnection.BackupDatabase()` (Online-Backup-API, WAL-kompatibel)
- **Zeitplan:** Täglich 23:30 Uhr via Windows-Aufgabenplaner
- **Verschlüsselung:** AES-256 via `age` mit Public-Key der Praxis
- **Speicherorte:** NAS (täglich) + rotierender USB-Stick (wöchentlich)

### 6.2 Aufbewahrungsfristen Backups
- 30 tägliche Backups
- 12 monatliche Backups
- 7 jährliche Backups
- Nach Ablauf: sicheres Löschen

### 6.3 Restore-Drill
**Quartalsweise** (dokumentiert in `docs/restore-protokoll.md`):
1. Letztes Backup auf Testinstanz wiederherstellen
2. Integritätsprüfung ausführen
3. Stichproben-Saldo vergleichen
4. Ergebnis protokollieren

---

## 7. Aufbewahrungsfristen

| Datenkategorie | Aufbewahrungsfrist | Rechtsgrundlage |
|---|---|---|
| Zeiterfassungsbelege (TimeEntry) | 10 Jahre | § 147 AO, GoBD |
| AuditLog | 10 Jahre | GoBD |
| Excel-Archiv (Vorperiode) | 10 Jahre | § 147 AO |
| Lohnabrechnungen (externer Steuerberater) | 10 Jahre | § 147 AO |

Löschung nach Ablauf nur nach expliziter Admin-Bestätigung und mit Audit-Eintrag.

---

## 8. Zugriffsberechtigungen

Detailliert in `docs/berechtigungskonzept.md`.

**Kurzübersicht:**
- **Mitarbeiter:** Stempeln via PIN, eigene Zeiten einsehen, Korrekturanträge stellen
- **Admin:** Vollzugriff, Korrekturen genehmigen, Exporte, Integritätsprüfung, PIN-Reset

---

## 9. Systemwartung und Versionierung

### 9.1 Softwareversionen
Die aktuelle Anwendungsversion sowie DB-Schema-Version sind im Admin-Bereich unter "System" abrufbar.

### 9.2 Änderungsmanagement
Jede Änderung am Quellcode wird via Git protokolliert. Die `PLAN.md` enthält die vollständige Architektur.

### 9.3 Schulung
- Mitarbeiter erhalten Handreichung `docs/betriebsanleitung-stempel.md`
- Admin erhält Schulung durch [Name des Einführungsverantwortlichen]

---

## 10. Unterschriften und Freigabe

| Rolle | Name | Datum | Unterschrift |
|---|---|---|---|
| Praxisinhaber/-in | | | |
| IT-Verantwortlicher | | | |
| Datenschutzbeauftragter (falls vorhanden) | | | |

---

*Dieses Dokument ist gemäß GoBD Rz. 155 aufzubewahren und bei Systemänderungen zu aktualisieren.*
