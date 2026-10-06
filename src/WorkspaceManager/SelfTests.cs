using System.Runtime.InteropServices;
using System.Text.Json;

namespace WorkspaceManager;

internal static class SelfTests
{
    static List<LocationRule> SampleLocations() => [new() { Name = "Zuhause", Subnet = "192.168.1.0/24" }, new() { Name = "Arbeit", Subnet = "10.0.0.0/24" }];
    public static int RenderUi(string[] args)
    {
        var folder = args[Array.IndexOf(args, "--ui-preview") + 1];
        Directory.CreateDirectory(folder);
        Storage.Root = Path.Combine(Path.GetTempPath(), "WorkspaceManager-Preview-" + Guid.NewGuid().ToString("N"));
        var report = new List<string>();
        report.Add($"Theme detection: {AppTheme.Detection}; Dark={AppTheme.Dark}; HighContrast={SystemInformation.HighContrast}");
        // Addons next to the program (if any) are part of the preview.
        var previewSettings = new Settings { Locations = SampleLocations() };
        using var previewAddons = new AddonManager(Path.Combine(AppContext.BaseDirectory, "Addons"), Storage.Root, () => { }, _ => { });
        previewAddons.Apply(previewSettings);
        using var form = new SettingsForm(previewSettings, () => "Standort: Zuhause · 3 Monitore", _ => { });
        form.Show(); form.Hide(); form.Show(); Application.DoEvents();
        void Capture(Form window, string file)
        {
            Application.DoEvents();
            using var bitmap = new Bitmap(window.Width, window.Height);
            window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, window.Size));
            bitmap.Save(Path.Combine(folder, file + ".png"));
            report.Add($"{file}: {window.Width}x{window.Height}, DPI={window.DeviceDpi}");
            // The scroll area must not reach further than its content, otherwise the page scrolls into empty space.
            if (Descendants(window).OfType<ScrollPanel>().FirstOrDefault() is { } scroll && scroll.Controls.Count > 0)
            {
                var contentHeight = scroll.Controls.Cast<Control>().Sum(c => c.Height + c.Margin.Vertical) + scroll.Padding.Vertical;
                var extent = scroll.DisplayRectangle.Height;
                var offset = -scroll.DisplayRectangle.Top;
                var lowest = Descendants(scroll).Where(c => c.Visible && c.Height > 0 && c.Controls.Count == 0).Select(c => (Control: c, Bottom: scroll.RectangleToClient(c.RectangleToScreen(c.ClientRectangle)).Bottom + offset)).OrderByDescending(x => x.Bottom).First();
                report.Add($"  scroll: client={scroll.ClientSize.Height}, extent={extent}, content={contentHeight}, vmax={scroll.VerticalScroll.Maximum}, lowest visible control ends at {lowest.Bottom} ({lowest.Control.GetType().Name} '{(lowest.Control.Text.Length > 30 ? lowest.Control.Text[..30] : lowest.Control.Text)}')");
                // The furthest scroll position must show the end of the content at the bottom edge, not empty space below it.
                var vertical = scroll.VerticalScroll;
                var farthest = vertical.Maximum - vertical.LargeChange + 1;
                if (vertical.Visible && farthest > Math.Max(0, contentHeight - scroll.ClientSize.Height) + 40)
                    throw new InvalidOperationException($"{file} can be scrolled {farthest - Math.Max(0, contentHeight - scroll.ClientSize.Height)}px past its end (LargeChange={vertical.LargeChange}, client={scroll.ClientSize.Height}).");
                if (extent > Math.Max(scroll.ClientSize.Height, contentHeight) + 40)
                    throw new InvalidOperationException($"Scroll area of {file} is {extent - contentHeight}px taller than its content.");
            }
            foreach (var label in Descendants(window).OfType<Label>().Where(l => l.Visible && l.AutoSize))
                if (label.GetPreferredSize(new Size(label.Width, 0)).Height > label.Height + 2)
                    throw new InvalidOperationException($"Clipped label in {file}: {label.Text}");
        }
        Capture(form, "overview");
        if (args.Contains("--system-theme-only"))
        {
            var until = DateTime.UtcNow.AddMilliseconds(1500);
            while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); }
            Capture(form, "system-theme");
            report.Add($"After startup: {AppTheme.Detection}; Dark={AppTheme.Dark}; BackColor={form.BackColor}");
            File.WriteAllLines(Path.Combine(folder, "ui-check.txt"), report); form.Close(); return 0;
        }
        foreach (var dark in new[] { true, false })
        {
            AppTheme.PreviewDark = dark;
            var until = DateTime.UtcNow.AddMilliseconds(1300);
            while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); }
            if (form.BackColor != AppTheme.Background || AppTheme.Dark != dark) throw new InvalidOperationException("Live theme change failed.");
            Capture(form, dark ? "overview-dark" : "overview-light");
        }
        AppTheme.PreviewDark = null; AppTheme.Refresh(); AppTheme.Apply(form);
        form.Size = new Size(820, 660); Capture(form, "overview-compact");
        static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));
        foreach (var name in new[] { "Programme", "Logins", "Standorte", "Einstellungen", "Lizenz", "Erweitert", "Info" })
        {
            if (name is "Lizenz" or "Erweitert" or "Info") Descendants(form).OfType<Button>().Single(b => b.Text == "Einstellungen").PerformClick();
            Descendants(form).OfType<Button>().Single(b => b.Text == name).PerformClick();
            Capture(form, name.ToLowerInvariant());
        }
        if (previewAddons.Pages().Any())
        {
            Descendants(form).OfType<ThemeButton>().First(x => x.Text == "Fernwartung" && x.Parent is SmoothStack).PerformClick(); Capture(form, "addon-page");
            Descendants(form).OfType<Button>().Single(x => x.Text == "Einstellungen").PerformClick();
            Descendants(form).OfType<Button>().Single(x => x.Text == "Addons").PerformClick(); Capture(form, "addons");
            Descendants(form).OfType<Button>().First(x => x.Text == "Einstellungen öffnen").PerformClick(); Capture(form, "addon-tab");
            if (previewAddons.Pages().Any(p => p.Page.Title == "Passwort-Tresor"))
            {
                Descendants(form).OfType<ThemeButton>().First(x => x.Text == "Passwort-Tresor" && x.Parent is SmoothStack).PerformClick(); Capture(form, "vault-page");
                Descendants(form).OfType<Button>().Single(x => x.Text == "Einstellungen").PerformClick();
                Descendants(form).OfType<Button>().Single(x => x.Text == "Addons").PerformClick();
                Descendants(form).OfType<Button>().Where(x => x.Text == "Einstellungen öffnen").Last().PerformClick(); Capture(form, "vault-tab");
            }
        }
        using var login = new LoginForm(new Settings(), _ => { }); login.Show(); Capture(login, "login-dialog");
        login.ScrollControlIntoView(login.Controls[0]);
        var loginScroll = (Panel)login.Controls[0]; loginScroll.AutoScrollPosition = new Point(0, 10000); Capture(login, "login-dialog-bottom"); login.Close();
        using var entry = new EntryDialog("Programm hinzufügen", ["Name", "Programm", "Tastenkürzel", "Startargumente (optional)"], ["Browser", @"C:\Program Files\Browser\browser.exe", "Ctrl+Alt+B", ""], true, logins: [new LoginEntry { Name = "Admin-Konto" }], runAs: "User"); entry.Show(); Capture(entry, "app-dialog");
        foreach (var dark in new[] { true, false })
        {
            AppTheme.PreviewDark = dark;
            var until = DateTime.UtcNow.AddMilliseconds(1300);
            while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(10); }
            if (entry.Values[0] != "Browser" || entry.Values[2] != "Ctrl+Alt+B") throw new InvalidOperationException("Theme change altered input.");
            Capture(entry, dark ? "app-dialog-dark" : "app-dialog-light");
        }
        AppTheme.PreviewDark = null; AppTheme.Refresh(); entry.Close();
        File.WriteAllLines(Path.Combine(folder, "ui-check.txt"), report);
        form.Close(); return 0;
    }
    sealed class TestSkipped(string message) : Exception(message);
    public static int Run(string[] args)
    {
        var report = new List<string>(); var failed = 0;
        void Test(string name, Action action)
        {
            try { action(); report.Add("PASS " + name); }
            catch (TestSkipped e) { report.Add("SKIP " + name + ": " + e.Message); }
            catch (Exception e) { failed++; report.Add("FAIL " + name + ": " + e.GetType().Name + " – " + e.Message); }
        }
        void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
        void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } catch (JsonException) { return; } throw new Exception("Invalid input accepted"); }
        var temporary = Path.Combine(Path.GetTempPath(), "WorkspaceManager-Test-" + Guid.NewGuid().ToString("N"));
        Storage.Root = temporary;
        try
        {
            var rules = SampleLocations();
            AdapterSnapshot Home(bool physical = true, bool active = true) => new("home", "Ethernet", physical, active, ["192.168.1.22"], ["192.168.1.1"]);
            AdapterSnapshot Work() => new("work", "Ethernet", true, true, ["10.0.0.99"], ["10.0.0.1"]);
            Test("IPv4 CIDR boundaries and malformed inputs", () =>
            {
                Assert(NetworkDiscovery.Contains("192.168.1.0/24", "192.168.1.255"));
                Assert(!NetworkDiscovery.Contains("192.168.1.0/24", "192.168.2.0"));
                Assert(NetworkDiscovery.Contains("10.0.0.99/32", "10.0.0.99"));
                Assert(!NetworkDiscovery.Contains("10.0.0.99/32", "10.0.0.98"));
                Assert(NetworkDiscovery.Contains("0.0.0.0/0", "1.2.3.4"));
                Assert(!NetworkDiscovery.ValidSubnet("10.0.0.0/33")); Assert(!NetworkDiscovery.Contains("bad", "::1"));
            });
            Test("Physical home/work and VPN filtering", () => { Assert(NetworkDiscovery.Detect(rules, [Home()]) == "Zuhause"); Assert(NetworkDiscovery.Detect(rules, [Work(), Home(false)]) == "Arbeit"); Assert(NetworkDiscovery.Detect(rules, [Home(false)]) is null); Assert(NetworkDiscovery.Detect(rules, [Home(true, false)]) is null); });
            Test("Conflicting physical networks fail closed", () => Assert(NetworkDiscovery.Detect(rules, [Home(), Work()]) is null));
            Test("Gateway and adapter constraints", () =>
            {
                var strict = new LocationRule { Name = "Strict", Subnet = "192.168.1.0/24", Gateway = "192.168.1.1", AdapterId = "home" };
                Assert(NetworkDiscovery.Detect([strict], [Home()]) == "Strict"); strict.AdapterId = "other"; Assert(NetworkDiscovery.Detect([strict], [Home()]) is null);
                strict.AdapterId = null; strict.Gateway = "192.168.1.2"; Assert(NetworkDiscovery.Detect([strict], [Home()]) is null);
                Assert(NetworkDiscovery.Detect(rules, [Home() with { Gateways = [] }]) is null);
            });
            var monitor = new MonitorSnapshot("DISPLAY1", "identity", new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true, 96);
            Test("Monitor topology, identity and DPI guard", () =>
            {
                Assert(WindowLayouts.Compatible([monitor], [monitor]));
                Assert(!WindowLayouts.Compatible([monitor], [monitor with { Dpi = 120 }]));
                Assert(!WindowLayouts.Compatible([monitor], [monitor with { Identity = "other" }]));
                Assert(!WindowLayouts.Compatible([monitor], [monitor with { Bounds = new(-1920, 0, 1920, 1080) }]));
                Assert(!WindowLayouts.Compatible([monitor], [])); Assert(!WindowLayouts.Compatible([], []));
                // After re-docking Windows hands out new display numbers; the same screens must still count as the same.
                var left = monitor with { Device = "DISPLAY1", Bounds = new(-1920, 0, 1920, 1080), WorkArea = new(-1920, 0, 1920, 1032), Primary = false };
                var main = monitor with { Device = "DISPLAY10", Identity = "main", Bounds = new(0, 0, 3840, 2160), WorkArea = new(0, 0, 3840, 2088), Primary = true, Dpi = 144 };
                Assert(WindowLayouts.Compatible([left, main], [left, main with { Device = "DISPLAY14" }]));
                Assert(WindowLayouts.Compatible([main, left], [left with { Device = "DISPLAY2" }, main with { Device = "DISPLAY14" }]));
                Assert(!WindowLayouts.Compatible([left, main], [left, main with { Device = "DISPLAY14", Bounds = new(0, 0, 2560, 1440) }]));
                Assert(!WindowLayouts.Compatible([left, main], [left, main with { Device = "DISPLAY14", Identity = "other" }]));
            });
            Test("Hotkey parser aliases and digits", () => { Assert(Hotkey.Parse("Strg+Alt+1") == Hotkey.Parse("Ctrl+Alt+D1")); Reject(() => Hotkey.Parse("Ctrl+Ctrl+X")); Reject(() => Hotkey.Parse("I")); });
            Test("Configuration validation and secret fields rejected", () =>
            {
                var s = new Settings(); s.Apps.Add(new() { Name = "Test", Executable = @"C:\Windows\notepad.exe", Hotkey = "Ctrl+Alt+1" });
                Assert(Storage.ParseSettings(JsonSerializer.Serialize(s)).Apps.Count == 1);
                s.Apps.Add(new() { Name = "Other", Executable = @"C:\Windows\other.exe", Hotkey = "Strg+Alt+1" });
                Reject(() => Storage.ParseSettings(JsonSerializer.Serialize(s))); Reject(() => Storage.ParseSettings("{\"Password\":\"must never be stored\"}"));
                Reject(() => Storage.ParseSettings("{\"Locations\":null}"));
            });
            Test("Programs open after a change of place, not at the first state", () =>
            {
                Assert(!TrayContext.IsChange(null, "Arbeit|1 Monitor"));
                Assert(!TrayContext.IsChange("Arbeit|1 Monitor", "Arbeit|1 Monitor"));
                Assert(TrayContext.IsChange("Arbeit|1 Monitor", "Zuhause|3 Monitore"));
                Assert(TrayContext.IsChange("Zuhause|1 Monitor", "Zuhause|3 Monitore"));
                Assert(TrayContext.IsChange("|1 Monitor", "Zuhause|1 Monitor"));
            });
            Test("Start as administrator or as another user", () =>
            {
                var login = new LoginEntry { Name = "Admin-Konto" };
                var app = new AppEntry { Name = "PowerShell", Executable = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", Arguments = "-NoLogo", RunAs = "Admin" };
                var admin = AppLauncher.Create(app, [login]);
                Assert(admin.Verb == "runas" && admin.UseShellExecute && admin.FileName == app.Executable && admin.Arguments == "-NoLogo");
                Assert(AppLauncher.Create(new AppEntry { Name = "N", Executable = app.Executable }, []).Verb == "");
                AppEntry Other(string executable = @"C:\Tools\a b\run.bat", string loginId = "") => new() { Name = "Skript", Executable = executable, Arguments = "eins", RunAs = "User", RunAsLoginId = loginId.Length > 0 ? loginId : login.Id };
                var other = Other();
                var start = AppLauncher.Create(other, [login], _ => ("FIRMA\\adm-test", "geheim".ToCharArray()));
                Assert(!start.UseShellExecute && start.UserName == "adm-test" && start.Domain == "FIRMA" && start.Password is not null && start.LoadUserProfile);
                Assert(start.FileName == "cmd.exe" && start.Arguments == "/c \"\"C:\\Tools\\a b\\run.bat\" eins\"");
                Assert(AppLauncher.SplitUser("adm@firma.example") == ("adm@firma.example", null) && AppLauncher.SplitUser("lokal").Domain == Environment.UserDomainName);
                Assert(AppLauncher.Target(@"C:\x\a.rdp", "").Equals(("mstsc.exe", "\"C:\\x\\a.rdp\"")));
                try { AppLauncher.Create(Other(loginId: "weg"), [login], _ => ("u", ['p'])); throw new Exception("missing login accepted"); } catch (InvalidOperationException) { }
                try { AppLauncher.Create(Other(@"C:\x\a.lnk"), [login], _ => ("u", ['p'])); throw new Exception("lnk accepted"); } catch (InvalidOperationException) { }
                try { AppLauncher.Create(other, [login], _ => ("", ['p'])); throw new Exception("empty user accepted"); } catch (InvalidOperationException) { }
                var s = new Settings(); s.Apps.Add(new() { Name = "T", Executable = @"C:\Windows\notepad.exe", Hotkey = "Ctrl+Alt+1", RunAs = "Admin" });
                Assert(Storage.ParseSettings(JsonSerializer.Serialize(s)).Apps[0].RunAs == "Admin");
                s.Apps[0].RunAs = "Root"; Reject(() => Storage.ParseSettings(JsonSerializer.Serialize(s)));
            });
            Test("License tokens: signature, expiry, trial and gating", () =>
            {
                using var signer = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
                using var stranger = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
                var pub = Convert.ToBase64String(signer.ExportSubjectPublicKeyInfo());
                var today = new DateOnly(2026, 10, 6);
                string Make(string? expires, string product = "WorkspaceManager", System.Security.Cryptography.ECDsa? key = null) =>
                    LicenseToken.Sign(new LicensePayload("a1", "Test GmbH", 5, "2026-10-01", expires, product), key ?? signer);
                var good = Make(null);
                Assert(LicenseToken.Verify(good, today, pub).Check == LicenseCheck.Valid);
                Assert(LicenseToken.Verify(Make("2026-10-06"), today, pub).Check == LicenseCheck.Valid);
                Assert(LicenseToken.Verify(Make("2026-10-05"), today, pub).Check == LicenseCheck.Expired);
                Assert(LicenseToken.Verify(Make(null, "Other"), today, pub).Check == LicenseCheck.WrongProduct);
                Assert(LicenseToken.Verify(Make(null, key: stranger), today, pub).Check == LicenseCheck.BadSignature);
                Assert(LicenseToken.Verify("garbage", today, pub).Check == LicenseCheck.Malformed);
                Assert(LicenseToken.Verify(null, today, pub).Check == LicenseCheck.Malformed);
                Assert(LicenseToken.Verify("  " + good.Insert(10, "\r\n") + " \n", today, pub).Check == LicenseCheck.Valid);
                using (var embedded = System.Security.Cryptography.ECDsa.Create()) embedded.ImportSubjectPublicKeyInfo(Convert.FromBase64String(LicenseToken.PublicKey), out _);
                Assert(LicenseService.Evaluate(new LicenseFile { Mode = "Private" }, today).Kind == LicenseKind.Private);
    // A domain-joined or Entra-joined device cannot be private, even if the file says so.
    Assert(LicenseService.Evaluate(new LicenseFile { Mode = "Private", TrialStart = "2026-10-06" }, today, pub, managedDevice: true).Kind == LicenseKind.Trial);
    Assert(LicenseService.Evaluate(new LicenseFile { Mode = "Private", TrialStart = "2026-09-01" }, today, pub, managedDevice: true).Kind == LicenseKind.Blocked);
                var trial = new LicenseFile { Mode = "Commercial", TrialStart = "2026-10-06" };
                Assert(LicenseService.Evaluate(trial, today, pub).TrialDaysLeft == 30);
                Assert(LicenseService.Evaluate(trial, today.AddDays(29), pub).TrialDaysLeft == 1);
                Assert(LicenseService.Evaluate(trial, today.AddDays(30), pub).Kind == LicenseKind.Blocked);
                Assert(!LicenseService.Evaluate(trial, today.AddDays(30), pub).Allowed);
                trial.Key = good;
                Assert(LicenseService.Evaluate(trial, today.AddDays(90), pub).Kind == LicenseKind.Licensed);
                trial.Key = Make("2026-10-05");
                Assert(LicenseService.Evaluate(trial, today.AddDays(40), pub).Kind == LicenseKind.Blocked);
            });
            Test("Renaming and deleting a location moves and removes its layout", () =>
            {
                var temporary = Path.Combine(Path.GetTempPath(), "WorkspaceManager-Test-" + Guid.NewGuid().ToString("N")); var previous = Storage.Root; Storage.Root = temporary;
                try
                {
                    Storage.Save(Storage.ProfilePath("Zuhause"), new LayoutProfile { Location = "Zuhause", Monitors = [], Windows = [] });
                    Storage.RenameProfile("Zuhause", "Büro");
                    Assert(Storage.ReadProfile("Zuhause") is null); Assert(Storage.ReadProfile("Büro")?.Location == "Büro");
                    Storage.RenameProfile("Unbekannt", "Egal");                       // nothing saved: nothing to move
                    Assert(Storage.ReadProfile("Egal") is null);
                    Storage.DeleteProfile("Büro"); Storage.DeleteProfile("Büro");     // deleting twice is fine
                    Assert(Storage.ReadProfile("Büro") is null);
                }
                finally { Storage.Root = previous; try { Directory.Delete(temporary, true); } catch (IOException) { } }
            });
            Test("Atomic configuration/profile roundtrip", () =>
            {
                Storage.Save(Storage.SettingsPath, new Settings()); Assert(Storage.ReadSettings().Locations.Count == 0);
                Storage.Save(Storage.SettingsPath, new Settings { AutoRestore = false }); Assert(!Storage.ReadSettings().AutoRestore);
                var profile = new LayoutProfile { Location = "Zuhause", Monitors = [monitor], Windows = [new() { Executable = "test", Minimum = new(10, 20), Maximum = new(30, 40), Normal = new(-800, 50, 700, 600), ShowCommand = 3 }] };
                Storage.Save(Storage.ProfilePath(profile.Location), profile); var read = Storage.ReadProfile(profile.Location)!;
                Assert(read.Windows[0].Minimum.X == 10 && read.Windows[0].Normal.X == -800 && read.Windows[0].ShowCommand == 3);
            });
            Test("Window classes with a number that changes at every start still match", () =>
            {
                Assert(WindowLayouts.StableClass("innovaphoneMyApps_089F6938") == WindowLayouts.StableClass("innovaphoneMyApps_088660E0"));
                Assert(WindowLayouts.StableClass("HwndWrapper[app.exe;;3f2b1c4d-0a9e-4b7d-8c11-5d6e7f809a1b]") == WindowLayouts.StableClass("HwndWrapper[app.exe;;9a8b7c6d-1e2f-4a3b-9c4d-0e1f2a3b4c5d]"));
                Assert(WindowLayouts.StableClass("Chrome_WidgetWin_1") == "Chrome_WidgetWin_1" && WindowLayouts.StableClass("Qt5QWindowIcon") == "Qt5QWindowIcon");
                Assert(WindowLayouts.StableClass("Alpha_ABCDEF12") != WindowLayouts.StableClass("Beta_ABCDEF12"));
                var saved = new WindowSnapshot { Executable = "app", ClassName = "App_089F6938", Title = "A" };
                Assert(WindowLayouts.Match([saved], [new(1, 1, new() { Executable = "app", ClassName = "App_088660E0", Title = "A" })]).Count == 1);
            });
            Test("Window matching preserves titles and rejects ambiguity", () =>
            {
                var a = new WindowSnapshot { Executable = "app", ClassName = "window", Title = "A" }; var b = new WindowSnapshot { Executable = "app", ClassName = "window", Title = "B" };
                var matches = WindowLayouts.Match([a, b], [new(1, 1, b), new(2, 1, a)]); Assert(matches.Count == 2 && matches[0].Live.Handle == 2);
                Assert(WindowLayouts.Match([a, b], [new(1, 1, new() { Executable = "app", ClassName = "window", Title = "Changed" })]).Count == 0);
                Assert(WindowLayouts.Match([a], [new(1, 1, b)]).Count == 1);
                Assert(WindowLayouts.Match([a, a], [new(1, 1, a), new(2, 1, a)]).Count == 0);
            });
            Test("Minimized windows excluded when saving a layout", () =>
            {
                Assert(WindowLayouts.ShouldSave(new WindowSnapshot { ShowCommand = 1 }));
                Assert(WindowLayouts.ShouldSave(new WindowSnapshot { ShowCommand = 3 }));
                Assert(!WindowLayouts.ShouldSave(new WindowSnapshot { ShowCommand = 2 }));
                Assert(!WindowLayouts.ShouldSave(new WindowSnapshot { ShowCommand = 6 }));
            });
            Test("Native ABI sizes", () => { Assert(Marshal.SizeOf<Native.Input>() == 40); Assert(Marshal.SizeOf<Native.Placement>() == 44); Assert(Marshal.SizeOf<Native.InterfaceRow>() == 1352); });
            Test("Owned application windows included; tool windows and dialogs excluded", () =>
            {
                Assert(WindowLayouts.IsApplicationWindow(0x40101, true));
                Assert(!WindowLayouts.IsApplicationWindow(0x101, true));
                Assert(!WindowLayouts.IsApplicationWindow(0x40080, true));
                Assert(WindowLayouts.IsApplicationWindow(0, false));
            });
            Test("Real adapter discovery", () => { var adapters = NetworkDiscovery.Capture(); report.Add($"INFO {adapters.Count} adapters, {adapters.Count(a => a.Physical)} physical, location={NetworkDiscovery.Detect(rules, adapters) ?? "unknown"}"); });
            Test("Real monitor discovery", () => { var monitors = WindowLayouts.Monitors(); Assert(monitors.Count > 0); Assert(WindowLayouts.Compatible(monitors, monitors)); report.Add($"INFO {monitors.Count} monitors"); });
            Test("Real desktop window enumeration (read only)", () => { var windows = WindowLayouts.Capture(); report.Add($"INFO {windows.Count} eligible windows"); });
            Test("Hotkey registration and release", () =>
            {
                using var host = new HotkeyWindow();
                var s = new Settings { Apps = [new() { Name = "Test", Executable = @"C:\Windows\notepad.exe", Hotkey = "Ctrl+Alt+Shift+F11" }] };
                Assert(host.Configure(s, _ => { }, _ => { }).Count == 0); Assert(host.Configure(new Settings(), _ => { }, _ => { }).Count == 0);
            });
            Test("Credential Manager synthetic roundtrip and deletion", () =>
            {
                var id = "test-" + Guid.NewGuid().ToString("N");
                try { Credentials.Write(id, "synthetic-user", "Synthetic-ä-🔑"); var secret = Credentials.Read(id); try { Assert(secret.Username == "synthetic-user" && new string(secret.Password) == "Synthetic-ä-🔑"); } finally { Array.Clear(secret.Password); } }
                finally { Credentials.Delete(id); }
            });
            Test("Native window restore on disposable test window only", () =>
            {
                using var window = new Form { Text = "WorkspaceManager synthetic test", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(90, 90, 420, 240), ShowInTaskbar = true };
                // A hidden process launch can override the first ShowWindow call via STARTUPINFO.
                window.Show(); window.Hide(); window.Show(); Application.DoEvents();
                Native.DwmGetWindowAttribute(window.Handle, 14, out var testCloaked, 4);
                report.Add($"INFO test window visible={Native.IsWindowVisible(window.Handle)}, owner={Native.GetWindow(window.Handle, 4)}, style={(long)Native.GetWindowLongPtr(window.Handle, -20):X}, cloaked={testCloaked}, executableFound={WindowLayouts.Executable(window.Handle) is not null}, captured={WindowLayouts.Capture(true).Any(w => w.Handle == window.Handle)}");
                var saved = WindowLayouts.Capture(true).Single(w => w.Handle == window.Handle).Snapshot;
                window.Bounds = new Rectangle(150, 150, 500, 300); Application.DoEvents();
                Assert(WindowLayouts.Apply(window.Handle, saved)); Application.DoEvents();
                var restored = WindowLayouts.Capture(true).Single(w => w.Handle == window.Handle).Snapshot;
                Assert(restored.Normal == saved.Normal);
                var outer = saved.ScreenBounds ?? throw new Exception("Actual screen bounds not captured");
                var snapped = new WindowSnapshot { Executable = saved.Executable, ClassName = saved.ClassName, ShowCommand = 1, Normal = saved.Normal,
                    ScreenBounds = new(outer.X + 30, outer.Y + 20, outer.Width + 180, outer.Height + 100) };
                Assert(WindowLayouts.Apply(window.Handle, snapped)); Application.DoEvents();
                var snapRestored = WindowLayouts.Capture(true).Single(w => w.Handle == window.Handle).Snapshot;
                Assert(snapRestored.ScreenBounds == snapped.ScreenBounds);
                var visible = snapRestored.VisibleBounds ?? throw new Exception("Visible bounds not captured");
                snapped.ScreenBounds = snapRestored.ScreenBounds;
                snapped.VisibleBounds = new(visible.X + 20, visible.Y + 10, visible.Width + 100, visible.Height + 70);
                Assert(WindowLayouts.Apply(window.Handle, snapped)); Application.DoEvents();
                Assert(WindowLayouts.Capture(true).Single(w => w.Handle == window.Handle).Snapshot.VisibleBounds == snapped.VisibleBounds);
                report.Add("INFO Actual screen bounds override stale WINDOWPLACEMENT size successfully");
                window.WindowState = FormWindowState.Maximized; Application.DoEvents();
                var maximized = WindowLayouts.Capture(true).Single(w => w.Handle == window.Handle).Snapshot;
                window.WindowState = FormWindowState.Normal; Assert(WindowLayouts.Apply(window.Handle, maximized)); Application.DoEvents(); Assert(window.WindowState == FormWindowState.Maximized);
                window.WindowState = FormWindowState.Minimized; Application.DoEvents();
                var minimized = WindowLayouts.Capture(true).Single(w => w.Handle == window.Handle).Snapshot;
                window.WindowState = FormWindowState.Normal; Assert(WindowLayouts.Apply(window.Handle, minimized)); Application.DoEvents(); Assert(window.WindowState == FormWindowState.Minimized);
                window.Close();
            });
            Test("Synthetic login typing into own disposable fields", () =>
            {
                // Needs a real interactive foreground window, so automated builds can switch it off.
                if (args.Contains("--no-input-test")) throw new TestSkipped("Disabled by --no-input-test.");
                using var window = new Form { Text = "WorkspaceManager synthetic login", Size = new Size(440, 220) };
                var user = new TextBox { Top = 20, Left = 20, Width = 350, TabIndex = 0 };
                var password = new TextBox { Top = 60, Left = 20, Width = 350, TabIndex = 1, UseSystemPasswordChar = true };
                window.Controls.AddRange([user, password]); window.Show(); window.Hide(); window.Show(); window.Activate(); Native.SetForegroundWindow(window.Handle); user.Focus(); Application.DoEvents();
                report.Add($"INFO login test visible={window.Visible}, foreground={Native.GetForegroundWindow() == window.Handle}, executableMatch={WindowLayouts.Executable(window.Handle) == Environment.ProcessPath}");
                if (args.Contains("--interactive-login-test"))
                {
                    var ready = false;
                    var begin = new Button { Text = "Testeingabe starten", Top = 110, Left = 20, Width = 350 };
                    begin.Click += (_, _) => { user.Focus(); ready = true; }; window.Controls.Add(begin);
                    var waitUntil = DateTime.UtcNow.AddSeconds(60);
                    while (!ready && !window.IsDisposed && DateTime.UtcNow < waitUntil) { Application.DoEvents(); Thread.Sleep(20); }
                    if (!ready) throw new TestSkipped("Interactive test button was not clicked.");
                }
                if (Native.GetForegroundWindow() != window.Handle)
                {
                    // The same guard protects real logins. Never send input to whichever app owns focus.
                    var blocked = LoginSender.Send(new LoginEntry { AllowedExecutable = @"C:\does-not-match.exe" });
                    Assert(blocked.IsFaulted);
                    throw new TestSkipped("Windows denied foreground activation for the automated process; wrong-target guard verified. Use --interactive-login-test and click the test button to verify actual typing.");
                }
                var id = "test-" + Guid.NewGuid().ToString("N");
                try
                {
                    Credentials.Write(id, "synthetic-user", "Test-ä-🔑");
                    var entry = new LoginEntry { Id = id, AllowedExecutable = Environment.ProcessPath!, TitleContains = "synthetic login" };
                    var task = LoginSender.Send(entry); var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
                    Assert(task.IsCompleted); task.GetAwaiter().GetResult(); Application.DoEvents();
                    Assert(user.Text == "synthetic-user" && password.Text == "Test-ä-🔑");
                    entry.AllowedExecutable = @"C:\does-not-match.exe";
                    var blocked = LoginSender.Send(entry); Assert(blocked.IsFaulted);
                    Assert(user.Text == "synthetic-user" && password.Text == "Test-ä-🔑");
                }
                finally { Credentials.Delete(id); user.Clear(); password.Clear(); window.Close(); }
            });
            Test("Settings surface render", () =>
            {
                using var form = new SettingsForm(new Settings { Locations = SampleLocations() }, () => "Test: Zuhause · 3 Monitore", _ => { });
                form.Show(); Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                var reportIndex = Array.IndexOf(args, "--report");
                if (reportIndex >= 0) bitmap.Save(Path.Combine(Path.GetDirectoryName(args[reportIndex + 1])!, "settings-preview.png"));
                form.Close();
            });
            Test("Tray startup and disposal with isolated configuration", () =>
            {
                Storage.Save(Storage.SettingsPath, new Settings { AutoRestore = false });
                using var context = new TrayContext(); Application.DoEvents();
            });
        }
        finally
        {
            // Only the unique test directory created above is removed.
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
        report.Add($"RESULT {report.Count(x => x.StartsWith("PASS"))} passed; {failed} failed; {report.Count(x => x.StartsWith("SKIP"))} skipped");
        var index = Array.IndexOf(args, "--report");
        var reportPath = index >= 0 && args.Length > index + 1 ? args[index + 1] : Path.Combine(AppContext.BaseDirectory, "test-results.txt");
        File.WriteAllLines(reportPath, report);
        return failed == 0 ? 0 : 1;
    }
}
