using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace WorkspaceManager;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        AppTheme.Refresh();
        if (args.Contains("--ui-preview")) return SelfTests.RenderUi(args);
        if (args.Contains("--self-test")) return SelfTests.Run(args);
        if (args.Contains("--diagnose")) return Diagnose.Run(args);
        using var mutex = new Mutex(true, @"Local\PersonalWorkspaceManager.Tray", out var first);
        if (!first) { MessageBox.Show("Workspace Manager läuft bereits im Infobereich neben der Uhr."); return 0; }
        try
        {
            using var app = new TrayContext(args.Contains("--settings"), args.Contains("--autostart"));
            if (AutoStart.Enabled) AutoStart.Set(true); // keeps path and --autostart flag current (not done by the self-test)
            Application.Run(app); return 0;
        }
        catch (Exception e) { MessageBox.Show(e.Message, "Workspace Manager konnte nicht starten", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }
}

internal sealed class TrayContext : ApplicationContext
{
    readonly NotifyIcon tray;
    readonly HotkeyWindow hotkeys = new();
    readonly AddonManager addons;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    readonly HashSet<string> launching = new(StringComparer.OrdinalIgnoreCase);
    Settings settings;
    SettingsForm? form;
    string? candidate, applied;
    int stableTicks;
    bool disposed;
    // Place and monitors the last time they were stable, and whether programs are being opened after a change right now.
    string? settledSignature;
    bool openingApps;
    public string Status { get; private set; } = "Standort wird ermittelt …";
    public string? Location { get; private set; }
    public TrayContext(bool openSettings = false, bool autostart = false)
    {
        settings = Storage.ReadSettings(); AppTheme.Mode = settings.ThemeMode; AppTheme.Refresh();
        tray = new NotifyIcon { Icon = Branding.Tray(), Text = "Workspace Manager", Visible = true };
        tray.DoubleClick += (_, _) => ShowSettings();
        addons = new AddonManager(Path.Combine(AppContext.BaseDirectory, "Addons"), Storage.Root, () => RegisterKeys(), Notify);
        addons.Apply(settings);
        BuildMenu(); RegisterKeys();
        timer.Tick += (_, _) => Guard(Poll); timer.Start();
        if (openSettings) ShowSettings();
        var license = LicenseService.State;
        if (license.Kind is LicenseKind.Trial or LicenseKind.Blocked) Notify(license.Headline + ". " + license.Detail);
        if (autostart) _ = RunStartup();
    }
    static readonly HashSet<string> NotLaunchable = new(["explorer.exe", "ApplicationFrameHost.exe", "SystemSettings.exe", "mstsc.exe", "cmd.exe", "conhost.exe", "TextInputHost.exe"], StringComparer.OrdinalIgnoreCase);
    // After sign-in: wait until the location is known, then open the apps configured for it.
    async Task RunStartup()
    {
        try
        {
            if (!LicenseService.State.Allowed) return;
            for (var i = 0; i < 60 && !disposed && !(Location is not null && stableTicks >= 3); i++) await Task.Delay(1500);
            if (disposed) return;
            var location = Location;
            if (location is null) { Notify("Autostart: Standort nicht erkannt, es wurden keine Programme geöffnet."); return; }
            await OpenMissingApps(location, settings.AutoOpenLayoutApps);
        }
        catch (Exception e) { if (!disposed) Fail(e); }
    }
    // Opens the programs of a location that are not running yet: the ones marked for autostart there and, with <layoutApps>,
    // the ones of the saved layout. Each one is placed on its saved position by Launch.
    async Task OpenMissingApps(string location, bool layoutApps)
    {
        var targets = settings.Apps.Where(a => a.AutoStartLocations.Contains(location, StringComparer.OrdinalIgnoreCase)).ToList();
        if (layoutApps && Storage.ReadProfile(location) is { } profile)
            foreach (var exe in profile.Windows.Select(w => w.Executable).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // System hosts cannot be opened meaningfully on their own (File Explorer, UWP frame, RDP client, consoles ...).
                if (targets.Any(a => string.Equals(a.Executable, exe, StringComparison.OrdinalIgnoreCase)) || NotLaunchable.Contains(Path.GetFileName(exe)) || !Path.IsPathFullyQualified(exe) || !File.Exists(exe)) continue;
                targets.Add(settings.Apps.FirstOrDefault(a => string.Equals(a.Executable, exe, StringComparison.OrdinalIgnoreCase))
                    ?? new AppEntry { Name = Path.GetFileNameWithoutExtension(exe), Executable = exe, RestorePosition = true });
            }
        foreach (var app in targets)
        {
            if (disposed) return;
            var running = app.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && WindowLayouts.Capture().Any(w => string.Equals(w.Snapshot.Executable, app.Executable, StringComparison.OrdinalIgnoreCase));
            if (running) continue;
            _ = Launch(app); await Task.Delay(2500);
        }
    }
    // A change is a stable state that differs from the previous stable one. The very first state is none.
    internal static bool IsChange(string? previous, string current) => previous is not null && previous != current;
    // After a change of place or monitors: windows that are open move to their saved position, programs that are missing are opened.
    async Task RestoreAndOpen(string location)
    {
        if (openingApps) return;
        openingApps = true;
        try
        {
            Notify(WindowLayouts.Restore(location));
            await OpenMissingApps(location, true);
        }
        catch (Exception e) { if (!disposed) Fail(e); }
        finally { openingApps = false; }
    }
    void BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => AppTheme.ApplyMenu(menu);
        menu.Items.Add("Workspace Manager – Einstellungen", null, (_, _) => ShowSettings());
        menu.Items.Add("Konfiguration neu laden", null, (_, _) => Guard(() =>
        {
            var next = Storage.ReadSettings(); settings = next; AppTheme.Mode = next.ThemeMode; AppTheme.Refresh(); applied = null; candidate = null;
            form?.Close(); addons.Apply(next); BuildMenu(); RegisterKeys(); Notify("Konfiguration neu geladen.");
        }));
        menu.Items.Add(new ToolStripSeparator());
        foreach (var location in settings.Locations)
        {
            var name = location.Name;
            menu.Items.Add($"Aktuelles Layout als {name} speichern", null, (_, _) => Guard(() =>
            {
                var profile = WindowLayouts.Save(name); Notify($"{name}: {profile.Windows.Count} Fenster gespeichert.");
            }));
        }
        menu.Items.Add("Layout wiederherstellen", null, (_, _) => Guard(() => Notify(WindowLayouts.Restore(CurrentLocation()))));
        var automatic = new ToolStripMenuItem("Automatisch wiederherstellen") { Checked = settings.AutoRestore, CheckOnClick = true };
        automatic.Click += (_, _) => Guard(() => { settings.AutoRestore = automatic.Checked; Storage.Save(Storage.SettingsPath, settings); applied = null; });
        menu.Items.Add(automatic);
        var startup = new ToolStripMenuItem("Mit Windows starten") { Checked = AutoStart.Enabled, CheckOnClick = true };
        startup.Click += (_, _) => Guard(() => { AutoStart.Set(startup.Checked); startup.Checked = AutoStart.Enabled; });
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => ExitThread());
        var old = tray.ContextMenuStrip; tray.ContextMenuStrip = menu; old?.Dispose();
    }
    string CurrentLocation() => NetworkDiscovery.Detect(settings.Locations, NetworkDiscovery.Capture()) ?? throw new InvalidOperationException("Standort unbekannt oder mehrdeutig. Kein Layout angewendet.");
    void Poll()
    {
        Location = NetworkDiscovery.Detect(settings.Locations, NetworkDiscovery.Capture());
        var monitors = WindowLayouts.Monitors();
        var signature = Location + JsonSerializer.Serialize(monitors, Storage.Json);
        Status = $"Standort: {Location ?? "Unbekannt / mehrdeutig"} · {monitors.Count} Monitor(e)";
        tray.Text = ("Workspace Manager · " + (Location ?? "Unbekannt"))[..Math.Min(63, ("Workspace Manager · " + (Location ?? "Unbekannt")).Length)];
        if (signature != candidate) { candidate = signature; stableTicks = 0; applied = null; }
        else stableTicks++;
        if (!settings.AutoRestore || !LicenseService.State.Allowed || stableTicks < 3 || applied == signature) return;
        applied = signature;
        // The first state after the program started is not a change: after Windows starts the autostart opens the programs,
        // and a program the user started by hand is not expected to open anything. Every later change (new place, dock) does.
        // Saving settings resets the detection, but the place and the monitors stay the same, so that is no change either.
        var changed = IsChange(settledSignature, signature); settledSignature = signature;
        if (Location is null) return;
        var profile = Storage.ReadProfile(Location);
        if (profile is null) { Notify($"Für {Location} ist noch kein Layout gespeichert."); return; }
        if (!WindowLayouts.Compatible(profile.Monitors, monitors)) { Notify("Automatik wartet: Die Monitorlandschaft passt nicht zum gespeicherten Profil."); return; }
        if (changed) _ = RestoreAndOpen(Location);
        else Notify(WindowLayouts.Restore(Location));
    }
    void RegisterKeys()
    {
        // Without a valid license (expired trial) no hotkeys are registered.
        var active = LicenseService.State.Allowed ? settings : new Settings { Apps = [], Logins = [] };
        var failures = hotkeys.Configure(active, app => _ = Launch(app), login => _ = Login(login), LicenseService.State.Allowed ? addons.Hotkeys() : null);
        if (failures.Count > 0) Notify(string.Join(Environment.NewLine, failures));
    }
    async Task Launch(AppEntry app)
    {
        if (!launching.Add(app.Executable)) return;
        try
        {
            var location = NetworkDiscovery.Detect(settings.Locations, NetworkDiscovery.Capture());
            // Scripts, .rdp files and shortcuts open their window in another process, so those windows are found by being new instead of by EXE path.
            var isExe = app.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
            var before = WindowLayouts.Capture().Select(w => w.Handle).ToHashSet();
            using var process = AppLauncher.Start(app, settings.Logins);
            // Windows does not let a normal program move the windows of a program running as administrator.
            if (!app.RestorePosition || location is null || app.RunAs == "Admin") return;
            var profile = Storage.ReadProfile(location);
            if (profile is null) { Notify("Programm gestartet. Für diesen Standort fehlt noch ein Layout."); return; }
            for (var attempt = 0; attempt < 40 && !disposed; attempt++)
            {
                await Task.Delay(500);
                if (disposed) return;
                if (CurrentLocation() != location || !WindowLayouts.Compatible(profile.Monitors, WindowLayouts.Monitors())) throw new InvalidOperationException("Programm gestartet; Position wegen geänderter Umgebung nicht angewendet.");
                var live = WindowLayouts.Capture().Where(w => !isExe || string.Equals(w.Snapshot.Executable, app.Executable, StringComparison.OrdinalIgnoreCase)).ToList();
                var fresh = live.Where(w => !before.Contains(w.Handle)).ToList();
                if (fresh.Count == 0 && (attempt < 5 || !isExe)) continue;
                var candidates = fresh.Count > 0 ? fresh : live;
                var candidateExecutables = candidates.Select(c => c.Snapshot.Executable).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var saved = profile.Windows.Where(w => isExe ? string.Equals(w.Executable, app.Executable, StringComparison.OrdinalIgnoreCase) : candidateExecutables.Contains(w.Executable)).ToList();
                var matches = WindowLayouts.Match(saved, candidates);
                if (matches.Count == 0) continue;
                foreach (var pair in matches) WindowLayouts.Apply(pair.Live.Handle, pair.Saved);
                return;
            }
            if (!disposed) Notify("Programm gestartet; kein eindeutig zuordenbares Fenster gefunden.");
        }
        catch (Exception e) { if (!disposed) Fail(e); }
        finally { launching.Remove(app.Executable); }
    }
    async Task Login(LoginEntry entry) { try { await LoginSender.Send(entry); } catch (Exception e) { if (!disposed) Fail(e); } }
    void ShowSettings()
    {
        if (form is { IsDisposed: false }) { form.Activate(); return; }
        form = new SettingsForm(settings, () => Status, SaveSettings);
        form.Show();
    }
    void SaveSettings(Settings next)
    {
        Storage.Save(Storage.SettingsPath, next); settings = next; applied = null; candidate = null;
        AppTheme.Mode = next.ThemeMode; AppTheme.Refresh();
        addons.Apply(next);
        BuildMenu(); RegisterKeys();
    }
    void Guard(Action action) { try { action(); } catch (Exception e) { Fail(e); } }
    // Shows an error and writes it to error.log (with the stack) so the cause can be found later. Never throws itself.
    string? lastFail; DateTime lastFailAt;
    void Fail(Exception e)
    {
        // The same error again (for example from the timer every two seconds) is shown and logged once a minute only.
        var key = e.GetType().Name + e.Message;
        if (key == lastFail && DateTime.UtcNow - lastFailAt < TimeSpan.FromMinutes(1)) return;
        lastFail = key; lastFailAt = DateTime.UtcNow;
        ErrorLog.Write(e);
        Notify(string.IsNullOrWhiteSpace(e.Message) ? $"Unerwarteter Fehler ({e.GetType().Name}). Details stehen in error.log im Datenordner." : e.Message);
    }
    void Notify(string text)
    {
        // A balloon with an empty text throws, and a notification must never take the program down.
        try { tray.ShowBalloonTip(6000, "Workspace Manager", string.IsNullOrWhiteSpace(text) ? "(keine Meldung)" : text, ToolTipIcon.Info); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or ObjectDisposedException) { }
    }
    protected override void Dispose(bool disposing)
    {
        disposed = true;
        if (disposing) { timer.Stop(); timer.Dispose(); addons.Dispose(); hotkeys.Dispose(); tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); form?.Dispose(); }
        base.Dispose(disposing);
    }
}

internal static class AutoStart
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "PersonalWorkspaceManager";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue(Name) is string; } }
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue(Name, "\"" + Environment.ProcessPath + "\" --autostart"); else key.DeleteValue(Name, false);
    }
}
