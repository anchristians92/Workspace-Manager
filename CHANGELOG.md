# Changelog

## 1.0.0

Erste veröffentlichte Version.

- Fensterlayouts pro Standort speichern und wiederherstellen, mit Prüfung der Monitorlandschaft
- Standorterkennung über die physischen Netzwerkadapter
- Programme, Skripte (`.bat`, `.cmd`), RDP-Dateien und Verknüpfungen per Tastenkürzel starten, auf Wunsch direkt an die gespeicherte Position
- Programme des Layouts nach der Windows-Anmeldung automatisch öffnen
- Logins im Windows-Tresor speichern und per Tastenkürzel eintippen
- Hell, Dunkel oder Windows-Design, ausklappbare Seitenleiste mit Icons
- MSI-Installer
- Addons als optionale Erweiterungen mit eigener Installationsdatei, mit Reiter „Addons“ zum Ein- und Ausschalten
- Addon „Fernwartung“: Verbindung zu TeamViewer, AnyDesk und RustDesk per Tastenkürzel, mit gespeicherten Geräten und Passwörtern
- Addon „Passwort-Tresor“: Einträge aus Pleasant Password Server per Tastenkürzel suchen und in das aktive Fenster tippen oder kopieren; weitere Passwort-Programme lassen sich als eigene Anbindung ergänzen
- Programme können als Administrator oder als anderer Benutzer (mit einem gespeicherten Login) gestartet werden
- Einstellungen der Addons öffnen sich über Einstellungen → Addons
- `build.ps1 -Quick` und `-AddonOnly` für schnelle Entwicklungs-Builds
- Beim Wechsel des Standorts oder der Monitore (zum Beispiel am Dock) werden fehlende Programme des Layouts geöffnet und angeordnet
