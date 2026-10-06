# Addons

Addons erweitern WorkspaceManager um zusätzliche Funktionen. Sie sind **optional**: Das Hauptprogramm läuft ohne sie, und jedes Addon hat seine eigene Installationsdatei.

## Installieren und verwalten

1. WorkspaceManager installieren.
2. Die Installationsdatei des Addons ausführen (zum Beispiel `WorkspaceManager-Addon-Fernwartung-<Version>-x64.msi`). Sie legt die Dateien in den Ordner `Addons` des Programms und beendet dabei ein laufendes WorkspaceManager.
3. WorkspaceManager neu starten.
4. Unter **Einstellungen → Addons** steht jedes installierte Addon mit einem Schalter zum Ein- und Ausschalten. Ein Addon, das eigene Einstellungen hat, bekommt dort einen Knopf „Einstellungen öffnen“. Die Einstellungen eines Addons sind nur sichtbar, solange es installiert und eingeschaltet ist.

Zum Entfernen deinstallierst du das Addon unter Windows „Apps“.

## Mitgelieferte Addons

### Fernwartung

Verbindet per Tastenkürzel mit **TeamViewer**, **AnyDesk** oder **RustDesk**.

- TeamViewer nimmt im ID-Feld auch einen **DNS-Namen** (zum Beispiel `pc01.firma.local`) oder eine **IP-Adresse**. Das gilt für das Verbindungsfenster und für gespeicherte Geräte.
- Das Tastenkürzel öffnet das Fenster **Fernwartung Verbindung herstellen**. Dort gibst du eine ID oder einen gespeicherten Gerätenamen ein.
- Unter **Einstellungen → Fernwartung** legst du pro Programm fest:
  - **welche Programme ihr nutzt**: Nur die gewählten erscheinen im Verbindungsfenster und in den Einstellungen. Bei nur einem Programm (zum Beispiel nur TeamViewer) entfällt die Auswahl überall. Ohne eigene Wahl bietet das Addon die Programme an, die auf dem Rechner gefunden werden,
  - den Pfad zum Programm (wird automatisch gesucht, ist aber änderbar),
  - das Tastenkürzel,
  - **Passwörter** (zum Beispiel „Lokale Clients“). Das Passwort, das als Standard markiert ist, wird im Verbindungsfenster automatisch benutzt. Für eine externe Verbindung wählst du „Eigenes Passwort eingeben“.
  - **Geräte** mit Name und ID (bei TeamViewer auch DNS-Name oder IP-Adresse). Mit dem Gerätenamen reicht im Fenster der Name. Ein Gerät kann ein eigenes Passwort haben.
- Passwörter liegen im Windows-Tresor, nicht in der Einstellungsdatei des Addons.
- TeamViewer und RustDesk bekommen das Passwort beim Start als Argument. Für einen Moment ist es dadurch für andere Programme auf diesem Rechner in der Prozessliste sichtbar. AnyDesk bekommt es über die Standardeingabe.

## Eigene Addons schreiben

Ein Addon ist eine .NET-Bibliothek `WorkspaceManager.Addon.<Name>.dll` in `Addons\<Name>\`, die die Schnittstelle `IAddon` aus `src/WorkspaceManager/Addons.cs` umsetzt:

- `Id`, `Name`, `Description`, `Version`
- `Tabs`: die Einstellungsseiten des Addons (geöffnet über Einstellungen → Addons)
- `Hotkeys`: globale Tastenkürzel
- `Start(IAddonHost)` und `Stop()`

Das Addon `addons/Remote` ist ein vollständiges Beispiel. Mitgelieferte Addons dürfen die Oberflächen-Bausteine des Programms nutzen (`Ui`, `ThemeDropdown`, `EntryDialog` und andere). Dafür ist die Bibliothek in `Addons.cs` per `InternalsVisibleTo` freigegeben.

Ein fehlerhaftes Addon wird mit seiner Fehlermeldung unter Einstellungen → Addons aufgelistet und kann das Programm nicht zum Absturz bringen.

## Sicherheit

Ein Addon läuft mit denselben Rechten wie WorkspaceManager und kann alles, was das Programm kann. Installiere nur Addons aus Quellen, denen du vertraust. Der Ordner `Addons` liegt unter `Program Files`, normale Benutzer können dort nichts hineinkopieren.
