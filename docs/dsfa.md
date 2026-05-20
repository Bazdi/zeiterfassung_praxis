# Datenschutz-Folgenabschätzung (DSFA)
## Gemäß Art. 35 DSGVO — Zeiterfassungssystem Arztpraxis

**Erstellt:** 2026-05-20  
**Verantwortliche Stelle:** [Name der Arztpraxis]  
**Datenschutzbeauftragter:** [Name oder "nicht bestellungspflichtig nach § 38 BDSG"]

---

## 1. Notwendigkeit einer DSFA

### 1.1 Schwellenwertprüfung (Art. 35 Abs. 1 DSGVO)

Eine DSFA ist erforderlich, wenn eine Verarbeitung aufgrund ihrer Art, Ihres Umfangs, ihrer Umstände und ihrer Zwecke voraussichtlich ein hohes Risiko für die Rechte und Freiheiten natürlicher Personen mit sich bringt.

**Begründung Pflicht:** Systematische Überwachung des Verhaltens von Beschäftigten gemäß Erwägungsgrund 91 DSGVO und DSK-Blacklist.

### 1.2 Einschlägige Risikomerkmale (nach EDSA Guidelines 09/2022)
- ☑ Systematische Überwachung (lückenlose Zeiterfassung)
- ☑ Datenverarbeitung über vulnerable Personen (Beschäftigte in Abhängigkeitsverhältnis)
- ☐ Massenverarbeitung (nur ~6 Personen — kein Hochrisiko-Merkmal)
- ☐ Sensitive Daten (nur wenn Krankmeldungen erfasst — technisch auf Minimum beschränkt)

---

## 2. Beschreibung der Verarbeitungstätigkeit

### 2.1 Zweck der Verarbeitung
Erfüllung der gesetzlichen Pflicht zur vollständigen Arbeitszeiterfassung (§ 3 Abs. 2 Nr. 1 ArbSchG i.V.m. BAG-Beschluss 13.09.2022) sowie Erstellung von Lohnberechnungsgrundlagen für den Steuerberater.

### 2.2 Art der verarbeiteten Daten

| Datenkategorie | Beschreibung | Rechtsgrundlage |
|---|---|---|
| Identifikationsdaten | Vor-/Nachname, E-Mail | Art. 6 Abs. 1 lit. c DSGVO (gesetzliche Pflicht) |
| Zeiterfassungsdaten | Kommen, Gehen, Pausen (Zeitstempel) | Art. 6 Abs. 1 lit. c DSGVO |
| Soll-Stunden / Vertragsdaten | Wochenstunden je Wochentag | Art. 6 Abs. 1 lit. b DSGVO (Vertragserfüllung) |
| Urlaubsdaten | Von, Bis, Typ (Urlaub/Sonstiges) | Art. 6 Abs. 1 lit. b/c DSGVO |
| Krankmeldungsdaten | **Nur:** Von, Bis, Status (kein Attest-Inhalt) | Art. 9 Abs. 2 lit. b DSGVO i.V.m. § 26 BDSG |
| PIN-Hash | Argon2id-Hash, kein Klartext | Art. 6 Abs. 1 lit. c DSGVO |
| Audit-Log | Systemänderungen mit User-ID | Art. 6 Abs. 1 lit. c DSGVO (GoBD-Pflicht) |

### 2.3 Nicht verarbeitete Daten (Minimierungsmaßnahmen)
- ❌ Diagnosen, Krankheitsbilder (technisch ausgeschlossen durch DB-Constraint)
- ❌ Attest-Inhalte
- ❌ Standortdaten, Biometrische Daten
- ❌ Kommunikationsinhalte

### 2.4 Empfänger der Daten
- **Steuerberater:** Monatliche Exporte (PDF + Excel) per verschlüsselter E-Mail/USB
- **Kein** Cloud-Anbieter, kein Dritter

### 2.5 Aufbewahrungsfristen
10 Jahre ab Ende des Beschäftigungsverhältnisses gemäß § 147 AO (GoBD-Pflicht).

---

## 3. Notwendigkeit und Verhältnismäßigkeit

### 3.1 Zweckbindung
Daten werden ausschließlich für Arbeitszeiterfassung und Lohnbuchhaltung verwendet. Keine Verhaltensüberwachung über reine Zeiterfassung hinaus.

