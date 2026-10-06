# WorkspaceManager

Ein kleines Windows-Programm für den Infobereich (Tray), das deine Fenster dort wiederherstellt, wo sie hingehören – abhängig davon, an welchem Standort du gerade bist.

Du ordnest deine Fenster einmal an und speicherst das Layout für „Zuhause“ oder „Arbeit“. Erkennt WorkspaceManager später den Standort und die passenden Monitore, stellt es das Layout wieder her.

## Funktionen

- **Layouts pro Standort**: Fensterpositionen speichern und wiederherstellen, mit Prüfung der Monitorlandschaft. Einzelne Fenster lassen sich bearbeiten, aktualisieren oder entfernen.
- **Eigene Standorte**: Du legst beliebig viele Standorte selbst an (zum Beispiel Zuhause, zwei Büros, Homeoffice), jeweils mit eigenem Layout. „Aktuelles Netzwerk übernehmen“ trägt das Netzwerk, in dem du gerade bist, schon ein. Standorte lassen sich umbenennen und löschen.
- **Standorterkennung**: anhand der physischen Netzwerkadapter (VPN-Verbindungen bleiben außen vor).
- **Programme per Tastenkürzel**: `.exe`, `.bat`, `.cmd`, `.rdp` und Verknüpfungen starten, auf Wunsch direkt an die gespeicherte Position. Das Tastenkürzel nimmt das Programm einfach durch Drücken der Tasten auf.
- **Autostart mit Layout**: nach der Windows-Anmeldung öffnet WorkspaceManager die Programme des Layouts, die noch nicht laufen, und setzt sie an ihren Platz.
- **Logins per Tastenkürzel**: Zugangsdaten liegen im Windows-Anmeldeinformationsmanager und werden per Tastenkürzel in das aktive Fenster getippt (zum Beispiel für Netzwerkfreigaben oder Remotedesktop). Es wird kein Enter gesendet.
- **Darstellung**: Hell, Dunkel oder Windows-Design, ausklappbare Seitenleiste mit Icons.
- **Addons**: Zusätzliche Funktionen gibt es als optionale Erweiterungen mit eigener Installationsdatei, zum Beispiel **Fernwartung** (Verbindung zu TeamViewer, AnyDesk oder RustDesk per Tastenkürzel mit gespeicherten Geräten und Passwörtern). Siehe [docs/ADDONS.md](docs/ADDONS.md).

## Installation

1. Die neueste `WorkspaceManager-<Version>-x64.msi` von der [Release-Seite](https://github.com/anchristians92/Workspace-Manager/releases) laden.
2. Doppelklick und den Installer durchgehen. Er fragt nach Zielordner, Nutzungsart (privat oder kommerziell) und ob WorkspaceManager mit Windows starten soll.
3. Starten über das Startmenü (Ordner **WorkspaceManager**). Das Symbol erscheint im Infobereich neben der Uhr, ein Doppelklick öffnet die Einstellungen.

Voraussetzung: Windows 10 oder 11, 64 Bit. Die .NET-Laufzeit ist im Installer enthalten.

Standardmäßig wird nach `C:\Program Files\WorkspaceManager_Windows` installiert (auf deutschem Windows als „Programme“ angezeigt).

## Daten und Datenschutz

- Alle Einstellungen und Layouts liegen lokal unter `%LOCALAPPDATA%\PersonalWorkspaceManager`.
- Passwörter liegen **nicht** dort, sondern im Windows-Anmeldeinformationsmanager (verschlüsselt, an dein Windows-Konto gebunden).
- WorkspaceManager baut **keine Internetverbindung** auf, sendet keine Daten und enthält keine Telemetrie. Die Lizenzprüfung läuft vollständig offline.
- Beim Deinstallieren bleiben deine Daten erhalten. Wer sie loswerden will, löscht den Ordner oben und die Einträge `PersonalWorkspaceManager/…` im Anmeldeinformationsmanager.

## Lizenz

WorkspaceManager steht unter der [PolyForm Noncommercial License 1.0.0](LICENSE):

- **Privat und nicht-kommerziell** darfst du es kostenlos nutzen, weitergeben und verändern.
- **Für jede gewerbliche Nutzung** ist eine schriftliche Absprache und eine Lizenz nötig, siehe [COMMERCIAL.md](COMMERCIAL.md).

Das ist keine Open-Source-Lizenz im Sinne der OSI, der Quellcode ist aber einsehbar.

## Selbst bauen

Voraussetzungen: .NET 10 SDK und das WiX-Tool (`dotnet tool install --global wix --version 6.0.2`).

```powershell
.\build.ps1            # veröffentlicht die App, führt die Tests aus und erzeugt die MSI und die Addon-MSI in artifacts\
.\build.ps1 -SkipTests # ohne Selbsttest
```

Den Selbsttest der App kann man auch direkt starten: `WorkspaceManager.exe --self-test`.

## Aufbau

| Ordner | Inhalt |
| --- | --- |
| `src/WorkspaceManager` | Quellcode der App (C# / Windows Forms) |
| `installer` | WiX-Installer (MSI) |
| `addons/Remote` | Addon Fernwartung (TeamViewer, AnyDesk, RustDesk) |
| `tests/Remote.Tests` | Tests für die Logik des Addons Fernwartung |
| `tools/LicenseGenerator` | Fenster-Programm zum Erzeugen von Lizenzschlüsseln mit Protokoll (nur für den Herausgeber) |
| `tools/LicenseTool` | dasselbe auf der Kommandozeile, siehe [docs/LICENSING.md](docs/LICENSING.md) |
| `assets/branding` | Originalbilder von Logo und Tray-Symbol |
