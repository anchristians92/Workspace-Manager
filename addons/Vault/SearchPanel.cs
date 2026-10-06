using System.Runtime.InteropServices;
using WorkspaceManager.Addons;

namespace WorkspaceManager.Addon.Vault;

// The search: a text field, the hits below it and what to do with the chosen entry.
// It is used twice: in the window a hotkey opens (it can type into the window that was active before) and on the addon's page.
internal sealed class SearchPanel
{
    readonly VaultService service;
    readonly VaultStore store;
    readonly Func<nint>? typeTarget;
    readonly Action? finished;
    readonly TextBox search = new();
    readonly ResultList list = new();
    readonly Label info = Ui.Text("", 9.5f, muted: true);
    readonly TextBox details = new();
    readonly WebLink link = new();
    readonly System.Windows.Forms.Timer debounce = new() { Interval = 300 };
    CancellationTokenSource? running;
    int generation;

    public ThemeButton? TypeButton { get; }

    // <typeTarget> gives the window to type into (null: the panel only copies). <finished> is called after typing or copying (the window closes then).
    public SearchPanel(VaultService service, VaultStore store, TableLayoutPanel panel, Func<nint>? typeTarget, Action? finished)
    {
        this.service = service; this.store = store; this.typeTarget = typeTarget; this.finished = finished;
        Ui.Field(panel, "Suchen (Name, Benutzer oder Adresse)", search);
        info.Margin = new Padding(0, 0, 0, 4); Ui.Add(panel, info);
        list.Height = 230; list.Margin = new Padding(0, 0, 0, 14); Ui.Add(panel, list);

        // The frame of a text field is as high as one line; MinimumSize makes it a box for several lines.
        details.Multiline = true; details.ReadOnly = true; details.ScrollBars = ScrollBars.Vertical; details.TabStop = false;
        Ui.Paragraph(panel, "Website und Bemerkung", bold: true);
        link.Height = (int)(30 * 1.0f); link.Margin = new Padding(0, 0, 0, 8); Ui.Add(panel, link);
        Ui.Add(panel, new InputFrame(details) { MinimumSize = new Size(0, 104), Margin = new Padding(0, 0, 0, 18) });

        var buttons = new List<Button>();
        if (typeTarget is not null) { TypeButton = Ui.Button("Eintippen", () => _ = Run(Type), true); buttons.Add(TypeButton); }
        buttons.Add(Ui.Button("Passwort kopieren", () => _ = Run(CopyPassword), typeTarget is null));
        buttons.Add(Ui.Button("Benutzername kopieren", () => _ = Run(CopyUser)));
        Ui.Add(panel, Ui.Actions([.. buttons]));

        debounce.Tick += (_, _) => { debounce.Stop(); _ = Search(); };
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && list.Count > 0) { list.Focus(); if (list.SelectedIndex < 0) list.SelectedIndex = 0; e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; _ = Run(Default); }
        };
        list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; _ = Run(Default); } };
        list.DoubleClick += (_, _) => _ = Run(Default);
        list.SelectedIndexChanged += (_, _) => { details.Text = list.Selected is { } entry ? Details(entry) : ""; link.Address = list.Selected?.Url ?? ""; };
        link.Click += (_, _) => _ = Run(OpenWebsite);
        panel.Disposed += (_, _) => { debounce.Dispose(); running?.Cancel(); };

        list.EmptyText = service.Configured ? "Tippe oben mindestens zwei Zeichen, um zu suchen." : service.Status;
    }

    public void FocusSearch() { search.Focus(); search.SelectAll(); }

    Task Default() => typeTarget is null ? CopyPassword() : Type();

    async Task Search()
    {
        var mine = ++generation;
        running?.Cancel(); running = new CancellationTokenSource(); var cancel = running.Token;
        var text = search.Text.Trim();
        if (text.Length < 2) { list.SetEntries([]); list.EmptyText = service.Configured ? "Tippe oben mindestens zwei Zeichen, um zu suchen." : service.Status; info.Text = ""; return; }
        info.Text = "Suche …"; list.EmptyText = "Suche …";
        try
        {
            var found = await service.SearchAsync(text, cancel);
            if (mine != generation || list.IsDisposed) return;
            list.SetEntries(found);
            list.EmptyText = "Nichts gefunden."; info.Text = found.Count == 0 ? "" : found.Count == 1 ? "1 Treffer." : $"{found.Count} Treffer. Mit Enter wird der markierte Eintrag verwendet.";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            if (mine == generation && !list.IsDisposed) { list.SetEntries([]); list.EmptyText = e.Message; info.Text = ""; }
        }
    }

    VaultEntry Chosen() => list.Selected ?? (list.Count == 1 ? list.EntryAt(0) : null) ?? throw new InvalidOperationException("Bitte erst einen Eintrag aus der Liste wählen.");

    // Runs an action and shows what went wrong in the info line instead of a message box.
    async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) when (e is not OutOfMemoryException) { if (!info.IsDisposed) info.Text = e.Message; }
    }

    async Task Type()
    {
        var entry = Chosen();
        var target = typeTarget!();
        var password = await service.GetPasswordAsync(entry);
        finished?.Invoke();
        await LoginSender.TypeInto(target, entry.Username, password);
    }

    async Task CopyPassword()
    {
        var entry = Chosen();
        Copy(await service.GetPasswordAsync(entry), store.Settings.ClearClipboardSeconds);
        info.Text = $"Das Passwort von „{entry.Name}“ ist in der Zwischenablage und wird nach {store.Settings.ClearClipboardSeconds} Sekunden gelöscht.";
        finished?.Invoke();
    }

    // Folder and note of an entry as readable text (the website is shown as a link above).
    internal static string Details(VaultEntry entry)
    {
        var lines = new List<string>();
        if (entry.Path.Length > 0) lines.Add("Ordner: " + entry.Path.TrimEnd('/', '\\'));
        if (entry.Notes.Trim().Length > 0) { lines.Add(""); lines.Add("Bemerkung:"); lines.Add(entry.Notes.Trim().ReplaceLineEndings("\r\n")); }
        else lines.Add("Keine Bemerkung hinterlegt.");
        return string.Join("\r\n", lines);
    }

    // Only web addresses are opened, never a file or another kind of link that happens to stand in the field.
    internal static Uri? WebAddress(string url)
    {
        url = url.Trim();
        if (url.Length > 0 && !url.Contains("://", StringComparison.Ordinal) && !url.Contains(' ')) url = "https://" + url;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Length > 0 ? uri : null;
    }

    Task OpenWebsite()
    {
        var entry = Chosen();
        var uri = WebAddress(entry.Url) ?? throw new InvalidOperationException(entry.Url.Length == 0 ? "Dieser Eintrag hat keine Website." : "Das ist keine Web-Adresse, die ich öffnen kann: " + entry.Url);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    Task CopyUser()
    {
        var entry = Chosen();
        if (entry.Username.Length == 0) throw new InvalidOperationException("Dieser Eintrag hat keinen Benutzernamen.");
        Copy(entry.Username, 0);
        info.Text = $"Der Benutzername von „{entry.Name}“ ist in der Zwischenablage.";
        return Task.CompletedTask;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();

    // Puts text on the clipboard, keeps it out of the Windows clipboard history and empties it after <clearAfter> seconds
    // (0: not at all), but only if nothing else was copied in the meantime. The clipboard is not read for that: Windows
    // counts every change, so an unchanged counter means it still holds the password.
    internal static void Copy(string text, int clearAfter)
    {
        var data = new DataObject();
        data.SetText(text);
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(BitConverter.GetBytes(1)));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        Clipboard.SetDataObject(data, true);
        if (clearAfter <= 0) return;
        var counter = GetClipboardSequenceNumber();
        var timer = new System.Windows.Forms.Timer { Interval = clearAfter * 1000 };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            try { if (GetClipboardSequenceNumber() == counter) Clipboard.Clear(); }
            catch (ExternalException) { }
        };
        timer.Start();
    }
}

