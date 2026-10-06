using System.Text.Json;
using WorkspaceManager.Addons;

namespace WorkspaceManager;

internal static class Ui
{
    public static Color Ink => AppTheme.Ink;
    public static Color Muted => AppTheme.Muted;
    public static Color Accent => AppTheme.Accent;
    public static Color Background => AppTheme.Background;
    public static void Prepare(Form form, string title, Size size)
    {
        form.SuspendLayout();
        form.Font = new Font("Segoe UI", 10); form.Icon = Branding.Window();
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.Text = title; form.ClientSize = size; form.BackColor = Background; form.ForeColor = Ink;
        form.StartPosition = FormStartPosition.CenterParent;
    }
    public static void Finish(Form form)
    {
        form.AutoScaleDimensions = new SizeF(96, 96);
        form.ResumeLayout(true);
        AppTheme.Bind(form);
    }
    public static TableLayoutPanel Stack(int padding = 0) => new SmoothStack { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Padding = new Padding(padding), Margin = new Padding(0, 0, 0, 16), ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) } };
    public static void Add(TableLayoutPanel panel, Control control)
    {
        control.Dock = DockStyle.Top;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(control, 0, panel.RowCount++);
    }
    public static Label Text(string text, float size = 10, bool bold = false, bool muted = false) => new() { Text = text, Tag = muted ? "muted" : "text", AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = muted ? Muted : Ink, Margin = new Padding(0, 0, 0, 12), MaximumSize = new Size(640, 0) };
    public static void Paragraph(TableLayoutPanel panel, string text, float size = 10, bool bold = false, bool muted = false)
    {
        var label = Text(text, size, bold, muted); Add(panel, label);
        panel.SizeChanged += (_, _) => label.MaximumSize = new Size(Math.Max(100, panel.ClientSize.Width - panel.Padding.Horizontal - 8), 0);
    }
    public static ThemeButton Button(string text, Action action, bool primary = false)
    {
        var b = new ThemeButton { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16, 9, 16, 9), Margin = new Padding(0, 0, 10, 8), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderSize = 0;
        b.Tag = primary ? "primary" : "button";
        b.Click += (_, _) => SettingsForm.Safe(action); return b;
    }
    public static FlowLayoutPanel Actions(params Button[] buttons)
    {
        var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = new Padding(0, 10, 0, 4), WrapContents = false };
        row.Controls.AddRange(buttons); return row;
    }
    public static TableLayoutPanel Card(TableLayoutPanel parent)
    {
        var card = new CardPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Padding = new Padding(24, 20, 24, 10), Margin = new Padding(0, 0, 0, 16), ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) }, Tag = "surface" };
        Add(parent, card); return card;
    }
    public static Control Row(string title, string detail, params (string Label, Action Action)[] items)
    {
        var row = new RowPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Padding = new Padding(0, 10, 0, 10), Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var text = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        var name = Text(title, 10.5f, true); name.Margin = new Padding(0, 0, 0, 2); var info = Text(detail, 9.5f, muted: true); info.Margin = Padding.Empty;
        text.Controls.Add(name); text.Controls.Add(info);
        var menu = new ContextMenuStrip { Font = new Font("Segoe UI", 10), ShowImageMargin = false };
        foreach (var (label, action) in items)
        {
            if (label == "-") { menu.Items.Add(new ToolStripSeparator()); continue; }
            menu.Items.Add(label, null, (_, _) => SettingsForm.Safe(action));
        }
        var more = new ThemeButton { Text = "Bearbeiten  ▾", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(14, 7, 14, 7), Margin = Padding.Empty, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Anchor = AnchorStyles.Right, Tag = "button" };
        more.FlatAppearance.BorderSize = 0;
        more.Click += (_, _) => { AppTheme.ApplyMenu(menu); menu.Show(more, new Point(more.Width - menu.GetPreferredSize(Size.Empty).Width, more.Height + 4)); };
        row.Disposed += (_, _) => menu.Dispose();
        row.Controls.Add(text, 0, 0); row.Controls.Add(more, 1, 0); return row;
    }
    // Read-only text that can be selected and copied (labels cannot).
    public static TextBox Selectable(TableLayoutPanel panel, string text, float size = 9.5f)
    {
        var box = new WheelPassTextBox { Text = text, ReadOnly = true, Multiline = true, WordWrap = true, BorderStyle = BorderStyle.None, TabStop = false, ScrollBars = ScrollBars.None, Font = new Font("Segoe UI", size), Tag = "selectable", Margin = new Padding(0, 0, 0, 10), Cursor = Cursors.IBeam };
        void Fit() { var width = Math.Max(100, box.ClientSize.Width); box.Height = TextRenderer.MeasureText(box.Text, box.Font, new Size(width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 6; }
        box.SizeChanged += (_, _) => Fit();
        box.HandleCreated += (_, _) => Fit();
        Add(panel, box); return box;
    }
    public static Button CopyButton(string text, Func<string> value)
    {
        Button? button = null;
        button = Button(text, () =>
        {
            Clipboard.SetText(value()); button!.Text = "Kopiert ✓";
            var reset = new System.Windows.Forms.Timer { Interval = 1500 };
            reset.Tick += (_, _) => { reset.Dispose(); if (!button.IsDisposed) button.Text = text; }; reset.Start();
        });
        return button;
    }
    public const string LaunchFilter = "Programme, Skripte und Verbindungen|*.exe;*.bat;*.cmd;*.rdp;*.lnk|Windows-Programme (*.exe)|*.exe|Skripte (*.bat, *.cmd)|*.bat;*.cmd|RDP-Verbindungen (*.rdp)|*.rdp|Verknüpfungen (*.lnk)|*.lnk";
    public static void Option(TableLayoutPanel card, string text, string hint, bool isChecked, Action<bool> changed)
    {
        var box = new CheckBox { Text = text, AutoSize = true, Checked = isChecked, Margin = new Padding(0, 4, 0, 2) };
        box.CheckedChanged += (_, _) => SettingsForm.Safe(() => changed(box.Checked));
        Add(card, box);
        var note = Text(hint, 9.5f, muted: true); note.Margin = new Padding(26, 0, 0, 14); Add(card, note);
    }
    public static ThemeButton Reveal(TextBox box)
    {
        ThemeButton? button = null;
        button = Button("Anzeigen", () => { box.UseSystemPasswordChar = !box.UseSystemPasswordChar; button!.Text = box.UseSystemPasswordChar ? "Anzeigen" : "Verbergen"; });
        return button;
    }
    public static string KeyText(string hotkey) => hotkey.Replace("Ctrl", "Strg", StringComparison.OrdinalIgnoreCase).Replace("Shift", "Umschalt", StringComparison.OrdinalIgnoreCase);
    public static Control Brand()
    {
        var header = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
        var logo = new PictureBox { Image = Branding.Logo(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(36, 36), Margin = new Padding(0, 0, 10, 0) };
        var name = Text("Workspace", 15, true); name.Margin = new Padding(0, 6, 0, 0);
        header.Controls.Add(logo); header.Controls.Add(name); return header;
    }
    public static void Field(TableLayoutPanel panel, string label, Control input, Button? action = null)
    {
        Paragraph(panel, label, bold: true);
        if (input is TextBox box)
        {
            var frame = new InputFrame(box) { Margin = new Padding(0, 0, 0, 18) };
            if (action is null) { Add(panel, frame); return; }
            var row = new SmoothStack { ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 18) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            frame.Margin = Padding.Empty; frame.Dock = DockStyle.Fill; action.Margin = new Padding(10, 0, 0, 0); action.Dock = DockStyle.Fill;
            row.Controls.Add(frame, 0, 0); row.Controls.Add(action, 1, 0); Add(panel, row); return;
        }
        input.Margin = new Padding(0, 0, 0, 18); Add(panel, input);
    }
    public static TableLayoutPanel ScrollContent(Control parent)
    {
        var scroll = new ScrollPanel { Dock = DockStyle.Fill, Padding = new Padding(32, 28, 32, 24) };
        var stack = Stack(); scroll.Controls.Add(stack); parent.Controls.Add(scroll); return stack;
    }
}

internal sealed class SettingsForm : Form
{
    Settings settings;
    readonly Action<Settings> save;
    readonly Func<string> getStatus;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    readonly Panel content = new() { Dock = DockStyle.Fill };
    readonly Label status = Ui.Text("", 12, true);
    readonly Label feedback = Ui.Text("Änderungen werden direkt gespeichert.", muted: true);
    string page = "Übersicht";
    bool initialized;
    readonly Dictionary<string, ThemeButton> navigation = [];
    readonly SmoothStack navHost = new() { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = Padding.Empty, ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) } };
    readonly ToolTip tips = new();
    readonly System.Windows.Forms.Timer slide = new() { Interval = 15 };
    readonly TableLayoutPanel root;
    readonly SidePanel sidebar;
    readonly Control brandName, tagline;
    bool collapsed;
    float sideWidth = 240, sideTarget = 240;
    static string SidebarFile => Path.Combine(Storage.Root, "sidebar.txt");
    public SettingsForm(Settings settings, Func<string> getStatus, Action<Settings> save)
    {
        this.settings = Storage.ParseSettings(JsonSerializer.Serialize(settings, Storage.Json)); this.save = save; this.getStatus = getStatus;
        Ui.Prepare(this, "Workspace Manager", new Size(1040, 760)); MinimumSize = new Size(800, 620); StartPosition = FormStartPosition.CenterScreen;
        try { collapsed = File.Exists(SidebarFile) && File.ReadAllText(SidebarFile).Trim() == "collapsed"; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        sideWidth = sideTarget = collapsed ? 72 : 240;
        root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, sideWidth)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar = new SidePanel { ColumnCount = 1, Dock = DockStyle.Fill, Padding = collapsed ? new Padding(12, 20, 12, 16) : new Padding(16, 20, 16, 16), Tag = "surface", ColumnStyles = { new ColumnStyle(SizeType.Percent, 100) } };
        var toggle = Ui.Button("Menü", ToggleSidebar); toggle.Glyph = "\uE700"; toggle.IconOnly = true; toggle.Tag = "nav"; toggle.AutoSize = false; toggle.Size = new Size(44, 40); toggle.Margin = new Padding(0, 0, 0, 14);
        tips.SetToolTip(toggle, "Menü ein-/ausklappen"); Ui.Add(sidebar, toggle); toggle.Dock = DockStyle.None;
        var brand = Ui.Brand(); brandName = ((FlowLayoutPanel)brand).Controls[1]; Ui.Add(sidebar, brand);
        tagline = Ui.Text("Dein Platz.\nDeine Programme.", muted: true); tagline.Margin = new Padding(0, 0, 0, 14); Ui.Add(sidebar, tagline);
        Ui.Add(sidebar, navHost); BuildNavigation();
        slide.Tick += (_, _) =>
        {
            var left = sideTarget - sideWidth;
            if (Math.Abs(left) < 2) { sideWidth = sideTarget; slide.Stop(); } else sideWidth += left * 0.35f;
            root.ColumnStyles[0].Width = sideWidth * DeviceDpi / 96f;
        };
        ApplySidebar();
        root.Controls.Add(sidebar, 0, 0); root.Controls.Add(content, 1, 0); Controls.Add(root);
        timer.Tick += (_, _) => status.Text = getStatus(); timer.Start(); ShowPage(page);
        Shown += (_, _) => { var work = Screen.FromControl(this).WorkingArea; if (Width > work.Width || Height > work.Height) Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height)); };
        Ui.Finish(this);
        initialized = true;
    }
    // The sidebar: the fixed pages, then the pages of the addons that are switched on, then Einstellungen.
    void BuildNavigation()
    {
        navHost.SuspendLayout();
        foreach (var control in navHost.Controls.Cast<Control>().ToArray()) control.Dispose();
        navHost.Controls.Clear(); navHost.RowStyles.Clear(); navHost.RowCount = 0; navigation.Clear();
        var entries = new List<(string Key, string Text, string Glyph)>
            { ("Übersicht", "Übersicht", "\uE80F"), ("Programme", "Programme", "\uE71D"), ("Logins", "Logins", "\uE72E"), ("Standorte", "Standorte", "\uE774") };
        if (AddonManager.Current is { } manager)
            foreach (var (addon, addonPage) in manager.Pages()) entries.Add(($"AddonPage|{addon.Id}|{addonPage.Title}", addonPage.Title, addonPage.Glyph));
        entries.Add(("Einstellungen", "Einstellungen", "\uE713"));
        foreach (var (key, text, glyph) in entries)
        {
            var button = Ui.Button(text, () => ShowPage(key)); button.TextAlign = ContentAlignment.MiddleLeft; button.Margin = new Padding(0, 2, 0, 2);
            button.Tag = "nav"; button.Glyph = glyph; navigation[key] = button; Ui.Add(navHost, button);
        }
        navHost.ResumeLayout(true);
        if (initialized && DeviceDpi != 96) navHost.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f));
        ApplySidebar();
    }
    static string TitleOf(string name) =>
        IsSettingsPage(name) ? "Einstellungen" : name.StartsWith("AddonPage|", StringComparison.Ordinal) ? name.Split('|', 3)[2] : name;
    void ApplySidebar()
    {
        foreach (var item in navigation) { item.Value.IconOnly = collapsed; tips.SetToolTip(item.Value, collapsed ? item.Value.Text : ""); item.Value.Invalidate(); }
        brandName.Visible = tagline.Visible = !collapsed;
        sidebar.Padding = collapsed ? new Padding(12, 20, 12, 16) : new Padding(16, 20, 16, 16);
    }
    void ToggleSidebar()
    {
        collapsed = !collapsed; sideTarget = collapsed ? 72 : 240; ApplySidebar(); slide.Start();
        try { Directory.CreateDirectory(Storage.Root); File.WriteAllText(SidebarFile, collapsed ? "collapsed" : "expanded"); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    void Commit(Settings next)
    {
        next = Storage.ParseSettings(JsonSerializer.Serialize(next, Storage.Json));
        var themeChanged = AppTheme.Mode != next.ThemeMode; save(next); settings = next;
        if (themeChanged) { AppTheme.Refresh(); AppTheme.Apply(this); }
        feedback.Text = "Gespeichert.";
    }
    Settings Copy() => Storage.ParseSettings(JsonSerializer.Serialize(settings, Storage.Json));
    void ShowPage(string name)
    {
        if (!LicenseService.State.Allowed) name = "Lizenz";
        if (name.StartsWith("AddonPage|", StringComparison.Ordinal) && !navigation.ContainsKey(name)) name = "Übersicht"; // the addon was switched off
        page = name;
        // Reusable status labels are detached before disposing a page.
        status.Parent?.Controls.Remove(status); feedback.Parent?.Controls.Remove(feedback);
        foreach (Control child in content.Controls.Cast<Control>().ToArray()) child.Dispose();
        foreach (var item in navigation) item.Value.Tag = item.Key == (IsSettingsPage(name) ? "Einstellungen" : name) ? "navActive" : "nav";
        var body = Ui.ScrollContent(content);
        Ui.Paragraph(body, TitleOf(name), 24, true);
        if (IsSettingsPage(name)) SettingsTabs(body, name);
        switch (name)
        {
            case "Übersicht": Overview(body); break;
            case "Programme": Apps(body); break;
            case "Logins": Logins(body); break;
            case "Standorte": Locations(body); break;
            case "Einstellungen": Preferences(body); break;
            case "Lizenz": LicensePage(body); break;
            case "Erweitert": Advanced(body); break;
            case "Info": InfoPage(body); break;
            case "Addons": AddonsPage(body); break;
            default:
                if (name.StartsWith("Addon|", StringComparison.Ordinal)) AddonPage(body, name);
                else if (name.StartsWith("AddonPage|", StringComparison.Ordinal)) AddonSidebarPage(body, name);
                break;
        }
        if (initialized && DeviceDpi != 96) body.Parent!.Scale(new SizeF(DeviceDpi / 96f, DeviceDpi / 96f));
        AppTheme.Apply(this);
    }
    void Overview(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Fenster einmal anordnen. Am richtigen Ort wiederfinden.", muted: true);
        var currentName = NetworkDiscovery.Detect(settings.Locations, NetworkDiscovery.Capture());
        var current = Ui.Card(body); status.Text = getStatus(); Ui.Add(current, status);
        Ui.Paragraph(current, settings.Locations.Count == 0
            ? "Es ist noch kein Standort angelegt. Lege unter Standorte deine Orte an, zum Beispiel Zuhause oder ein Büro."
            : currentName is null
                ? "Zu deinem aktuellen Netzwerk passt kein angelegter Standort. Lege unter Standorte einen an oder passe die Erkennung an."
                : "Dein Standort wird automatisch erkannt. Ein Layout wird nur mit den dazu passenden Monitoren geladen.", muted: true);
        if (settings.Locations.Count == 0)
            Ui.Add(current, Ui.Actions(Ui.Button("Standort anlegen", () => ShowPage("Standorte"), true)));
        else
            Ui.Add(current, Ui.Actions(Ui.Button("Layout wiederherstellen", () =>
            {
                var location = NetworkDiscovery.Detect(settings.Locations, NetworkDiscovery.Capture()) ?? throw new InvalidOperationException("Standort ist noch nicht eindeutig erkannt.");
                feedback.Text = WindowLayouts.Restore(location);
            }, true), Ui.Button("Standorte verwalten", () => ShowPage("Standorte"))));
        Ui.Add(current, feedback);

        var license = LicenseService.State;
        // With a valid license key there is nothing to look after, the details are under Einstellungen, Lizenz.
        if (license.Kind == LicenseKind.Licensed) return;
        var licenseCard = Ui.Card(body); Ui.Paragraph(licenseCard, "Lizenz", 13.5f, true);
        Ui.Paragraph(licenseCard, license.Headline, 10.5f, true); Ui.Paragraph(licenseCard, license.Detail, muted: true);
        Ui.Add(licenseCard, Ui.Actions(Ui.Button("Lizenz verwalten", () => ShowPage("Lizenz"), license.Kind is LicenseKind.Trial or LicenseKind.Blocked)));
    }
    void LocationCard(TableLayoutPanel body, LocationRule rule, string? currentName)
    {
        var card = Ui.Card(body);
        Ui.Add(card, Ui.Row(rule.Name + (rule.Name == currentName ? "  ·  aktueller Standort" : ""),
            $"{rule.Subnet}  ·  Gateway: {rule.Gateway ?? "automatisch"}",
            ("Erkennung bearbeiten", () => EditLocation(rule)), ("Umbenennen", () => RenameLocation(rule)), ("-", () => { }), ("Löschen", () => DeleteLocation(rule))));
        LayoutProfile? profile = null; string? problem = null;
        try { profile = Storage.ReadProfile(rule.Name); }
        catch (Exception e) when (e is InvalidDataException or JsonException or IOException) { problem = "Das gespeicherte Layout ist nicht lesbar."; }
        Ui.Paragraph(card, problem ?? (profile is null ? "Noch kein Layout gespeichert. Ordne deine Fenster an und speichere sie hier." : $"{profile.Windows.Count} Fenster · gespeichert am {profile.SavedAt:dd.MM.yyyy, HH:mm}"), muted: true);
        if (profile is not null)
            foreach (var window in profile.Windows)
                Ui.Add(card, Ui.Row(window.Title, window.ShowCommand is 2 or 6 or 7 ? "Minimiert" : window.ShowCommand == 3 ? "Maximiert" : "Gespeicherte Größe",
                    ("Position bearbeiten", () => EditLayoutWindow(profile, window)),
                    ("Aktuelle Position übernehmen", () => CaptureLayoutWindow(profile, window)),
                    ("-", () => { }),
                    ("Entfernen", () => RemoveLayoutWindow(profile, window))));
        if (profile is null)
            Ui.Add(card, Ui.Actions(Ui.Button($"Layout als {rule.Name} speichern", () => { WindowLayouts.Save(rule.Name); ShowPage(page); })));
        else
            Ui.Add(card, Ui.Actions(
                Ui.Button("Positionen aktualisieren", () => UpdateProfile(profile)),
                Ui.Button("Layout neu erfassen", () =>
                {
                    if (MessageBox.Show(this, "Das gespeicherte Layout wird durch alle aktuell geöffneten Fenster ersetzt. Entfernte Fenster kommen dadurch wieder zurück. Fortfahren?", "Layout neu erfassen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    WindowLayouts.Save(rule.Name); ShowPage(page);
                })));
    }
    void SaveProfile(LayoutProfile profile)
    {
        profile.SavedAt = DateTimeOffset.Now;
        Storage.Save(Storage.ProfilePath(profile.Location), profile);
        ShowPage(page);
    }
    void UpdateProfile(LayoutProfile profile)
    {
        // Only refreshes windows that are already part of the layout; removed windows stay removed.
        if (!WindowLayouts.Compatible(profile.Monitors, WindowLayouts.Monitors())) throw new InvalidOperationException("Die aktuelle Monitorlandschaft passt nicht zu diesem Layout.");
        var updated = 0;
        foreach (var match in WindowLayouts.Match(profile.Windows, WindowLayouts.Capture()))
        {
            var index = profile.Windows.IndexOf(match.Saved);
            if (index < 0 || !WindowLayouts.ShouldSave(match.Live.Snapshot)) continue;
            profile.Windows[index] = match.Live.Snapshot; updated++;
        }
        SaveProfile(profile);
        feedback.Text = $"{updated} von {profile.Windows.Count} Fenstern aktualisiert. Nicht geöffnete Fenster blieben unverändert.";
    }
    void RemoveLayoutWindow(LayoutProfile profile, WindowSnapshot window)
    {
        profile.Windows.Remove(window); SaveProfile(profile);
    }
    void CaptureLayoutWindow(LayoutProfile profile, WindowSnapshot window)
    {
        if (!WindowLayouts.Compatible(profile.Monitors, WindowLayouts.Monitors())) throw new InvalidOperationException("Die aktuelle Monitorlandschaft passt nicht zu diesem Layout.");
        var matches = WindowLayouts.Match([window], WindowLayouts.Capture());
        if (matches.Count != 1) throw new InvalidOperationException("Dieses Fenster ist nicht eindeutig geöffnet.");
        profile.Windows[profile.Windows.IndexOf(window)] = matches[0].Live.Snapshot;
        SaveProfile(profile);
    }
    void EditLayoutWindow(LayoutProfile profile, WindowSnapshot window)
    {
        if (window.ShowCommand is 2 or 6 or 7) throw new InvalidOperationException("Minimierte Fenster bitte entfernen oder geöffnet neu erfassen.");
        if (window.ShowCommand == 3) throw new InvalidOperationException("Bei maximierten Fenstern bestimmt der Monitor die Größe. Für freie Koordinaten das Fenster zuerst wiederherstellen und dann die aktuelle Position übernehmen.");
        var oldVisible = window.VisibleBounds ?? window.ScreenBounds ?? window.Normal;
        using var dialog = new EntryDialog(window.Title + " – Position", ["X (sichtbarer linker Rand)", "Y (sichtbarer oberer Rand)", "Breite", "Höhe"],
            [oldVisible.X.ToString(), oldVisible.Y.ToString(), oldVisible.Width.ToString(), oldVisible.Height.ToString()]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var fields = dialog.Values;
        if (!int.TryParse(fields[0], out var x) || !int.TryParse(fields[1], out var y) || !int.TryParse(fields[2], out var width) || !int.TryParse(fields[3], out var height) || width < 100 || height < 100)
            throw new InvalidDataException("Bitte gültige Koordinaten sowie Breite und Höhe ab 100 eingeben.");
        var desired = new Rect(x, y, width, height);
        var outer = window.ScreenBounds ?? oldVisible;
        window.ScreenBounds = new Rect(outer.X + x - oldVisible.X, outer.Y + y - oldVisible.Y,
            outer.Width + width - oldVisible.Width, outer.Height + height - oldVisible.Height);
        window.VisibleBounds = desired;
        if (window.ScreenBounds.Width < 100 || window.ScreenBounds.Height < 100) throw new InvalidDataException("Die Zielgröße ist für dieses Fenster zu klein.");
        SaveProfile(profile);
    }
    void Apps(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Starte deine Programme mit einem Tastenkürzel – auf Wunsch direkt am gespeicherten Platz.", muted: true);
        Ui.Add(body, Ui.Actions(Ui.Button("Programm hinzufügen", () => EditApp(null), true)));
        if (settings.Apps.Count == 0) { var empty = Ui.Card(body); Ui.Paragraph(empty, "Dein erster Schnellstart", 13.5f, true); Ui.Paragraph(empty, "Wähle ein Programm und lege ein Kürzel wie Strg+Alt+B fest. Du brauchst dafür keine Konfigurationsdatei zu bearbeiten.", muted: true); }
        foreach (var entry in settings.Apps)
        {
            var card = Ui.Card(body); Ui.Paragraph(card, entry.Name, 13.5f, true); Ui.Paragraph(card, Ui.KeyText(entry.Hotkey) + (entry.RestorePosition ? " · Gespeicherte Position verwenden" : " · Nur starten") + entry.RunAs switch { "Admin" => " · Als Administrator", "User" => " · Als " + (settings.Logins.FirstOrDefault(l => l.Id == entry.RunAsLoginId)?.Name ?? "unbekannter Login"), _ => "" }); Ui.Paragraph(card, entry.Executable + (entry.AutoStartLocations.Count > 0 ? "\nÖffnet sich nach dem Windows-Start automatisch an: " + string.Join(", ", entry.AutoStartLocations) : ""), muted: true);
            Ui.Add(card, Ui.Actions(Ui.Button("Bearbeiten", () => EditApp(entry)), Ui.Button("Entfernen", () => { var next = Copy(); next.Apps.RemoveAll(a => a.Name == entry.Name && a.Hotkey == entry.Hotkey); Commit(next); ShowPage(page); })));
        }
    }
    void EditApp(AppEntry? entry)
    {
        var path = entry?.Executable;
        if (path is null) { using var picker = new OpenFileDialog { Title = "Programm oder Datei auswählen", Filter = Ui.LaunchFilter }; if (picker.ShowDialog(this) != DialogResult.OK) return; path = picker.FileName; }
        using var dialog = new EntryDialog(entry is null ? "Programm hinzufügen" : "Programm bearbeiten", ["Name", "Programm oder Datei (.exe, .bat, .cmd, .rdp, .lnk)", "Tastenkürzel (ins Feld klicken, dann Tasten drücken)", "Startargumente (optional)"], [entry?.Name ?? Path.GetFileNameWithoutExtension(path), path, Ui.KeyText(entry?.Hotkey ?? "Strg+Alt+B"), entry?.Arguments ?? ""], entry?.RestorePosition ?? true, hotkeyField: 2, browseField: 1, locations: settings.Locations.Select(l => l.Name).ToArray(), selectedLocations: entry?.AutoStartLocations, logins: settings.Logins, runAs: entry?.RunAs ?? "", runAsLoginId: entry?.RunAsLoginId ?? "");
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var next = Copy(); if (entry is not null) next.Apps.RemoveAll(a => a.Name == entry.Name && a.Hotkey == entry.Hotkey);
        next.Apps.Add(new() { Name = dialog.Values[0], Executable = dialog.Values[1], Hotkey = dialog.Values[2], Arguments = dialog.Values[3], RestorePosition = dialog.RestorePosition, AutoStartLocations = dialog.SelectedLocations.ToList(), RunAs = dialog.RunAs, RunAsLoginId = dialog.RunAsLoginId });
        Commit(next); ShowPage(page);
    }
    void Logins(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Zugangsdaten per Tastenkürzel eingeben. Sicher im Windows-Anmeldeinformationsmanager gespeichert.", muted: true);
        Ui.Add(body, Ui.Actions(Ui.Button("Neuen Login anlegen", () => OpenLogin(null), true)));
        var list = Ui.Card(body); Ui.Paragraph(list, "Gespeicherte Logins", 13.5f, true);
        if (settings.Logins.Count == 0) Ui.Paragraph(list, "Noch kein Login gespeichert. Lege einen an und gib ihn danach per Tastenkürzel in Anmeldefenster ein, zum Beispiel bei Netzwerkfreigaben oder Remotedesktop.", muted: true);
        foreach (var entry in settings.Logins)
        {
            var scope = string.IsNullOrEmpty(entry.AllowedExecutable) ? "jedes Fenster" : Path.GetFileName(entry.AllowedExecutable);
            var what = entry.Mode switch { "Username" => "nur Benutzername", "Password" => "nur Passwort", _ => "Benutzername und Passwort" };
            Ui.Add(list, Ui.Row(entry.Name, $"{Ui.KeyText(entry.Hotkey)}  ·  {what}  ·  {scope}",
                ("Bearbeiten", () => OpenLogin(entry)),
                ("-", () => { }),
                ("Löschen", () => DeleteLogin(entry))));
        }
        var help = Ui.Card(body); Ui.Paragraph(help, "So funktioniert’s", 13.5f, true); Ui.Paragraph(help, "1. Login anlegen. Optional ein Programm festlegen, in dem er gelten soll.\n2. Das Eingabefeld im Zielprogramm anklicken.\n3. Tastenkürzel drücken und loslassen."); Ui.Paragraph(help, "Die App sendet kein Enter. Bei Browsern bestätigt ein Fenstertitel keine Website-Adresse.", muted: true);
    }
    void OpenLogin(LoginEntry? entry)
    {
        using var dialog = new LoginForm(Copy(), Commit, entry); dialog.ShowDialog(this); ShowPage(page);
    }
    void DeleteLogin(LoginEntry entry)
    {
        if (settings.Apps.FirstOrDefault(a => a.RunAs == "User" && a.RunAsLoginId == entry.Id) is { } user)
            throw new InvalidOperationException($"Dieser Login wird noch von dem Programm „{user.Name}“ zum Starten verwendet. Stelle das Programm zuerst auf eine andere Startart um.");
        if (MessageBox.Show(this, $"Login „{entry.Name}“ samt gespeicherten Zugangsdaten löschen?", "Login löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var next = Copy(); next.Logins.RemoveAll(l => l.Id == entry.Id); Commit(next); Credentials.Delete(entry.Id); ShowPage(page);
    }
    static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text;
    static string ProfileInfo(string location)
    {
        try { return Storage.ReadProfile(location) is { } profile ? $"Layout mit {profile.Windows.Count} {(profile.Windows.Count == 1 ? "Fenster" : "Fenstern")}" : "noch kein Layout"; }
        catch (Exception e) when (e is InvalidDataException or JsonException or IOException) { return "Layout nicht lesbar"; }
    }
    // Prefill for a new location from the active physical network (first IPv4 adapter).
    static (string Subnet, string Gateway) DetectNetwork()
    {
        static bool IPv4(string text) => System.Net.IPAddress.TryParse(text, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        var adapter = NetworkDiscovery.Capture().FirstOrDefault(a => a.Physical && a.Active && a.Addresses.Any(IPv4))
            ?? throw new InvalidOperationException("Es wurde kein aktives physisches Netzwerk gefunden.");
        var parts = adapter.Addresses.First(IPv4).Split('.');
        return ($"{parts[0]}.{parts[1]}.{parts[2]}.0/24", adapter.Gateways.FirstOrDefault(IPv4) ?? "");
    }
    void AddLocation((string Subnet, string Gateway)? detected)
    {
        using var dialog = new EntryDialog("Standort hinzufügen", ["Name", "Netzwerk (CIDR, z. B. 192.168.1.0/24)", "Gateway (optional)", "Adapter-ID (optional)"],
            ["", detected?.Subnet ?? "", detected?.Gateway ?? "", ""]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (dialog.Values[0].Length == 0) throw new InvalidDataException("Bitte einen Namen für den Standort eingeben.");
        var next = Copy();
        next.Locations.Add(new() { Name = dialog.Values[0], Subnet = dialog.Values[1], Gateway = Blank(dialog.Values[2]), AdapterId = Blank(dialog.Values[3]) });
        Commit(next); ShowPage(page);
    }
    void EditLocation(LocationRule entry)
    {
        using var dialog = new EntryDialog(entry.Name + " – Netzwerkerkennung", ["Netzwerk (CIDR)", "Gateway (optional)", "Adapter-ID (optional)"], [entry.Subnet, entry.Gateway ?? "", entry.AdapterId ?? ""]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var next = Copy(); var rule = next.Locations.Single(r => r.Name == entry.Name);
        rule.Subnet = dialog.Values[0]; rule.Gateway = Blank(dialog.Values[1]); rule.AdapterId = Blank(dialog.Values[2]);
        Commit(next); ShowPage(page);
    }
    void RenameLocation(LocationRule entry)
    {
        using var dialog = new EntryDialog("Standort umbenennen", ["Neuer Name"], [entry.Name]);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var name = dialog.Values[0];
        if (name == entry.Name) return;
        if (name.Length == 0) throw new InvalidDataException("Bitte einen Namen für den Standort eingeben.");
        var next = Copy(); next.Locations.Single(r => r.Name == entry.Name).Name = name;
        Storage.ParseSettings(JsonSerializer.Serialize(next, Storage.Json)); // validate first, then move the saved layout along
        Storage.RenameProfile(entry.Name, name);
        Commit(next); ShowPage(page);
    }
    void DeleteLocation(LocationRule entry)
    {
        var hasLayout = ProfileInfo(entry.Name) != "noch kein Layout";
        if (MessageBox.Show(this, $"Standort „{entry.Name}“ löschen?" + (hasLayout ? " Das dazu gespeicherte Fensterlayout wird ebenfalls gelöscht." : ""), "Standort löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Storage.DeleteProfile(entry.Name);
        var next = Copy(); next.Locations.RemoveAll(r => r.Name == entry.Name);
        Commit(next); ShowPage(page);
    }
    void Locations(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Die aktiven physischen Netzwerkadapter bestimmen den Standort. VPN-Verbindungen bleiben außen vor. Lege hier deine eigenen Standorte an.", muted: true);
        Ui.Add(body, Ui.Actions(Ui.Button("Standort hinzufügen", () => AddLocation(null), true), Ui.Button("Aktuelles Netzwerk übernehmen", () => AddLocation(DetectNetwork()))));
        var current = NetworkDiscovery.Detect(settings.Locations, NetworkDiscovery.Capture());
        if (settings.Locations.Count == 0)
        {
            var none = Ui.Card(body); Ui.Paragraph(none, "Noch kein Standort angelegt", 13.5f, true);
            Ui.Paragraph(none, "Du kannst beliebig viele Standorte anlegen, zum Beispiel Zuhause, zwei Büros oder ein Homeoffice. „Aktuelles Netzwerk übernehmen“ trägt das Netzwerk, in dem du gerade bist, schon ein. Du musst nur noch einen Namen vergeben.", muted: true);
        }
        foreach (var entry in settings.Locations.OrderByDescending(r => r.Name == current).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
            LocationCard(body, entry, current);
        var monitors = Ui.Card(body); Ui.Paragraph(monitors, "Angeschlossene Monitore", 13.5f, true);
        foreach (var monitor in WindowLayouts.Monitors()) Ui.Paragraph(monitors, $"{monitor.Device} · {monitor.Bounds.Width} × {monitor.Bounds.Height} · {monitor.Dpi * 100 / 96}%" + (monitor.Primary ? " · Hauptmonitor" : ""));
    }
    static bool IsSettingsPage(string name) => name is "Einstellungen" or "Lizenz" or "Erweitert" or "Addons" or "Info" || name.StartsWith("Addon|", StringComparison.Ordinal);

    // Einstellungen has tabs: general settings, the tabs of installed addons, Addons, license and advanced (technical) options.
    void SettingsTabs(TableLayoutPanel body, string current)
    {
        // The settings of an addon are opened from the Addons tab, so that tab stays highlighted while one is shown.
        var tabs = new List<(string Title, string Page)> { ("Allgemein", "Einstellungen"), ("Addons", "Addons"), ("Lizenz", "Lizenz"), ("Erweitert", "Erweitert"), ("Info", "Info") };
        var buttons = tabs.Select(tab =>
        {
            var button = Ui.Button(tab.Title, () => ShowPage(tab.Page));
            button.Tag = tab.Page == current || (tab.Page == "Addons" && current.StartsWith("Addon|", StringComparison.Ordinal)) ? "navActive" : "nav"; button.Margin = new Padding(0, 0, 6, 0); button.Padding = new Padding(13, 8, 13, 8);
            return (Button)button;
        }).ToArray();
        Ui.Add(body, Ui.Actions(buttons));
    }
    void AddonPage(TableLayoutPanel body, string name)
    {
        var parts = name.Split('|', 3);
        var match = AddonManager.Current?.Tabs().FirstOrDefault(x => x.Addon.Id == parts[1] && x.Tab.Title == parts[2]);
        if (match is null || match.Value.Tab is null) { var gone = Ui.Card(body); Ui.Paragraph(gone, "Dieses Addon ist nicht aktiv.", muted: true); return; }
        Ui.Add(body, Ui.Actions(Ui.Button("Zurück zu Addons", () => ShowPage("Addons"))));
        Ui.Paragraph(body, match.Value.Addon.Name, 16, true);
        try { match.Value.Tab.Build(body, new AddonTabContext(this, () => ShowPage(page), tab => ShowPage($"Addon|{parts[1]}|{tab}"))); }
        catch (Exception e) when (e is not OutOfMemoryException) { var error = Ui.Card(body); Ui.Paragraph(error, "Die Seite des Addons konnte nicht angezeigt werden.", 13.5f, true); Ui.Paragraph(error, e.Message, muted: true); }
    }
    void AddonSidebarPage(TableLayoutPanel body, string name)
    {
        var parts = name.Split('|', 3);
        var match = AddonManager.Current?.Pages().FirstOrDefault(x => x.Addon.Id == parts[1] && x.Page.Title == parts[2]);
        if (match is null || match.Value.Page is null) { var gone = Ui.Card(body); Ui.Paragraph(gone, "Dieses Addon ist nicht aktiv.", muted: true); return; }
        try { match.Value.Page.Build(body, new AddonTabContext(this, () => ShowPage(page), tab => ShowPage($"Addon|{parts[1]}|{tab}"))); }
        catch (Exception e) when (e is not OutOfMemoryException) { var error = Ui.Card(body); Ui.Paragraph(error, "Die Seite des Addons konnte nicht angezeigt werden.", 13.5f, true); Ui.Paragraph(error, e.Message, muted: true); }
    }
    void AddonsPage(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Addons erweitern WorkspaceManager um zusätzliche Funktionen. Du wählst sie bei der Installation aus und kannst sie hier ein- und ausschalten.", muted: true);
        var list = Ui.Card(body);
        var manager = AddonManager.Current;
        if (manager is null || manager.Installed.Count == 0)
        {
            Ui.Paragraph(list, "Keine Addons installiert", 13.5f, true);
            Ui.Paragraph(list, "Addons gibt es als eigene Installationsdateien (zum Beispiel „Fernwartung“). Nach dem Installieren erscheinen sie hier.", muted: true);
            return;
        }
        Ui.Paragraph(list, "Installierte Addons", 13.5f, true);
        foreach (var item in manager.Installed)
        {
            if (item.Instance is null) { Ui.Paragraph(list, $"{item.Name} konnte nicht geladen werden: {item.Error}", muted: true); continue; }
            var id = item.Id;
            Ui.Option(list, $"{item.Name}  ·  Version {item.Version}", item.Description, !settings.DisabledAddons.Contains(id, StringComparer.OrdinalIgnoreCase), on =>
            {
                var next = Copy(); next.DisabledAddons.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
                if (!on) next.DisabledAddons.Add(id);
                Commit(next); BuildNavigation(); ShowPage(page);
            });
            // The addon's own settings: only for addons that are switched on (a switched-off addon is not running).
            var tabs = manager.Tabs().Where(x => x.Addon.Id == id).ToList();
            if (tabs.Count > 0)
                Ui.Add(list, Ui.Actions([.. tabs.Select(x => Ui.Button(tabs.Count == 1 ? "Einstellungen öffnen" : x.Tab.Title, () => ShowPage($"Addon|{id}|{x.Tab.Title}")))]));
        }
    }
    // Who wrote the program, how to reach them and under which terms it can be used.
    void InfoPage(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Über WorkspaceManager und wie du den Entwickler erreichst.", muted: true);
        var version = typeof(SettingsForm).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        var about = Ui.Card(body); Ui.Paragraph(about, "WorkspaceManager", 13.5f, true);
        Ui.Paragraph(about, $"Version {version}");
        Ui.Paragraph(about, "Fensterlayouts pro Standort, Programmstarter und Logins für Windows.", muted: true);

        var contact = Ui.Card(body); Ui.Paragraph(contact, "Kontakt", 13.5f, true);
        Ui.Paragraph(contact, "Andre Christians", 10.5f, true);
        Ui.Selectable(contact, "E-Mail: anchristians@gmx.de\r\nGitHub: anchristians92", 10);
        Ui.Add(contact, Ui.Actions(
            Ui.Button("E-Mail schreiben", () => OpenLink("mailto:anchristians@gmx.de?subject=WorkspaceManager"), true),
            Ui.Button("GitHub öffnen", () => OpenLink("https://github.com/anchristians92/Workspace-Manager")),
            Ui.CopyButton("Kopieren", () => "anchristians@gmx.de")));

        var terms = Ui.Card(body); Ui.Paragraph(terms, "Nutzung", 13.5f, true);
        Ui.Paragraph(terms, "Private Nutzung frei und erweiterbar. Kommerzielle Nutzung und Erweiterung nur mit Lizenz.");
        Ui.Paragraph(terms, "Die genauen Bedingungen stehen in der Lizenzdatei und in COMMERCIAL.md im Projekt auf GitHub. Eine Lizenz für den Einsatz in einer Firma gibt es auf Anfrage.", muted: true);
        Ui.Add(terms, Ui.Actions(Ui.Button("Lizenz verwalten", () => ShowPage("Lizenz"))));
    }
    // Only fixed web and mail addresses of this program are opened here.
    static void OpenLink(string address) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address) { UseShellExecute = true });
    void LicensePage(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Privat ist WorkspaceManager kostenlos. Für gewerbliche Nutzung ist eine schriftlich vereinbarte Lizenz nötig.", muted: true);
        var state = LicenseService.State;
        var status = Ui.Card(body); Ui.Paragraph(status, state.Headline, 13.5f, true); Ui.Paragraph(status, state.Detail, muted: true);

        var usage = Ui.Card(body); Ui.Paragraph(usage, "Nutzungsart", 13.5f, true);
        if (LicenseService.IsManagedDevice)
        {
            Ui.Paragraph(usage, "Kommerziell", 11, true);
            Ui.Paragraph(usage, "Dieser Computer ist Mitglied einer Domäne oder hat eine Firmen-Anmeldung (Microsoft Entra ID). Dafür ist nur die kommerzielle Nutzung vorgesehen. Es gibt 30 Tage zum Testen, danach wird ein Lizenzschlüssel benötigt. Den bekommst du auf Anfrage und gegen eine Spende.", muted: true);
        }
        else
        {
            var mode = new ThemeDropdown(); mode.Items.AddRange(["Privat / nicht-kommerziell", "Kommerziell (Firma, Selbstständige, Erwerbszweck)"]);
            mode.SelectedIndex = LicenseService.Current.Mode == "Commercial" ? 1 : 0;
            mode.SelectedIndexChanged += (_, _) => Safe(() =>
            {
                LicenseService.Update(f =>
                {
                    f.Mode = mode.SelectedIndex == 1 ? "Commercial" : "Private";
                    if (f.Mode == "Commercial" && f.TrialStart is null) f.TrialStart = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                });
                Commit(Copy()); ShowPage("Lizenz");
            });
            Ui.Field(usage, "Wie nutzt du WorkspaceManager?", mode);
            Ui.Paragraph(usage, "Kommerziell heißt: Nutzung in einem Unternehmen oder zum Gelderwerb. Es gibt 30 Tage zum Testen, danach wird ein Lizenzschlüssel benötigt. Den bekommst du auf Anfrage und gegen eine Spende.", muted: true);
        }

        if (LicenseService.Current.Mode == "Commercial")
        {
            var entry = Ui.Card(body); Ui.Paragraph(entry, "Lizenzschlüssel", 13.5f, true);
            var box = new TextBox { Text = LicenseService.Current.Key };
            var save = Ui.Button("Schlüssel prüfen und speichern", () =>
            {
                var result = LicenseToken.Verify(box.Text, DateOnly.FromDateTime(DateTime.Today));
                if (result.Check != LicenseCheck.Valid) throw new InvalidDataException(LicenseService.Describe(result.Check, result.Payload));
                LicenseService.Update(f => f.Key = new string(box.Text.Where(c => !char.IsWhiteSpace(c)).ToArray()));
                Commit(Copy()); ShowPage("Lizenz");
            }, true);
            Ui.Field(entry, "Schlüssel (beginnt mit WSM1.)", box);
            Ui.Add(entry, Ui.Actions(save));
        }
    }
    void Preferences(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Darstellung und Verhalten des Workspace Managers.", muted: true);
        var look = Ui.Card(body); Ui.Paragraph(look, "Darstellung", 13.5f, true);
        var theme = new ThemeDropdown(); theme.Items.AddRange(["Wie Windows (automatisch)", "Hell", "Dunkel"]);
        theme.SelectedIndex = settings.ThemeMode switch { "Light" => 1, "Dark" => 2, _ => 0 };
        theme.SelectedIndexChanged += (_, _) => Safe(() => { var next = Copy(); next.ThemeMode = theme.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" }; Commit(next); });
        Ui.Field(look, "Farbschema", theme);
        Ui.Paragraph(look, "Bei „Wie Windows“ folgt die App deinem Windows-Design. Hell oder Dunkel gilt nur für den Workspace Manager, nicht für Windows.", muted: true);
        var start = Ui.Card(body); Ui.Paragraph(start, "Start und Automatik", 13.5f, true);
        Ui.Option(start, "Workspace Manager mit Windows starten", "Startet nach der Anmeldung im Infobereich neben der Uhr.", AutoStart.Enabled, on => AutoStart.Set(on));
        Ui.Option(start, "Layout-Programme beim Windows-Start öffnen", "Öffnet nach der Anmeldung die Programme, die im Layout des erkannten Standorts gespeichert sind, und setzt sie an ihren Platz.", settings.AutoOpenLayoutApps, on => { var next = Copy(); next.AutoOpenLayoutApps = on; Commit(next); });
        Ui.Option(start, "Fensterlayouts automatisch wiederherstellen", "Ordnet geöffnete Fenster neu an, sobald Standort und Monitore zu einem gespeicherten Layout passen. Wechselst du den Ort oder schließt den Dock an, werden fehlende Programme des Layouts dabei auch geöffnet.", settings.AutoRestore, on => { var next = Copy(); next.AutoRestore = on; Commit(next); });
        var data = Ui.Card(body); Ui.Paragraph(data, "Daten", 13.5f, true); Ui.Selectable(data, "Einstellungen und Layouts liegen in: " + Storage.Root);
        Ui.Add(data, Ui.Actions(Ui.Button("Datenordner öffnen", () => { Directory.CreateDirectory(Storage.Root); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + Storage.Root + "\"") { UseShellExecute = true }); })));
    }
    void Advanced(TableLayoutPanel body)
    {
        Ui.Paragraph(body, "Technische Details und direkte Konfiguration. Im Alltag brauchst du diese Ansicht nicht.", muted: true);
        var card = Ui.Card(body); Ui.Paragraph(card, "Konfiguration", 13.5f, true); Ui.Paragraph(card, "Keine Benutzernamen oder Passwörter hier eintragen. Die normalen Eingabemasken findest du unter Programme und Logins.", muted: true);
        var editor = new WheelPassTextBox { Multiline = true, AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Both, WordWrap = false, Height = 320, Font = new Font("Consolas", 11), Text = JsonSerializer.Serialize(settings, Storage.Json), Margin = new Padding(0, 0, 0, 16) }; Ui.Add(card, editor);
        Ui.Add(card, Ui.Actions(Ui.Button("Konfiguration prüfen und speichern", () => { Commit(Storage.ParseSettings(editor.Text)); MessageBox.Show(this, "Konfiguration gespeichert."); }, true)));
        var diagnostics = Ui.Card(body); Ui.Paragraph(diagnostics, "Netzwerkdiagnose", 13.5f, true);
        Ui.Paragraph(diagnostics, "Text lässt sich markieren und kopieren. Mit den Buttons kopierst du die Adapter-ID direkt für die Standort-Erkennung.", muted: true);
        foreach (var adapter in NetworkDiscovery.Capture())
        {
            Ui.Paragraph(diagnostics, $"{adapter.Name} · {(adapter.Physical ? "Physisch" : "Virtuell / sonstiger Adapter")} · {(adapter.Active ? "Verbunden" : "Inaktiv")}", 10.5f, true);
            Ui.Selectable(diagnostics, $"{string.Join(", ", adapter.Addresses)}\nAdapter-ID: {adapter.Id}");
            Ui.Add(diagnostics, Ui.Actions(Ui.CopyButton("Adapter-ID kopieren", () => adapter.Id), Ui.CopyButton("IP-Adressen kopieren", () => string.Join(", ", adapter.Addresses))));
        }
        Ui.Selectable(body, "Datenordner: " + Storage.Root);
    }
    internal static void Safe(Action action) { try { action(); } catch (Exception e) { MessageBox.Show(e.Message, "Workspace Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    protected override void Dispose(bool disposing) { if (disposing) { slide.Dispose(); tips.Dispose(); timer.Dispose(); status.Dispose(); feedback.Dispose(); } base.Dispose(disposing); }
}

internal sealed class EntryDialog : Form
{
    readonly List<TextBox> fields = [];
    readonly CheckBox restore = new() { AutoSize = true, Text = "Gespeicherte Fensterposition verwenden" };
    public string[] Values => fields.Select(f => f.Text.Trim()).ToArray();
    public bool RestorePosition => restore.Checked;
    readonly List<CheckBox> locationBoxes = [];
    public IEnumerable<string> SelectedLocations => locationBoxes.Where(b => b.Checked).Select(b => b.Text);
    readonly ThemeDropdown startMode = new();
    readonly ThemeDropdown startLogin = new();
    readonly IReadOnlyList<LoginEntry> startLogins = [];
    public string RunAs => startMode.SelectedIndex switch { 1 => "Admin", 2 => "User", _ => "" };
    public string RunAsLoginId => RunAs == "User" && startLogin.SelectedIndex >= 0 ? startLogins[startLogin.SelectedIndex].Id : "";
    public EntryDialog(string title, string[] labels, string[] values, bool? restorePosition = null, int hotkeyField = -1, int browseField = -1, string[]? locations = null, IEnumerable<string>? selectedLocations = null, int secretField = -1,
        IReadOnlyList<LoginEntry>? logins = null, string runAs = "", string runAsLoginId = "")
    {
        Ui.Prepare(this, title, new Size(620, 600)); MinimumSize = new Size(520, 480);
        var panel = Ui.ScrollContent(this); Ui.Paragraph(panel, title, 20, true);
        for (var i = 0; i < labels.Length; i++) { var field = i == hotkeyField ? new HotkeyBox { Text = values[i] } : new TextBox { Text = values[i] }; fields.Add(field); if (i == secretField) field.UseSystemPasswordChar = true;
            Button? browse = i == secretField ? Ui.Reveal(field) : i != browseField ? null : Ui.Button("Durchsuchen …", () => { using var picker = new OpenFileDialog { Title = "Programm oder Datei auswählen", Filter = Ui.LaunchFilter }; if (picker.ShowDialog(this) == DialogResult.OK) field.Text = picker.FileName; });
            Ui.Field(panel, labels[i], field, browse); }
        if (logins is not null)
        {
            startLogins = logins;
            startMode.Items.AddRange(["Normal", "Als Administrator (Windows fragt jedes Mal nach)", "Als anderer Benutzer (mit einem gespeicherten Login)"]);
            startMode.SelectedIndex = runAs switch { "Admin" => 1, "User" => 2, _ => 0 };
            Ui.Field(panel, "Starten", startMode);
            var loginHost = new SmoothStack { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            startLogin.Items.AddRange(logins.Select(l => l.Name).ToArray());
            startLogin.SelectedIndex = Math.Max(0, logins.ToList().FindIndex(l => l.Id == runAsLoginId));
            Ui.Field(loginHost, logins.Count == 0 ? "Login (noch keiner angelegt, das geht unter Logins)" : "Mit diesem Login starten", startLogin); Ui.Add(panel, loginHost);
            var hint = Ui.Text("Windows erlaubt es nicht, die Fenster eines Programms mit Administratorrechten zu verschieben. Das Programm startet, die gespeicherte Position wird dort aber nicht angewendet.", 9.5f, muted: true);
            var hintHost = new SmoothStack { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink }; Ui.Add(hintHost, hint); Ui.Add(panel, hintHost);
            void Show() { loginHost.Visible = startMode.SelectedIndex == 2; hintHost.Visible = startMode.SelectedIndex == 1; }
            startMode.SelectedIndexChanged += (_, _) => Show(); Show();
        }
        if (restorePosition.HasValue) { restore.Checked = restorePosition.Value; restore.Margin = new Padding(0, 0, 0, 20); Ui.Add(panel, restore); }
        if (locations is { Length: > 0 })
        {
            Ui.Paragraph(panel, "Nach dem Windows-Start automatisch öffnen an Standort", bold: true);
            Ui.Paragraph(panel, "Das Programm startet nach der Anmeldung und wird an die gespeicherte Position gesetzt. Beim Herunterfahren schließt Windows alle Programme selbst.", 9.5f, muted: true);
            foreach (var location in locations)
            {
                var box = new CheckBox { Text = location, AutoSize = true, Checked = selectedLocations?.Contains(location, StringComparer.OrdinalIgnoreCase) ?? false, Margin = new Padding(0, 0, 0, 8) };
                locationBoxes.Add(box); Ui.Add(panel, box);
            }
            locationBoxes[^1].Margin = new Padding(0, 0, 0, 20);
        }
        var ok = Ui.Button("Speichern", () =>
        {
            if (RunAs == "User" && RunAsLoginId.Length == 0) throw new InvalidDataException("Bitte lege zuerst unter Logins einen Login an und wähle ihn hier aus.");
            DialogResult = DialogResult.OK;
        }, true); var cancel = Ui.Button("Abbrechen", () => DialogResult = DialogResult.Cancel);
        Ui.Add(panel, Ui.Actions(ok, cancel)); AcceptButton = ok; CancelButton = cancel;
        Ui.Finish(this);
    }
}

internal sealed class LoginForm : Form
{
    readonly ThemeDropdown selection = new();
    readonly TextBox hotkey = new HotkeyBox();
    readonly TextBox name = new(), executable = new(), title = new(), username = new(), password = new() { UseSystemPasswordChar = true };
    readonly ThemeDropdown mode = new();
    readonly Settings settings;
    readonly Action<Settings> persist;
    LoginEntry? selected;
    public LoginForm(Settings settings, Action<Settings> persist, LoginEntry? preselect = null)
    {
        this.settings = settings; this.persist = persist;
        Ui.Prepare(this, "Login verwalten", new Size(680, 760)); MinimumSize = new Size(560, 560);
        var panel = Ui.ScrollContent(this); Ui.Paragraph(panel, "Login", 22, true);
        Ui.Paragraph(panel, "Benutzername und Passwort werden im Windows-Tresor gespeichert.", muted: true);
        selection.Items.Add("Neuen Login anlegen"); foreach (var entry in settings.Logins) selection.Items.Add(entry.Name); Ui.Field(panel, "Login auswählen", selection);
        Ui.Field(panel, "Name", name); Ui.Field(panel, "Tastenkürzel (ins Feld klicken, dann Tasten drücken)", hotkey); Ui.Field(panel, "Erlaubtes Programm (optional, leer = jedes Fenster)", executable, Ui.Button("Durchsuchen …", () => { using var picker = new OpenFileDialog { Filter = "Programme|*.exe" }; if (picker.ShowDialog(this) == DialogResult.OK) executable.Text = picker.FileName; }));
        Ui.Field(panel, "Fenstertitel enthält (optional)", title);
        mode.Items.AddRange(["Benutzername und Passwort", "Nur Benutzername", "Nur Passwort"]); mode.SelectedIndex = 0; Ui.Field(panel, "Was soll eingegeben werden?", mode);
        Ui.Field(panel, "Benutzername", username); Button? reveal = null;
        reveal = Ui.Button("Anzeigen", () => { password.UseSystemPasswordChar = !password.UseSystemPasswordChar; reveal!.Text = password.UseSystemPasswordChar ? "Anzeigen" : "Verbergen"; });
        Ui.Field(panel, "Passwort", password, reveal);
        Ui.Paragraph(panel, "Beim Bearbeiten bleiben Benutzername und Passwort unverändert, wenn du die Felder leer lässt. Vor dem Hotkey das Zielfeld anklicken. Ohne festgelegtes Programm wird in das gerade aktive Fenster getippt, prüfe es also vorher. Kein automatisches Enter.", muted: true);
        Ui.Add(panel, Ui.Actions(Ui.Button("Login speichern", Save, true), Ui.Button("Login löschen", Delete)));
        selection.SelectedIndexChanged += (_, _) => LoadSelection(); selection.SelectedIndex = 0;
        if (preselect is not null) { var index = settings.Logins.FindIndex(l => l.Id == preselect.Id); if (index >= 0) selection.SelectedIndex = index + 1; }
        Ui.Finish(this);
    }
    void LoadSelection()
    {
        selected = selection.SelectedIndex > 0 ? settings.Logins[selection.SelectedIndex - 1] : null;
        name.Text = selected?.Name ?? ""; hotkey.Text = Ui.KeyText(selected?.Hotkey ?? "Strg+Alt+1"); executable.Text = selected?.AllowedExecutable ?? ""; title.Text = selected?.TitleContains ?? ""; mode.SelectedIndex = selected?.Mode switch { "Username" => 1, "Password" => 2, _ => 0 };
        username.Clear(); password.Clear();
    }
    void Save()
    {
        var needUser = mode.SelectedIndex is 0 or 1; var needPassword = mode.SelectedIndex is 0 or 2;
        if (selected is null && ((needUser && username.Text.Length == 0) || (needPassword && password.Text.Length == 0))) throw new InvalidDataException("Bitte Benutzername und Passwort eingeben, passend zur gewählten Eingabe.");
        var entry = new LoginEntry { Id = selected?.Id ?? Guid.NewGuid().ToString("N"), Name = name.Text, Hotkey = hotkey.Text, AllowedExecutable = executable.Text, TitleContains = title.Text, Mode = mode.SelectedIndex switch { 1 => "Username", 2 => "Password", _ => "Both" } };
        var next = Storage.ParseSettings(JsonSerializer.Serialize(settings, Storage.Json));
        next.Logins.RemoveAll(l => l.Id == entry.Id); next.Logins.Add(entry);
        Storage.ParseSettings(JsonSerializer.Serialize(next, Storage.Json));
        if (selected is null || username.Text.Length > 0 || password.Text.Length > 0)
        {
            // Editing: empty fields keep the stored value.
            var user = username.Text; var secret = password.Text;
            if (selected is not null && (user.Length == 0 || secret.Length == 0))
            {
                var existing = Credentials.Read(entry.Id);
                if (user.Length == 0) user = existing.Username;
                if (secret.Length == 0) secret = new string(existing.Password);
                Array.Clear(existing.Password);
            }
            Credentials.Write(entry.Id, user, secret);
        }
        password.Clear(); username.Clear();
        persist(next); Close();
    }
    void Delete()
    {
        if (selected is null) return;
        var next = Storage.ParseSettings(JsonSerializer.Serialize(settings, Storage.Json)); next.Logins.RemoveAll(l => l.Id == selected.Id);
        persist(next); Credentials.Delete(selected.Id); Close();
    }
    protected override void Dispose(bool disposing) { if (disposing) { password.Clear(); username.Clear(); } base.Dispose(disposing); }
}

// Records the key combination the user presses instead of asking for typed text.
internal sealed class HotkeyBox : TextBox
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    public HotkeyBox() { ReadOnly = true; ShortcutsEnabled = false; Cursor = Cursors.Hand; PlaceholderText = "Hier klicken und Tastenkombination drücken"; }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        var win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
        var ctrl = (keyData & Keys.Control) != 0; var alt = (keyData & Keys.Alt) != 0; var shift = (keyData & Keys.Shift) != 0;
        // Without Ctrl, Alt or Win the key keeps its normal job (Tab, Enter, Esc ...).
        if (!ctrl && !alt && !win) return base.ProcessCmdKey(ref msg, keyData);
        // Modifier keys alone: keep waiting for the real key.
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin or Keys.None) return true;
        var parts = new List<string>();
        if (ctrl) parts.Add("Strg"); if (alt) parts.Add("Alt"); if (shift) parts.Add("Umschalt"); if (win) parts.Add("Win");
        parts.Add(key is >= Keys.D0 and <= Keys.D9 ? ((char)('0' + (key - Keys.D0))).ToString() : key.ToString());
        Text = string.Join("+", parts); return true;
    }
}
