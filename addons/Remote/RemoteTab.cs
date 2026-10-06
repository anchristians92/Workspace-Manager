using WorkspaceManager.Addons;

namespace WorkspaceManager.Addon.Remote;

// The "Fernwartung" tab under Einstellungen: program, shortcut, saved passwords and saved devices.
internal static class RemoteTab
{
    // Which program the tab currently shows (it starts with the default program of the connect window).
    static string shownTool = "";

    public static void Build(RemoteStore store, IAddonHost host, TableLayoutPanel body, AddonTabContext context)
    {
        var settings = store.Settings;
        var active = settings.ActiveTools();
        if (!active.Any(t => t.Id == shownTool)) shownTool = settings.StartTool().Id;
        var tool = RemoteTool.ById(shownTool);
        var tools = settings.For(tool.Id);

        Ui.Paragraph(body, "Verbinde dich per Tastenkürzel mit Rechnern über TeamViewer, AnyDesk oder RustDesk. Geräte und Passwörter speicherst du hier einmal.", muted: true);

        // ---- Which programs ----
        var choice = Ui.Card(body); Ui.Paragraph(choice, "Welche Programme nutzt ihr?", 13.5f, true);
        Ui.Paragraph(choice, "Nur die gewählten Programme erscheinen im Verbindungsfenster und in den Einstellungen. Bei nur einem Programm entfällt die Auswahl ganz.", muted: true);
        foreach (var candidate in RemoteTool.All)
        {
            var current = candidate;
            var path = current.FindExecutable(settings.For(current.Id).Executable);
            Ui.Option(choice, current.Name, path is null ? "Nicht gefunden. Den Pfad kannst du unten angeben." : "Gefunden: " + path, active.Any(t => t.Id == current.Id), on =>
            {
                var chosen = active.Select(t => t.Id).ToList();
                chosen.RemoveAll(id => id == current.Id);
                if (on) chosen.Add(current.Id);
                if (chosen.Count == 0) { context.Refresh(); throw new InvalidDataException("Mindestens ein Programm muss ausgewählt sein."); }
                settings.EnabledTools = [.. RemoteTool.All.Where(t => chosen.Contains(t.Id)).Select(t => t.Id)];
                if (!chosen.Contains(settings.DefaultTool)) settings.DefaultTool = settings.EnabledTools[0];
                store.Save(); context.Refresh();
            });
        }

        // ---- Path of the shown program ----
        var program = Ui.Card(body); Ui.Paragraph(program, active.Count > 1 ? "Programm einrichten" : tool.Name, 13.5f, true);
        if (active.Count > 1)
        {
            var toolBox = new ThemeDropdown(); toolBox.Items.AddRange(active.Select(t => t.Name));
            toolBox.SelectedIndex = Math.Max(0, active.ToList().FindIndex(t => t.Id == tool.Id));
            toolBox.SelectedIndexChanged += (_, _) => SettingsForm.Safe(() =>
            {
                shownTool = active[toolBox.SelectedIndex].Id;
                settings.DefaultTool = shownTool; store.Save(); context.Refresh();
            });
            Ui.Field(program, "Einstellungen für (dieses Programm ist auch die Vorauswahl im Verbindungsfenster)", toolBox);
        }
        var found = tool.FindExecutable(tools.Executable);
        Ui.Paragraph(program, found is null ? $"{tool.Name} wurde nicht gefunden. Wähle die Programmdatei aus." : "Gefunden: " + found, muted: true);
        Ui.Add(program, Ui.Actions(
            Ui.Button("Pfad auswählen …", () =>
            {
                using var picker = new OpenFileDialog { Title = $"{tool.Name} auswählen", Filter = "Programme (*.exe)|*.exe" };
                if (picker.ShowDialog(context.Owner) != DialogResult.OK) return;
                tools.Executable = picker.FileName; store.Save(); context.Refresh();
            }),
            Ui.Button("Automatisch suchen", () => { tools.Executable = null; store.Save(); context.Refresh(); })));

        // ---- Shortcut ----
        var keys = Ui.Card(body); Ui.Paragraph(keys, "Tastenkürzel", 13.5f, true);
        var hotkey = new HotkeyBox { Text = Ui.KeyText(settings.Hotkey) };
        hotkey.TextChanged += (_, _) => SettingsForm.Safe(() =>
        {
            if (hotkey.Text.Length > 0) _ = Hotkey.Parse(hotkey.Text);
            if (hotkey.Text == Ui.KeyText(settings.Hotkey)) return;
            settings.Hotkey = hotkey.Text; store.Save(); host.HotkeysChanged();
        });
        Ui.Field(keys, "Tastenkürzel für „Fernwartung Verbindung herstellen“ (ins Feld klicken, dann Tasten drücken)", hotkey);
        Ui.Add(keys, Ui.Actions(
            Ui.Button("Verbindungsfenster öffnen", () => ConnectForm.ShowSingle(store), true),
            Ui.Button("Kürzel entfernen", () => { hotkey.Text = ""; })));

        // ---- Passwords ----
        var passwords = Ui.Card(body); Ui.Paragraph(passwords, $"Passwörter für {tool.Name}", 13.5f, true);
        if (tools.Passwords.Count == 0)
            Ui.Paragraph(passwords, "Noch kein Passwort gespeichert. Das Passwort, das du als Standard markierst, wird im Verbindungsfenster automatisch verwendet. So gibst du bei euren Clients nur noch die ID oder den Gerätenamen ein.", muted: true);
        foreach (var profile in tools.Passwords.ToList())
            Ui.Add(passwords, Ui.Row(profile.Name + (profile == tools.DefaultPassword ? "  ·  Standard" : ""), "Gespeichert im Windows-Tresor",
                ("Bearbeiten", () => EditPassword(store, tools, profile, context)),
                ("Als Standard verwenden", () => { foreach (var p in tools.Passwords) p.IsDefault = p == profile; store.Save(); context.Refresh(); }),
                ("-", () => { }),
                ("Löschen", () => DeletePassword(store, tools, profile, context))));
        Ui.Add(passwords, Ui.Actions(Ui.Button("Passwort hinzufügen", () => EditPassword(store, tools, null, context), true)));

        // ---- Devices ----
        var devices = Ui.Card(body); Ui.Paragraph(devices, $"Geräte für {tool.Name}", 13.5f, true);
        if (tools.Devices.Count == 0)
            Ui.Paragraph(devices, "Noch kein Gerät gespeichert. Mit einem Gerätenamen musst du im Verbindungsfenster nur noch den Namen tippen.", muted: true);
        foreach (var device in tools.Devices.ToList())
            Ui.Add(devices, Ui.Row(device.Name, $"{(tool.NormalizeId(device.RemoteId) ?? device.RemoteId)}  ·  Passwort: {tools.PasswordFor(device)?.Name ?? "keines"}" + (device.PasswordId is null ? "  (Standard)" : ""),
                ("Bearbeiten", () => EditDevice(store, tool, tools, device, context)),
                ("-", () => { }),
                ("Löschen", () => DeleteDevice(store, tools, device, context))));
        Ui.Add(devices, Ui.Actions(Ui.Button("Gerät hinzufügen", () => EditDevice(store, tool, tools, null, context), true)));

        var note = Ui.Card(body); Ui.Paragraph(note, "Zur Sicherheit", 13.5f, true);
        Ui.Paragraph(note, "Passwörter liegen im Windows-Tresor, nicht in einer Datei des Programms. TeamViewer und RustDesk bekommen das Passwort beim Start als Argument. Für einen Moment ist es dadurch für andere Programme auf diesem Rechner in der Prozessliste sichtbar. AnyDesk bekommt es über die Standardeingabe und ist davon nicht betroffen.", muted: true);
    }

