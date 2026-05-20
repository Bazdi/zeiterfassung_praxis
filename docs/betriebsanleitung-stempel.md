# Betriebsanleitung: Stempel-Terminal

**Für Mitarbeiter der Praxis** — Kurzanleitung zur täglichen Zeiterfassung.

---

## So stempeln Sie

### Kommen

1. Tippen Sie auf Ihren **Namen** im Terminal
2. Geben Sie Ihre **6-stellige PIN** ein
3. Drücken Sie **KOMMEN** (grüner Button)
4. Das Terminal bestätigt mit Uhrzeit — fertig

### Gehen

Gleiche Schritte wie Kommen, dann **GEHEN** (roter Button) drücken.

### Pause

- **Pause beginnen:** Name → PIN → **PAUSE START** (gelber Button)
- **Pause beenden:** Name → PIN → **PAUSE ENDE** (blauer Button)

> Das Terminal meldet sich nach 5 Sekunden automatisch ab.

---

## Vergessene Stempelung nachmelden

Falls Sie vergessen haben zu stempeln:

1. Öffnen Sie im Browser: `http://praxis-pc:5000/selfservice/korrektur`
2. Wählen Sie Ihren Namen und geben Sie Ihre PIN ein
3. Wählen Sie Typ (Kommen/Gehen/Pause), geben Sie Datum + Uhrzeit und eine **Begründung** ein
4. Der Antrag wird an den Admin gesendet — nach Genehmigung erscheint der Eintrag in Ihrer Übersicht

---

## Eigene Zeiten einsehen

Unter `http://praxis-pc:5000/selfservice` (Name + PIN) sehen Sie:
- Alle Stempelungen des aktuellen Monats
- Überstunden-Saldo
- Status laufender Anträge

---

## Wichtige Hinweise

- **PIN vergessen?** Sprechen Sie den Admin an — er setzt eine neue Initial-PIN
- **Terminal reagiert nicht?** Bildschirm antippen oder Admin rufen
- **Strom-/Serverausfall:** Papierformular (im Empfang) ausfüllen — Admin trägt nach

---

## Häufige Fehlermeldungen

| Meldung | Bedeutung |
|---|---|
| „Doppelstempelung erkannt" | Sie haben denselben Typ innerhalb von 5 Sek. zweimal gedrückt — ignorieren, einmal ist genug |
| „Gesperrt für Xs" | 5 Mal falsche PIN eingegeben — kurz warten, dann erneut versuchen |
| „Account gesperrt — Admin kontaktieren" | Zu viele Fehlversuche — Admin muss PIN zurücksetzen |
| „Ungültige Stempelung" | Z.B. nochmal KOMMEN ohne vorheriges GEHEN — ggf. Korrekturantrag stellen |

---

*Bei Fragen wenden Sie sich an die Praxisleitung.*
