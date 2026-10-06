using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using WorkspaceManager;

namespace LicenseGenerator;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
#pragma warning disable WFO5001 // dark mode for the standard controls
        Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
        Application.Run(new MainForm());
    }
}

// Small desktop tool for the publisher: enter the customer, click "Erstellen", get the license key.
// Every license is written to issued-licenses.csv next to the private key (who, for whom, when).
internal sealed class MainForm : Form
{
    static readonly string KeyFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WorkspaceManager-Lizenzen");
    static string DefaultKey => Path.Combine(KeyFolder, "license-private.key");
    static readonly Color Muted = Color.FromArgb(150, 160, 175);

    readonly TextBox keyPath = new() { Dock = DockStyle.Fill };
    readonly TextBox licensee = new() { Dock = DockStyle.Fill };
    readonly TextBox note = new() { Dock = DockStyle.Fill, PlaceholderText = "Ansprechpartner, Rechnung oder Spende, Bestellnummer …" };
    readonly NumericUpDown seats = new() { Minimum = 1, Maximum = 100000, Value = 5, Width = 90 };
    readonly RadioButton perpetual = new() { Text = "Unbefristet", Checked = true, AutoSize = true };
    readonly RadioButton until = new() { Text = "Gültig bis", AutoSize = true };
    readonly DateTimePicker untilDate = new() { Format = DateTimePickerFormat.Short, MinDate = DateTime.Today.AddDays(1), Value = DateTime.Today.AddYears(1), Enabled = false, Width = 130 };
    readonly TextBox result = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9.5f) };
    readonly Label status = new() { AutoSize = true, ForeColor = Muted };
    readonly ListView history = new() { View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, Dock = DockStyle.Fill };
    List<IssuedLicense> issued = [];

    public MainForm()
    {
        Text = "WorkspaceManager Lizenzgenerator";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 780);
        MinimumSize = new Size(760, 700);
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!); } catch (Exception e) when (e is ArgumentException or IOException) { }

        keyPath.Text = File.Exists(DefaultKey) ? DefaultKey : "";
        keyPath.Leave += (_, _) => LoadHistory();
        until.CheckedChanged += (_, _) => untilDate.Enabled = until.Checked;

        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(16, 16, 16, 0) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void Row(string label, Control control, Control? extra = null)
        {
            var row = form.RowCount++;
            form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 14, 8) }, 0, row);
            form.Controls.Add(control, 1, row);
            if (extra is not null) form.Controls.Add(extra, 2, row);
            else form.SetColumnSpan(control, 2);
            control.Margin = new Padding(0, 4, 0, 4);
        }

        var browse = new Button { Text = "Durchsuchen …", AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Title = "Privaten Schlüssel auswählen", Filter = "Privater Schlüssel (*.key)|*.key|Alle Dateien|*.*", InitialDirectory = Directory.Exists(KeyFolder) ? KeyFolder : "" };
            if (picker.ShowDialog(this) == DialogResult.OK) { keyPath.Text = picker.FileName; LoadHistory(); }
        };
        Row("Privater Schlüssel", keyPath, browse);
        Row("Lizenznehmer (Firma)", licensee);
        Row("Notiz (nur im Protokoll)", note);

        var seatsPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        seatsPanel.Controls.Add(seats);
        seatsPanel.Controls.Add(new Label { Text = "Geräte (vereinbarte Obergrenze)", AutoSize = true, ForeColor = Muted, Margin = new Padding(10, 6, 0, 0) });
        Row("Anzahl Geräte", seatsPanel);

        var validity = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        validity.Controls.AddRange([perpetual, until, untilDate]);
        until.Margin = new Padding(16, 3, 6, 3);
        Row("Gültigkeit", validity);

        var create = new Button { Text = "Erstellen", AutoSize = true, Padding = new Padding(20, 6, 20, 6), Margin = new Padding(0, 14, 0, 0) };
        create.Click += (_, _) => Create();
        var copy = new Button { Text = "Schlüssel kopieren", AutoSize = true, Padding = new Padding(14, 6, 14, 6), Margin = new Padding(10, 14, 0, 0) };
        copy.Click += (_, _) => CopyKey(result.Text);
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        buttons.Controls.AddRange([create, copy]);
        Row("", buttons);

        var resultBox = new GroupBox { Text = "Lizenzschlüssel", Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12) };
        resultBox.Controls.Add(result);
        var resultHost = new Panel { Dock = DockStyle.Top, Height = 160, Padding = new Padding(16, 8, 16, 0) };
        resultHost.Controls.Add(resultBox);

        var statusHost = new Panel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(16, 8, 16, 0) };
        statusHost.Controls.Add(status);

        int Px(int value) => (int)(value * DeviceDpi / 96f);
        history.Columns.Add("Erstellt", Px(150)); history.Columns.Add("Lizenznehmer", Px(190)); history.Columns.Add("Geräte", Px(70));
        history.Columns.Add("Gültig bis", Px(100)); history.Columns.Add("Status", Px(100)); history.Columns.Add("Erstellt von", Px(110)); history.Columns.Add("Notiz", Px(300));
        history.DoubleClick += (_, _) => { if (history.SelectedIndices.Count == 1) { result.Text = issued[history.SelectedIndices[0]].Key; CopyKey(result.Text); } };

        var openLog = new Button { Text = "Protokoll öffnen", AutoSize = true, Dock = DockStyle.Bottom, Margin = Padding.Empty };
        openLog.Click += (_, _) => OpenLog();
        var historyBox = new GroupBox { Text = "Bisher ausgestellt (Doppelklick kopiert den Schlüssel)", Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 12) };
        var historyInner = new Panel { Dock = DockStyle.Fill };
        historyInner.Controls.Add(history);
        var logBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        logBar.Controls.Add(openLog); openLog.Dock = DockStyle.None;
        historyBox.Controls.Add(historyInner); historyBox.Controls.Add(logBar);
        var historyHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 16) };
        historyHost.Controls.Add(historyBox);

        Controls.Add(historyHost);
        Controls.Add(statusHost);
        Controls.Add(resultHost);
        Controls.Add(form);
        LoadHistory();
    }

    string? LogPath => File.Exists(keyPath.Text) ? LicenseLog.PathFor(keyPath.Text) : null;

    void Create()
    {
        var name = licensee.Text.Trim();
        if (name.Length == 0) { Warn("Bitte den Namen der Firma eintragen."); return; }
        if (!File.Exists(keyPath.Text)) { Warn("Der private Schlüssel wurde nicht gefunden. Bitte die Datei license-private.key auswählen."); return; }
        using var key = ECDsa.Create();
        try { key.ImportFromPem(File.ReadAllText(keyPath.Text)); }
        catch (Exception e) when (e is CryptographicException or ArgumentException or IOException) { Warn("Die Datei ist kein gültiger privater Schlüssel."); return; }

        if (Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) != LicenseToken.PublicKey)
        {
            Warn("Dieser private Schlüssel passt nicht zu dem im Programm eingebauten öffentlichen Schlüssel. Eine damit erzeugte Lizenz würde im Programm abgelehnt.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var validFrom = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expires = until.Checked ? DateOnly.FromDateTime(untilDate.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
        var payload = new LicensePayload(Guid.NewGuid().ToString("N"), name, (int)seats.Value, validFrom, expires);
        var token = LicenseToken.Sign(payload, key);
        if (LicenseToken.Verify(token, today).Check != LicenseCheck.Valid) { Warn("Der erzeugte Schlüssel besteht die eigene Prüfung nicht. Bitte nicht verwenden."); return; }

        var logged = true;
        try { LicenseLog.Append(LogPath!, LicenseLog.NewEntry(payload.Id, name, note.Text, payload.Seats, validFrom, expires, token)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { logged = false; Warn("Der Schlüssel wurde erzeugt, konnte aber nicht protokolliert werden: " + e.Message); }

        result.Text = token;
        CopyKey(token, logged ? "Schlüssel erstellt, kopiert und im Protokoll festgehalten." : null);
        LoadHistory();
    }

    void CopyKey(string text, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try { Clipboard.SetText(text); status.ForeColor = Color.MediumSeaGreen; status.Text = message ?? "Schlüssel in die Zwischenablage kopiert."; }
        catch (System.Runtime.InteropServices.ExternalException) { status.ForeColor = Color.IndianRed; status.Text = "Die Zwischenablage ist gerade belegt. Bitte den Schlüssel von Hand markieren und kopieren."; }
    }

    void Warn(string text)
    {
        status.ForeColor = Color.IndianRed; status.Text = text;
        MessageBox.Show(this, text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    void OpenLog()
    {
        var log = LogPath;
        if (log is null || !File.Exists(log)) { status.ForeColor = Muted; status.Text = "Es gibt noch kein Protokoll. Es entsteht mit der ersten Lizenz."; return; }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{log}\"") { UseShellExecute = true });
    }

    void LoadHistory()
    {
        history.Items.Clear();
        issued = LogPath is { } log ? LicenseLog.Read(log) : [];
        var today = DateOnly.FromDateTime(DateTime.Today);
        foreach (var entry in issued)
            history.Items.Add(new ListViewItem([entry.Created, entry.Licensee, entry.Seats.ToString(CultureInfo.InvariantCulture), entry.ValidUntil ?? "unbefristet", entry.Status(today), entry.CreatedBy, entry.Note]));
    }
}
