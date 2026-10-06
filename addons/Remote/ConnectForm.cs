using WorkspaceManager.Addons;

namespace WorkspaceManager.Addon.Remote;

// The connection form: program, ID or device name, which saved password to use (or a one-off one), connect.
// It is used twice: in the window a hotkey opens and on the addon's page in the sidebar.
internal sealed class ConnectPanel
{
    enum ChoiceKind { Saved, Manual, None }
    sealed record Choice(ChoiceKind Kind, PasswordProfile? Profile, string Label);

    readonly RemoteStore store;
    readonly Action? connected;
    readonly IReadOnlyList<RemoteTool> tools;
    readonly ThemeDropdown toolBox = new();
    readonly SmoothStack toolHost = new() { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    readonly ThemeDropdown passwordBox = new();
    readonly TextBox target = new();
    readonly TextBox manual = new() { UseSystemPasswordChar = true };
    readonly SmoothStack manualHost = new() { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    readonly Label info = Ui.Text("", 9.5f, muted: true);
    List<Choice> choices = [];
    bool loading;

    public ThemeButton ConnectButton { get; }
    public ThemeButton? CancelButton { get; }

    // Adds the form to <panel>. With <cancel> a "Abbrechen" button is added as well (window); without it only "Verbinden" (page).
    public ConnectPanel(RemoteStore store, TableLayoutPanel panel, Action? connected, Action? cancel)
    {
        this.store = store; this.connected = connected;
        tools = store.Settings.ActiveTools();

        // With a single program there is nothing to choose, so the selection is not shown at all.
        toolBox.Items.AddRange(tools.Select(t => t.Name));
        Ui.Field(toolHost, "Programm", toolBox); Ui.Add(panel, toolHost); toolHost.Visible = tools.Count > 1;
        target.AutoCompleteMode = AutoCompleteMode.SuggestAppend; target.AutoCompleteSource = AutoCompleteSource.CustomSource;
        Ui.Field(panel, "ID oder Gerätename", target);
        Ui.Field(panel, "Passwort", passwordBox);

        Ui.Field(manualHost, "Eigenes Passwort (nur für diese Verbindung)", manual, Ui.Reveal(manual));
        Ui.Add(panel, manualHost);
        info.Margin = new Padding(0, 0, 0, 16); Ui.Add(panel, info);

        ConnectButton = Ui.Button("Verbinden", Connect, true);
        if (cancel is null) Ui.Add(panel, Ui.Actions(ConnectButton));
        else { CancelButton = Ui.Button("Abbrechen", cancel); Ui.Add(panel, Ui.Actions(ConnectButton, CancelButton)); }

        toolBox.SelectedIndexChanged += (_, _) => { if (!loading) LoadTool(); };
        passwordBox.SelectedIndexChanged += (_, _) => { manualHost.Visible = CurrentChoice.Kind == ChoiceKind.Manual; };
        target.TextChanged += (_, _) => { if (!loading) UpdateInfo(); };

        loading = true;
        toolBox.SelectedIndex = Math.Max(0, tools.ToList().FindIndex(t => t.Id == store.Settings.StartTool().Id));
        loading = false;
        LoadTool();
    }

    public void FocusTarget() => target.Focus();
    public void ClearSecret() => manual.Clear();

    RemoteTool Tool => tools[Math.Clamp(toolBox.SelectedIndex, 0, tools.Count - 1)];
    ToolSettings Settings => store.Settings.For(Tool.Id);
    Choice CurrentChoice => choices.Count == 0 ? new(ChoiceKind.None, null, "") : choices[Math.Clamp(passwordBox.SelectedIndex, 0, choices.Count - 1)];

    void LoadTool()
    {
        loading = true;
        var settings = Settings;
        target.AutoCompleteCustomSource.Clear();
        target.AutoCompleteCustomSource.AddRange([.. settings.Devices.Select(d => d.Name)]);

        choices = [.. settings.Passwords.Select(p => new Choice(ChoiceKind.Saved, p, p.Name + (p == settings.DefaultPassword ? "  (Standard)" : "")))];
        choices.Add(new(ChoiceKind.Manual, null, "Eigenes Passwort eingeben"));
        choices.Add(new(ChoiceKind.None, null, "Ohne Passwort (das Programm fragt selbst)"));
        passwordBox.Items.Clear(); passwordBox.Items.AddRange(choices.Select(c => c.Label));
        passwordBox.SelectedIndex = settings.DefaultPassword is { } standard ? choices.FindIndex(c => c.Profile == standard) : choices.FindIndex(c => c.Kind == ChoiceKind.Manual);
        manualHost.Visible = CurrentChoice.Kind == ChoiceKind.Manual;
        loading = false;
        UpdateInfo();
    }

    // Shows what the typed text means and preselects the password that belongs to a saved device.
    void UpdateInfo()
    {
        var tool = Tool; var settings = Settings;
        var resolved = tool.Resolve(settings.Devices, target.Text);
        if (resolved is null)
        {
            info.Text = target.Text.Trim().Length == 0 ? $"Möglich: {tool.IdHint}." : $"Das ist weder ein gespeicherter Gerätename noch eine gültige Eingabe ({tool.IdHint}).";
            return;
        }
        if (resolved.Device is not { } device) { info.Text = resolved.Id.All(char.IsAsciiDigit) ? $"{tool.Name}-ID {resolved.Id}" : $"Verbindung zu {resolved.Id}"; return; }
        var profile = settings.PasswordFor(device);
        info.Text = $"Gespeichertes Gerät „{device.Name}“ · {resolved.Id}" + (profile is null ? "" : $" · Passwort „{profile.Name}“");
        if (profile is not null)
        {
            var index = choices.FindIndex(c => c.Profile == profile);
            if (index >= 0 && passwordBox.SelectedIndex != index) passwordBox.SelectedIndex = index;
        }
    }

    void Connect()
    {
        var tool = Tool; var settings = Settings;
        var resolved = tool.Resolve(settings.Devices, target.Text)
            ?? throw new InvalidDataException($"Bitte eine gültige Eingabe machen oder einen gespeicherten Gerätenamen verwenden ({tool.IdHint}).");
        var executable = tool.FindExecutable(settings.Executable)
            ?? throw new FileNotFoundException($"{tool.Name} wurde nicht gefunden. Den Pfad stellst du unter Einstellungen → Fernwartung ein.");

        var choice = CurrentChoice;
        string? password = null;
        if (choice.Kind == ChoiceKind.Saved)
            password = store.ReadPassword(choice.Profile!) ?? throw new InvalidOperationException($"Das Passwort „{choice.Profile!.Name}“ konnte nicht aus dem Windows-Tresor gelesen werden. Bitte unter Einstellungen → Fernwartung neu eintragen.");
        else if (choice.Kind == ChoiceKind.Manual)
            password = manual.Text.Length > 0 ? manual.Text : throw new InvalidDataException("Bitte das Passwort eingeben oder „Ohne Passwort“ wählen.");

        tool.Start(executable, resolved.Id, password);
        if (store.Settings.DefaultTool != tool.Id) { store.Settings.DefaultTool = tool.Id; store.Save(); }
        info.Text = $"Verbindung zu {resolved.Id} wird gestartet …";
        manual.Clear();
        connected?.Invoke();
    }
}

// The window a hotkey opens.
internal sealed class ConnectForm : Form
{
    static ConnectForm? open;
    readonly ConnectPanel connect;

    public static void ShowSingle(RemoteStore store)
    {
        if (open is { IsDisposed: false } existing)
        {
            if (existing.WindowState == FormWindowState.Minimized) existing.WindowState = FormWindowState.Normal;
            existing.Activate(); existing.BringToFront(); existing.connect.FocusTarget();
            return;
        }
        open = new ConnectForm(store);
        open.Show(); open.Activate(); open.connect.FocusTarget();
    }

    public static void CloseOpen()
    {
        if (open is { IsDisposed: false } form) form.Close();
        open = null;
    }

    ConnectForm(RemoteStore store)
    {
        Ui.Prepare(this, "Fernwartung Verbindung herstellen", new Size(560, 600)); MinimumSize = new Size(480, 520);
        StartPosition = FormStartPosition.CenterScreen;
        var panel = Ui.ScrollContent(this);
        Ui.Paragraph(panel, "Fernwartung Verbindung herstellen", 18, true);
        Ui.Paragraph(panel, "ID oder Gerätename eingeben. Ist ein Passwort gespeichert, wird es automatisch verwendet.", muted: true);
        connect = new ConnectPanel(store, panel, Close, Close);
        AcceptButton = connect.ConnectButton; CancelButton = connect.CancelButton;
        FormClosed += (_, _) => { connect.ClearSecret(); if (open == this) open = null; };
        Ui.Finish(this);
    }
}
