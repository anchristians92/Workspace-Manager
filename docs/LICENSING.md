# Lizenzschlüssel erzeugen

Gilt nur für den Herausgeber. Lizenzschlüssel werden mit einem privaten Schlüssel signiert, den nur du besitzt. Das Programm enthält nur den öffentlichen Schlüssel und kann damit prüfen, ob ein Schlüssel echt ist. Die Prüfung braucht kein Internet.

## Einmalig: Schlüsselpaar

Das Schlüsselpaar ist bereits erzeugt. Der öffentliche Teil steht in `src/WorkspaceManager/LicenseToken.cs`, der private liegt **außerhalb des Repositorys** unter

```
%USERPROFILE%\Documents\WorkspaceManager-Lizenzen\license-private.key
```

**Sichere diese Datei** (zum Beispiel auf einem USB-Stick oder in einem Passwortmanager), gib sie nie weiter und lade sie nie hoch. Geht sie verloren, kannst du keine neuen Schlüssel mehr erzeugen, die zu bereits ausgelieferten Programmen passen. Gelangt sie in fremde Hände, kann jeder gültige Schlüssel erzeugen. Dann müsste ein neues Paar erzeugt und eine neue Programmversion veröffentlicht werden.

Ein neues Paar erzeugen (nur wenn nötig, überschreibt nichts):

```powershell
dotnet run --project tools\LicenseTool -c Release -- keygen --out "D:\Lizenzen"
```

Den ausgegebenen öffentlichen Schlüssel dann in `LicenseToken.PublicKey` eintragen und neu bauen.

## Schlüssel ausstellen mit dem Lizenzgenerator (empfohlen)

`LizenzGenerator.exe` liegt neben deinem privaten Schlüssel in `Dokumente\WorkspaceManager-Lizenzen`. Doppelklick, Firma, Notiz und Anzahl der Geräte eintragen, Gültigkeit wählen, **Erstellen** klicken. Der Schlüssel erscheint im Fenster und liegt sofort in der Zwischenablage.

Jede ausgestellte Lizenz wird in `issued-licenses.csv` neben dem privaten Schlüssel festgehalten: wann (Datum und Uhrzeit), von wem (Windows-Benutzer), für wen (Firma), Notiz, Anzahl Geräte, Gültigkeit und der Schlüssel selbst. Die Datei ist ein einfaches Textprotokoll (Semikolon-getrennt, öffnet sich in Excel). Im Generator steht dieselbe Liste unten, mit Status *gültig*, *abgelaufen* oder *unbefristet*. Ein Doppelklick auf eine Zeile kopiert den Schlüssel erneut, **Protokoll öffnen** zeigt die Datei im Explorer. Sichere sie zusammen mit dem privaten Schlüssel.

Der Generator prüft, dass der gewählte private Schlüssel zum öffentlichen Schlüssel im Programm passt. Mit einem falschen Schlüssel erzeugt er keine Lizenz.

Neu bauen (zum Beispiel nach einer Programmänderung):

```powershell
.\tools\Build-LicenseGenerator.ps1
```

Die EXE ist nur für dich gedacht. Nicht weitergeben und nicht hochladen.

## Schlüssel ausstellen auf der Kommandozeile

```powershell
# unbefristet, 10 Geräte
dotnet run --project tools\LicenseTool -c Release -- issue --key "$env:USERPROFILE\Documents\WorkspaceManager-Lizenzen\license-private.key" --licensee "Beispiel GmbH" --seats 10

# ein Jahr gültig
dotnet run --project tools\LicenseTool -c Release -- issue --key "$env:USERPROFILE\Documents\WorkspaceManager-Lizenzen\license-private.key" --licensee "Beispiel GmbH" --seats 10 --days 365
```

Der Schlüssel (beginnt mit `WSM1.`) wird ausgegeben und zusätzlich in `issued-licenses.csv` neben dem privaten Schlüssel festgehalten. Dort siehst du später, wer welche Lizenz hat.

## Schlüssel prüfen

```powershell
dotnet run --project tools\LicenseTool -c Release -- verify "WSM1...."
```

## Grenzen

- Die **Gerätezahl** steht im Schlüssel, wird aber nicht technisch erzwungen. Sie ist Teil der Absprache.
- Weil der Quellcode einsehbar ist, kann ein technisch versierter Nutzer die Prüfung entfernen. Wirksam ist deshalb vor allem die Lizenz selbst, der Schlüssel dient als Nachweis.
- Die Nutzungsart („Privat“ oder „Kommerziell“) wählt die Person selbst, sie beruht auf Ehrlichkeit.
