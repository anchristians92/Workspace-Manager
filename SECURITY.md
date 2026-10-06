# Sicherheit

## Eine Schwachstelle melden

Bitte melde Sicherheitslücken **nicht** als öffentliches Issue. Nutze stattdessen die private Meldung auf GitHub („Security“ → „Report a vulnerability“) im Repository. Beschreibe, was du gefunden hast, wie man es nachstellt und welche Version betroffen ist.

## Was WorkspaceManager schützt und was nicht

**Passwörter** liegen im Windows-Anmeldeinformationsmanager, verschlüsselt und an das Windows-Konto gebunden. Sie stehen nie in den Einstellungsdateien des Programms.

**Netzwerk:** Das Programm baut keine Internetverbindung auf, sendet keine Daten und enthält keine Telemetrie. Die Lizenzprüfung läuft offline.

**Addons:** Ein Addon läuft mit denselben Rechten wie das Programm. Installiere nur Addons aus vertrauenswürdigen Quellen. Der Ordner `Addons` liegt unter `Program Files`, normale Benutzer können dort nichts ablegen. Ein fehlerhaftes Addon wird abgefangen und aufgelistet.

**Fernwartung:** Passwörter für TeamViewer, AnyDesk und RustDesk liegen im Windows-Tresor. TeamViewer und RustDesk bekommen das Passwort beim Start als Argument, es ist dadurch kurz in der Prozessliste sichtbar. AnyDesk bekommt es über die Standardeingabe.

**Rechte:** Es läuft mit normalen Benutzerrechten und braucht keine Administratorrechte. Nur der Installer läuft erhöht und schreibt nach `Program Files`.

**Logins eintippen:** Ein Login wird nur eingegeben, wenn das aktive Fenster beim Tastendruck und während der ganzen Eingabe dasselbe bleibt. Ist beim Login ein Programm festgelegt, muss es dazu passen. Es wird nie ein Enter gesendet. Ohne festgelegtes Programm wird in jedes aktive Fenster getippt.

**Nicht geschützt:**

- Ein anderes Programm, das unter demselben Windows-Benutzer läuft, kann den Windows-Tresor und die Einstellungsdateien lesen und ändern. Das gilt für alles, was ein Benutzerkonto speichert.
- Gespeicherte Layouts enthalten die **Fenstertitel** der erfassten Fenster (zum Beispiel Dokumentnamen) im Klartext unter `%LOCALAPPDATA%\PersonalWorkspaceManager`.
- Die eingetippten Zeichen eines Logins laufen als Tastatureingabe durch Windows. Ein Keylogger auf dem Rechner könnte sie mitlesen.
- Die Lizenzprüfung ist ein Nachweis und kein Kopierschutz. Der Quellcode ist einsehbar, die Prüfung kann entfernt werden.

## Hinweise für Releases

- Die MSI enthält die .NET-Laufzeit. Sie bekommt Sicherheitsupdates von Microsoft nicht automatisch über Windows Update, sondern nur mit einer neuen WorkspaceManager-Version. Bei jedem .NET-Sicherheitsupdate sollte deshalb neu gebaut und veröffentlicht werden.
- Die Dateien sind nicht mit einem Codesignatur-Zertifikat signiert. Windows SmartScreen kann beim ersten Start warnen. Zu jedem Release gehört die SHA-256-Prüfsumme der MSI, damit Nutzer den Download prüfen können.
- Der private Lizenzschlüssel gehört nie ins Repository (siehe [docs/LICENSING.md](docs/LICENSING.md)).
