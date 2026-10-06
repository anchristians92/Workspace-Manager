namespace WorkspaceManager;

// "WorkspaceManager.exe --diagnose <file>": writes what the program sees right now and why a saved layout would or would not be
// applied (place, monitors, which windows match). Window titles are left out, only program names are written.
internal static class Diagnose
{
    public static int Run(string[] args)
    {
        var target = args[Array.IndexOf(args, "--diagnose") + 1];
        var lines = new List<string>();
        void Add(string text = "") => lines.Add(text);
        try
        {
            var settings = Storage.ReadSettings();
            Add($"Zeit: {DateTime.Now:yyyy-MM-dd HH:mm:ss}   Automatik: {settings.AutoRestore}   Programme des Layouts öffnen: {settings.AutoOpenLayoutApps}");
            var adapters = NetworkDiscovery.Capture();
            foreach (var a in adapters.Where(a => a.Physical)) Add($"Netzwerk: {a.Name}  aktiv={a.Active}  {string.Join(",", a.Addresses)}  Gateway {string.Join(",", a.Gateways)}");
            var location = NetworkDiscovery.Detect(settings.Locations, adapters);
            Add($"Erkannter Standort: {location ?? "(keiner / mehrdeutig)"}");
            var monitors = WindowLayouts.Monitors();
            Add(); Add("Monitore, wie das Programm sie sieht:");
            foreach (var m in monitors) Add($"  {m.Device}  {Describe(m)}");
            if (location is null) { File.WriteAllLines(target, lines); return 0; }

            var profile = Storage.ReadProfile(location);
            if (profile is null) { Add($"Für {location} gibt es kein gespeichertes Layout."); File.WriteAllLines(target, lines); return 0; }
            Add(); Add($"Gespeichertes Layout für {location} ({profile.Windows.Count} Fenster) wurde mit diesen Monitoren gespeichert:");
            foreach (var m in profile.Monitors) Add($"  {m.Device}  {Describe(m)}");

            var compatible = WindowLayouts.Compatible(profile.Monitors, monitors);
            Add(); Add($"Passen die Monitore zum Layout? {(compatible ? "JA" : "NEIN")}");
            if (!compatible)
            {
                // The monitors are paired from left to right, the names (\\.\DISPLAYn) are only numbers that Windows hands out anew.
                var before = WindowLayouts.ByPosition(profile.Monitors); var after = WindowLayouts.ByPosition(monitors);
                if (before.Length != after.Length) Add($"  - Anzahl der Monitore: jetzt {after.Length}, gespeichert {before.Length}");
                for (var i = 0; i < Math.Min(before.Length, after.Length); i++)
                {
                    var saved = before[i]; var now = after[i]; var name = $"Monitor {i + 1} von links";
                    if (now.Identity != saved.Identity) Add($"  - {name}: andere Monitor-Kennung (jetzt {now.Identity}, gespeichert {saved.Identity})");
                    if (now.Bounds != saved.Bounds) Add($"  - {name}: Größe/Position {Show(now.Bounds)} statt {Show(saved.Bounds)}");
                    if (now.WorkArea != saved.WorkArea) Add($"  - {name}: Arbeitsbereich (ohne Taskleiste) {Show(now.WorkArea)} statt {Show(saved.WorkArea)}");
                    if (now.Primary != saved.Primary) Add($"  - {name}: Hauptmonitor jetzt {now.Primary}, gespeichert {saved.Primary}");
                    if (now.Dpi != saved.Dpi) Add($"  - {name}: Skalierung {now.Dpi * 100 / 96}% statt {saved.Dpi * 100 / 96}%");
                }
            }

            var live = WindowLayouts.Capture();
            var matches = WindowLayouts.Match(profile.Windows, live);
            Add(); Add($"Gespeicherte Fenster: {profile.Windows.Count}   davon jetzt eindeutig zuzuordnen: {matches.Count}");
            foreach (var w in profile.Windows)
            {
                var program = Path.GetFileName(w.Executable);
                var same = live.Count(l => string.Equals(l.Snapshot.Executable, w.Executable, StringComparison.OrdinalIgnoreCase));
                var found = matches.Any(m => ReferenceEquals(m.Saved, w));
                Add($"  {program}: {(found ? "wird zugeordnet" : same == 0 ? "läuft gerade nicht" : same > 1 ? $"{same} Fenster dieses Programms, nicht eindeutig" : "Fenster gefunden, aber Fensterklasse passt nicht")}");
                if (!found && same > 0)
                {
                    Add($"      gespeicherte Klasse: {w.ClassName}");
                    foreach (var l in live.Where(l => string.Equals(l.Snapshot.Executable, w.Executable, StringComparison.OrdinalIgnoreCase))) Add($"      jetzige Klasse:      {l.Snapshot.ClassName}");
                }
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException) { Add("Fehler bei der Diagnose: " + e.GetType().Name + ": " + e.Message); }
        File.WriteAllLines(target, lines);
        return 0;
    }

    static string Show(Rect r) => $"{r.X},{r.Y} {r.Width}x{r.Height}";
    static string Describe(MonitorSnapshot m) => $"{Show(m.Bounds)}  Arbeitsbereich {Show(m.WorkArea)}  Skalierung {m.Dpi * 100 / 96}%{(m.Primary ? "  Hauptmonitor" : "")}";
}