    static void EditPassword(RemoteStore store, ToolSettings tools, PasswordProfile? profile, AddonTabContext context)
    {
        using var dialog = new EntryDialog(profile is null ? "Passwort hinzufügen" : "Passwort bearbeiten",
            ["Name (zum Beispiel Lokale Clients)", profile is null ? "Passwort" : "Passwort (leer lassen = unverändert)"], [profile?.Name ?? "", ""], secretField: 1);
        if (dialog.ShowDialog(context.Owner) != DialogResult.OK) return;
        var name = dialog.Values[0]; var secret = dialog.Values[1];
        if (name.Length == 0) throw new InvalidDataException("Bitte einen Namen eingeben.");
        if (tools.Passwords.Any(p => p != profile && string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase))) throw new InvalidDataException("Diesen Namen gibt es schon.");
        if (profile is null)
        {
            if (secret.Length == 0) throw new InvalidDataException("Bitte ein Passwort eingeben.");
            profile = new PasswordProfile { Name = name, IsDefault = tools.Passwords.Count == 0 };
            store.WritePassword(profile, secret);
            tools.Passwords.Add(profile);
        }
        else
        {
            profile.Name = name;
            if (secret.Length > 0) store.WritePassword(profile, secret);
        }
        store.Save(); context.Refresh();
    }

    static void DeletePassword(RemoteStore store, ToolSettings tools, PasswordProfile profile, AddonTabContext context)
    {
        var used = tools.Devices.Count(d => d.PasswordId == profile.Id);
        var text = $"Passwort „{profile.Name}“ löschen?" + (used > 0 ? $" {used} Gerät(e) verwenden danach wieder das Standard-Passwort." : "");
        if (MessageBox.Show(context.Owner, text, "Passwort löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        store.DeletePassword(profile);
        tools.Passwords.Remove(profile);
        foreach (var device in tools.Devices.Where(d => d.PasswordId == profile.Id)) device.PasswordId = null;
        if (tools.Passwords.Count > 0 && !tools.Passwords.Any(p => p.IsDefault)) tools.Passwords[0].IsDefault = true;
        store.Save(); context.Refresh();
    }

    static void EditDevice(RemoteStore store, RemoteTool tool, ToolSettings tools, DeviceEntry? device, AddonTabContext context)
    {
        using var dialog = new EntryDialog(device is null ? "Gerät hinzufügen" : "Gerät bearbeiten",
            ["Gerätename", tool.IdLabel, "Passwort (Name, leer = Standard-Passwort)"],
            [device?.Name ?? "", device?.RemoteId ?? "", device?.PasswordId is { } id ? tools.Passwords.FirstOrDefault(p => p.Id == id)?.Name ?? "" : ""]);
        if (dialog.ShowDialog(context.Owner) != DialogResult.OK) return;
        var name = dialog.Values[0];
        if (name.Length == 0) throw new InvalidDataException("Bitte einen Gerätenamen eingeben.");
        if (tools.Devices.Any(d => d != device && string.Equals(d.Name, name, StringComparison.CurrentCultureIgnoreCase))) throw new InvalidDataException("Diesen Gerätenamen gibt es schon.");
        var remoteId = tool.NormalizeId(dialog.Values[1]) ?? throw new InvalidDataException($"Das ist keine gültige Angabe für {tool.Name} ({tool.IdHint}).");
        string? passwordId = null;
        if (dialog.Values[2].Length > 0)
            passwordId = (tools.Passwords.FirstOrDefault(p => string.Equals(p.Name, dialog.Values[2], StringComparison.CurrentCultureIgnoreCase))
                ?? throw new InvalidDataException($"Es gibt kein Passwort mit dem Namen „{dialog.Values[2]}“.")).Id;
        if (device is null) tools.Devices.Add(device = new());
        device.Name = name; device.RemoteId = remoteId; device.PasswordId = passwordId;
        store.Save(); context.Refresh();
    }

    static void DeleteDevice(RemoteStore store, ToolSettings tools, DeviceEntry device, AddonTabContext context)
    {
        if (MessageBox.Show(context.Owner, $"Gerät „{device.Name}“ löschen?", "Gerät löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        tools.Devices.Remove(device);
        store.Save(); context.Refresh();
    }
}