### 3.2 Datenminimierung
- Nur erforderliche Felder werden gespeichert
- Keine Notizen bei Krankmeldungen (Art. 9 DSGVO)
- Kein biometrisches Verfahren, nur PIN

### 3.3 Verhältnismäßigkeit
- Kleine Praxis (6 MA) → überschaubare Datenmenge
- Gesetzliche Erfassungspflicht → keine Alternativen zur Speicherung

---

## 4. Risikoanalyse

### 4.1 Identifizierte Risiken

| Risiko | Eintrittswahrsch. | Schwere | Gesamt | Massnahme |
|---|:---:|:---:|:---:|---|
| Unbefugter Datenzugriff (Hacker) | Niedrig | Mittel | **Niedrig** | Kein Internetzugang, Loopback-only |
| Datenverlust (Hardwaredefekt) | Mittel | Hoch | **Mittel** | Tägliche verschlüsselte Backups, NAS + USB |
| Datenverlust (Löschen/Fehlbedienung) | Niedrig | Hoch | **Niedrig** | Append-only DB, Soft-Delete nicht möglich |
| Manipulation von Einträgen | Niedrig | Hoch | **Niedrig** | SHA-256 Hash-Chain, sofortige Erkennung |
| Mitarbeiterüberwachung (Missbrauch) | Niedrig | Hoch | **Niedrig** | Nur Zeitstempel, kein Standort/Inhalt |
| Versehentliche Offenbarung | Niedrig | Mittel | **Niedrig** | Berechtigungskonzept, PIN-Auth |
| Zugriff durch Dritte (Steuerberater) | Mittel | Niedrig | **Niedrig** | Nur Exporte, keine Direktverbindung |
| Verlust physischer Datenträger | Niedrig | Mittel | **Niedrig** | AES-256 Verschlüsselung der Backups |

### 4.2 Bewertung
Das Restrisiko nach Anwendung aller Massnahmen wird als **akzeptabel** eingestuft.

---

## 5. Massnahmen zum Schutz der Rechte der Betroffenen

### 5.1 Information der Beschäftigten (§ 26 BDSG, Art. 13 DSGVO)
- Schriftliche Information vor Einführung des Systems
- Erläuterung von Zweck, Rechtsgrundlage, Aufbewahrungsdauer
- Information über Auskunfts- und Berichtigungsrechte

### 5.2 Mitbestimmung
- Praxis mit < 5 wahlberechtigten Mitarbeitern → i.d.R. kein Betriebsrat (§ 1 BetrVG)
- Falls Betriebsrat vorhanden: Mitbestimmungsrecht nach § 87 Abs. 1 Nr. 6 BetrVG beachten

### 5.3 Betroffenenrechte
| Recht | Umsetzung |
|---|---|
| Auskunft (Art. 15) | Admin-Export eigener Daten |
| Berichtigung (Art. 16) | Korrekturantrag-Workflow mit Admin-Freigabe |
| Einschränkung (Art. 18) | Manuelle Admin-Massnahme |
| Löschung (Art. 17) | Erst nach Ablauf der Aufbewahrungspflicht (GoBD) |
| Datenübertragbarkeit (Art. 20) | PDF/Excel-Export |

---

## 6. Konsultation des Datenschutzbeauftragten

[ ] Kein DSB vorhanden (Praxis < 20 Beschäftigte mit automatisierter Datenverarbeitung — Prüfung § 38 BDSG)  
[ ] DSB konsultiert am: _______________  
[ ] DSB hat keine Einwände

---

## 7. Ergebnis

Die Verarbeitung ist nach Umsetzung aller genannten Massnahmen datenschutzrechtlich zulässig und verhältnismässig. Das Restrisiko für die Rechte der Betroffenen ist als gering einzustufen.

**Eine Vorabkonsultation der Aufsichtsbehörde (Art. 36 DSGVO) ist nicht erforderlich.**

---

## 8. Genehmigung und Unterschriften

| Rolle | Name | Datum | Unterschrift |
|---|---|---|---|
| Verantwortlicher (Praxisinhaber/-in) | | | |
| Datenschutzbeauftragter (falls vorhanden) | | | |

---

## 9. Überprüfung

Diese DSFA ist bei wesentlichen Änderungen der Verarbeitung zu aktualisieren, mindestens jedoch jährlich zu überprüfen.

**Nächste Überprüfung:** 2027-05-20
