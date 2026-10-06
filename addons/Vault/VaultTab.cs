using WorkspaceManager.Addons;

namespace WorkspaceManager.Addon.Vault;

// The settings of the addon (Einstellungen → Addons → Passwort-Tresor): which program, server, login, shortcut.
internal static class VaultTab
{
    public static void Build(VaultStore store, VaultService service, IAddonHost host, TableLayoutPanel body, AddonTabContext context)
    {
        var settings = store.Settings;
        var saved = store.ReadLogin();
        Ui.Paragraph(body, "Verbinde WorkspaceManager mit deinem Passwort-Programm. Danach suchst du per Tastenkürzel einen Eintrag und lässt Benutzername und Passwort eintippen.", muted: true);

        // ---- Connection ----
        var connection = Ui.Card(body); Ui.Paragraph(connection, "Verbindung", 13.5f, true);
        var providers = VaultProviders.All;
        var providerBox = new ThemeDropdown(); providerBox.Items.AddRange(providers.Select(p => p.Name));
        providerBox.SelectedIndex = Math.Max(0, providers.ToList().FindIndex(p => p.Id == settings.Provider));
        // With a single supported program there is nothing to choose.
        if (providers.Count > 1) Ui.Field(connection, "Passwort-Programm", providerBox);
        var hint = VaultProviders.Create(providers[Math.Max(0, providerBox.SelectedIndex)].Id).ServerHint;

        var server = new TextBox { Text = settings.Server };
        var user = new TextBox { Text = saved?.Username ?? "" };
        var password = new TextBox { UseSystemPasswordChar = true };
        Ui.Field(connection, $"Serveradresse (zum Beispiel {hint})", server);
        Ui.Field(connection, "Benutzername", user);
        Ui.Field(connection, saved is null ? "Passwort" : "Passwort (leer lassen = unverändert)", password, Ui.Reveal(password));

        var result = Ui.Text(service.Configured ? service.Status : "Noch nicht eingerichtet.", 9.5f, muted: true);
        result.Margin = new Padding(0, 0, 0, 8); Ui.Add(connection, result);

        VaultConnection Collect()
        {
            var secret = password.Text.Length > 0 ? password.Text : saved?.Password ?? "";
            return new VaultConnection(PleasantProvider.NormalizeServer(server.Text), user.Text.Trim(), secret);
        }
        void Report(string text) { if (!result.IsDisposed && result.IsHandleCreated) result.BeginInvoke(() => { if (!result.IsDisposed) result.Text = text; }); }

        Ui.Add(connection, Ui.Actions(
            Ui.Button("Speichern und verbinden", () =>
            {
                var values = Collect();
                if (values.Username.Length == 0) throw new InvalidDataException("Bitte den Benutzernamen eintragen.");
                if (values.Password.Length == 0) throw new InvalidDataException("Bitte das Passwort eintragen.");
                settings.Provider = providers[Math.Max(0, providerBox.SelectedIndex)].Id; settings.Server = values.Server;
                store.WriteLogin(values.Username, values.Password); store.Save();
                result.Text = "Verbinde …";
                _ = Task.Run(async () =>
                {
                    try { await service.ReconnectAsync(); Report(service.Status); }
                    catch (Exception e) when (e is not OutOfMemoryException) { Report(service.Status); }
                });
            }, true),
            Ui.Button("Nur testen", () =>
            {
                var values = Collect(); var id = providers[Math.Max(0, providerBox.SelectedIndex)].Id;
                if (values.Username.Length == 0 || values.Password.Length == 0) throw new InvalidDataException("Bitte Benutzername und Passwort eintragen.");
                result.Text = "Teste …";
                _ = Task.Run(async () =>
                {
                    try { await VaultService.TestAsync(id, values); Report("Anmeldung erfolgreich. Mit „Speichern und verbinden“ übernimmst du die Angaben."); }
                    catch (Exception e) when (e is not OutOfMemoryException) { Report("Fehlgeschlagen: " + e.Message); }
                });
            }),
            Ui.Button("Zugangsdaten löschen", () =>
            {
                if (MessageBox.Show(context.Owner, "Gespeicherten Benutzernamen und das Passwort aus dem Windows-Tresor löschen?", "Zugangsdaten löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                store.DeleteLogin(); settings.Server = ""; store.Save(); service.Reset(); context.Refresh();
            })));

        // ---- Shortcut ----
        var keys = Ui.Card(body); Ui.Paragraph(keys, "Tastenkürzel", 13.5f, true);
        var hotkey = new HotkeyBox { Text = Ui.KeyText(settings.Hotkey) };
        hotkey.TextChanged += (_, _) => SettingsForm.Safe(() =>
        {
            if (hotkey.Text.Length > 0) _ = Hotkey.Parse(hotkey.Text);
            if (hotkey.Text == Ui.KeyText(settings.Hotkey)) return;
            settings.Hotkey = hotkey.Text; store.Save(); host.HotkeysChanged();
        });
        Ui.Field(keys, "Tastenkürzel für „Passwort-Tresor öffnen“ (ins Feld klicken, dann Tasten drücken)", hotkey);
        Ui.Add(keys, Ui.Actions(
            Ui.Button("Fenster öffnen", () => SearchForm.ShowSingle(service, store), true),
            Ui.Button("Kürzel entfernen", () => { hotkey.Text = ""; })));

        // ---- Clipboard ----
        var clipboard = Ui.Card(body); Ui.Paragraph(clipboard, "Zwischenablage", 13.5f, true);
        int[] choices = [10, 20, 30, 60, 120];
        var seconds = new ThemeDropdown(); seconds.Items.AddRange(choices.Select(c => $"{c} Sekunden"));
        seconds.SelectedIndex = Math.Max(0, Array.IndexOf(choices, settings.ClearClipboardSeconds));
        seconds.SelectedIndexChanged += (_, _) => SettingsForm.Safe(() => { settings.ClearClipboardSeconds = choices[Math.Max(0, seconds.SelectedIndex)]; store.Save(); });
        Ui.Field(clipboard, "Kopierte Passwörter aus der Zwischenablage löschen nach", seconds);

        var note = Ui.Card(body); Ui.Paragraph(note, "Zur Sicherheit", 13.5f, true);
        Ui.Paragraph(note, "Benutzername und Passwort für die Anmeldung am Server liegen im Windows-Tresor, nicht in einer Datei des Programms. Die Passwörter deiner Einträge werden nicht gespeichert: Sie werden erst beim Verwenden vom Server geholt und nur kurz im Arbeitsspeicher gehalten. Kopierte Passwörter landen nicht im Zwischenablageverlauf von Windows. Es wird nie automatisch abgeschickt, du drückst selbst Enter.", muted: true);
    }
}