// The window a hotkey opens.
internal sealed class SearchForm : Form
{
    static SearchForm? open;
    readonly SearchPanel panel;
    nint target;

    public static void ShowSingle(VaultService service, VaultStore store)
    {
        // The window that is in front now is the one the password is typed into.
        var previous = Native.GetForegroundWindow();
        if (open is { IsDisposed: false } existing)
        {
            if (previous != existing.Handle) existing.target = previous;
            if (existing.WindowState == FormWindowState.Minimized) existing.WindowState = FormWindowState.Normal;
            existing.Activate(); existing.BringToFront(); existing.panel.FocusSearch();
            return;
        }
        open = new SearchForm(service, store) { target = previous };
        open.Show(); open.Activate(); open.panel.FocusSearch();
        if (!service.Connected) service.WarmUp();
    }

    public static void CloseOpen()
    {
        if (open is { IsDisposed: false } form) form.Close();
        open = null;
    }

    SearchForm(VaultService service, VaultStore store)
    {
        Ui.Prepare(this, "Passwort-Tresor", new Size(580, 720)); MinimumSize = new Size(480, 560);
        StartPosition = FormStartPosition.CenterScreen; TopMost = true;
        var body = Ui.ScrollContent(this);
        Ui.Paragraph(body, "Passwort-Tresor", 18, true);
        Ui.Paragraph(body, "Eintrag suchen und in das Fenster tippen, das du zuvor geöffnet hattest.", muted: true);
        panel = new SearchPanel(service, store, body, () => target, Close);
        CancelButton = new Button();
        ((Button)CancelButton).Click += (_, _) => Close();
        FormClosed += (_, _) => { if (open == this) open = null; };
        Ui.Finish(this);
        Shown += (_, _) => { var area = Screen.FromControl(this).WorkingArea; if (Height > area.Height) { Height = area.Height; Top = area.Top; } };
    }
}
