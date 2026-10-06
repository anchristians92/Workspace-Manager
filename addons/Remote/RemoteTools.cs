using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WorkspaceManager.Addon.Remote;

internal sealed record ConnectCommand(ProcessStartInfo Start, string? StandardInput);

internal sealed record ResolvedTarget(string Id, DeviceEntry? Device);

// The remote maintenance programs the addon can start: how to find them, which IDs they accept and how to pass ID and password.
internal sealed partial class RemoteTool
{
    public string Id { get; }
    public string Name { get; }
    public string IdHint { get; }
    public string IdLabel { get; }
    readonly string[] defaultPaths;
    readonly Func<string, string?> normalize;

    RemoteTool(string id, string name, string idHint, string idLabel, string[] defaultPaths, Func<string, string?> normalize)
    {
        Id = id; Name = name; IdHint = idHint; IdLabel = idLabel; this.defaultPaths = defaultPaths; this.normalize = normalize;
    }

    // TeamViewer's partner field also takes a DNS name or an IP address (direct connection in the network).
    public static readonly RemoteTool TeamViewer = new("teamviewer", "TeamViewer", "9 bis 10 Ziffern (zum Beispiel 123 456 789), ein DNS-Name (pc01.firma.local) oder eine IP-Adresse",
        "TeamViewer-ID oder DNS-Name / IP-Adresse",
        [@"%ProgramFiles%\TeamViewer\TeamViewer.exe", @"%ProgramFiles(x86)%\TeamViewer\TeamViewer.exe"],
        input => Digits(input, 8, 12) ?? HostName(input));

    public static readonly RemoteTool AnyDesk = new("anydesk", "AnyDesk", "9 bis 10 Ziffern oder ein Alias wie name@ad", "AnyDesk-ID oder Alias",
        [@"%ProgramFiles(x86)%\AnyDesk\AnyDesk.exe", @"%ProgramFiles%\AnyDesk\AnyDesk.exe"],
        input =>
        {
            var text = input.Trim();
            return text.Contains('@') ? (AliasPattern().IsMatch(text) ? text : null) : Digits(text, 8, 12);
        });

    public static readonly RemoteTool RustDesk = new("rustdesk", "RustDesk", "Ziffern oder eine eigene ID (6 bis 32 Zeichen)", "RustDesk-ID",
        [@"%ProgramFiles%\RustDesk\rustdesk.exe", @"%LocalAppData%\RustDesk\rustdesk.exe"],
        input =>
        {
            var text = input.Trim();
            return Digits(text, 6, 12) ?? (CustomIdPattern().IsMatch(text) ? text : null);
        });

    public static readonly IReadOnlyList<RemoteTool> All = [TeamViewer, AnyDesk, RustDesk];

    public static RemoteTool ById(string? id) => All.FirstOrDefault(t => t.Id == id) ?? TeamViewer;

    [GeneratedRegex(@"^[A-Za-z0-9._-]{2,64}@[A-Za-z0-9._-]{1,32}$")] private static partial Regex AliasPattern();
    [GeneratedRegex(@"^(?=.{2,253}$)[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$")] private static partial Regex HostPattern();
    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}$")] private static partial Regex Ipv4Pattern();
    [GeneratedRegex(@"^[A-Za-z0-9_-]{6,32}$")] private static partial Regex CustomIdPattern();

    // Spaces and dashes inside a numeric ID are allowed ("123 456 789"); the result has digits only.
    static string? Digits(string input, int min, int max)
    {
        var compact = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());
        return compact.Length >= min && compact.Length <= max && compact.All(char.IsAsciiDigit) ? compact : null;
    }

    // A DNS name (letters, digits, hyphens, dots) or an IPv4 address. Plain digits that are not a valid ID are rejected on purpose:
    // they are far more likely a typo than a host name.
    static string? HostName(string input)
    {
        var text = input.Trim();
        if (text.All(char.IsAsciiDigit)) return null;
        if (Ipv4Pattern().IsMatch(text)) return System.Net.IPAddress.TryParse(text, out _) ? text : null;
        return HostPattern().IsMatch(text) ? text.ToLowerInvariant() : null;
    }

    public string? NormalizeId(string input) => string.IsNullOrWhiteSpace(input) ? null : normalize(input);

    // The configured path wins, otherwise the usual install locations.
    public string? FindExecutable(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        return defaultPaths.Select(Environment.ExpandEnvironmentVariables).FirstOrDefault(File.Exists);
    }

    // A name that matches a saved device wins over an ID; otherwise the text has to be a valid ID.
    public ResolvedTarget? Resolve(IEnumerable<DeviceEntry> devices, string input)
    {
        var text = input.Trim();
        if (text.Length == 0) return null;
        var list = devices.ToList();
        var normalized = NormalizeId(text);
        var device = list.FirstOrDefault(d => string.Equals(d.Name, text, StringComparison.CurrentCultureIgnoreCase))
            ?? (normalized is null ? null : list.FirstOrDefault(d => NormalizeId(d.RemoteId) == normalized));
        if (device is not null)
        {
            var id = NormalizeId(device.RemoteId);
            return id is null ? null : new(id, device);
        }
        return normalized is null ? null : new(normalized, null);
    }

    // The password goes to the program as a separate argument (or on its standard input for AnyDesk),
    // never glued into a command string, so special characters survive unchanged.
    public ConnectCommand BuildCommand(string executable, string id, string? password)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable) ?? "" };
        string? input = null;
        var hasPassword = !string.IsNullOrEmpty(password);
        if (Id == AnyDesk.Id)
        {
            start.ArgumentList.Add(id);
            if (hasPassword) { start.ArgumentList.Add("--with-password"); start.RedirectStandardInput = true; input = password; }
        }
        else if (Id == RustDesk.Id)
        {
            start.ArgumentList.Add("--connect"); start.ArgumentList.Add(id);
            if (hasPassword) { start.ArgumentList.Add("--password"); start.ArgumentList.Add(password!); }
        }
        else
        {
            start.ArgumentList.Add("-i"); start.ArgumentList.Add(id);
            if (hasPassword) { start.ArgumentList.Add("--Password"); start.ArgumentList.Add(password!); }
        }
        return new(start, input);
    }

    public void Start(string executable, string id, string? password)
    {
        var command = BuildCommand(executable, id, password);
        using var process = Process.Start(command.Start) ?? throw new InvalidOperationException($"{Name} konnte nicht gestartet werden.");
        if (command.StandardInput is { } input)
        {
            process.StandardInput.WriteLine(input);
            process.StandardInput.Close();
        }
    }
}
