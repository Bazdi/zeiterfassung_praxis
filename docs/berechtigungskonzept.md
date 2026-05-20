# Berechtigungskonzept Zeiterfassung
## Gemäß DSGVO Art. 5 Abs. 1 lit. f (Datensicherheit) und GoBD Rz. 100 ff.

**Version:** 1.0  
**Erstellt:** 2026-05-20

---

## 1. Rollenübersicht

| Rolle | Beschreibung | Authentifizierung |
|---|---|---|
| **Mitarbeiter** | Normaler Angestellter | 6-stellige PIN (Argon2id) |
| **Admin** | Praxismanagement | Benutzername + Passwort (≥10 Zeichen) + optional TOTP |
| **System** | Interne Prozesse (Backup, Watchdog) | Kein interaktiver Login |

---

## 2. Berechtigungsmatrix

| Funktion | Mitarbeiter (eigene Daten) | Mitarbeiter (fremde Daten) | Admin |
|---|:---:|:---:|:---:|
| Kommen/Gehen/Pause stempeln | ✅ | ❌ | ✅ |
| Eigene Zeiten ansehen | ✅ | ❌ | ✅ |
| Eigene Korrekturen anfordern | ✅ | ❌ | ✅ |
| Korrekturen genehmigen | ❌ | ❌ | ✅ |
| Urlaubsantrag stellen | ✅ | ❌ | ✅ |
| Urlaubsanträge genehmigen | ❌ | ❌ | ✅ |
| Mitarbeiter-Stammdaten verwalten | ❌ | ❌ | ✅ |
| PIN zurücksetzen | ❌ | ❌ | ✅ |
| Monats-Exporte erstellen | ❌ | ❌ | ✅ |
| Integritätsprüfung ausführen | ❌ | ❌ | ✅ |
| AuditLog lesen | ❌ | ❌ | ✅ |
| Soll-Stunden bearbeiten | ❌ | ❌ | ✅ |
| Urlaubsanspruch bearbeiten | ❌ | ❌ | ✅ |
| Systemkonfiguration | ❌ | ❌ | ✅ |

---

## 3. Datenzugriff im Detail

### 3.1 Mitarbeiter-Sicht (nach PIN-Login)
Nach PIN-Eingabe am Terminal:
- Sieht ausschliesslich eigene `TimeEntry`-Einträge
- Sieht eigenen Saldo und Urlaubskonto
- Kann Korrekturantrag für vergessene Stempelung stellen (mit Begründung)
- **Kein** Zugriff auf Daten anderer Mitarbeiter

### 3.2 Admin-Sicht
- Vollzugriff auf alle Mitarbeiterdaten
- Audit-Log ist lesbar (nicht veränderbar)
- Kann Korrekturen genehmigen/ablehnen
- Stellt Exporte für Steuerberater bereit
- Verwaltet Stammdaten (Soll-Stunden, Urlaubsanspruch)

### 3.3 Daten, die kein Admin sieht
- PIN-Klartexte (werden niemals gespeichert)
- TOTP-Secrets (Base32-encoded, nur Einrichtung)
- Diagnosen oder Attest-Inhalte bei Krankmeldungen (nie gespeichert, Art. 9 DSGVO)

---

## 4. Authentifizierung und Session-Management

### 4.1 PIN-Verfahren (Mitarbeiter am Terminal)
- 6-stellige numerische PIN
- Hash-Algorithmus: **Argon2id** (t=3, m=65536, p=4, 32-byte Output)
- Einzigartiger Salt pro Mitarbeiter
- **Brute-Force-Schutz:**
  - 5 Fehlversuche → 60 Sekunden Sperre
  - 6. Fehlversuch → 5 Minuten Sperre
  - Weitere Fehlversuche → Admin-Reset erforderlich
- Auto-Logout: 5 Sekunden nach erfolgreicher Stempelung
- Self-Service: 2 Minuten Inaktivitäts-Timeout

### 4.2 Admin-Authentifizierung
- Benutzername + Passwort (≥10 Zeichen, Komplexitätsanforderungen)
- Optional: TOTP-2FA (Authenticator App)
- Session-Timeout: [noch zu konfigurieren]

### 4.3 PIN-Änderung bei Erstlogin
- Admin setzt Erstpin → Mitarbeiter muss bei Erstlogin ändern
- `Employee.PinChangedAt = null` signalisiert erzwungene Änderung
- PIN-Reset erzeugt zwei Audit-Einträge

---

## 5. Physische Zugangskontrolle

- Praxis-PC mit Zeiterfassungs-Software steht im gesicherten Empfangsbereich
- Kiosk-Browser läuft im Vollbild (kein Desktop-Zugriff ohne Tastenkombination)
- Kiosk-Benutzerkonto `praxiskiosk` hat eingeschränkte Windows-Rechte
- Wartungszugriff auf Server-Komponente nur mit Admin-Windows-Anmeldung

---

## 6. Protokollierung und Nachvollziehbarkeit

Alle sicherheitsrelevanten Ereignisse werden im AuditLog protokolliert:
- Fehlgeschlagene PIN-Versuche (im Employee-Datensatz: `FailedPinAttempts`)
- Admin-Aktionen (Korrekturgenehmigung, PIN-Reset, Stammdatenänderung)
- Alle Datenbankänderungen automatisch via EF Core `SaveChangesInterceptor`

---

## 7. Änderungshistorie

| Version | Datum | Änderung | Verantwortlich |
|---|---|---|---|
| 1.0 | 2026-05-20 | Erstversion | [Name] |

---

*Zu überprüfen und zu aktualisieren bei: Systemänderungen, Rollenwechsel, Personaländerungen, jährlicher Revision.*
