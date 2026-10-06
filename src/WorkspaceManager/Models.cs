using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkspaceManager;

public sealed class Settings
{
    public int Version { get; set; } = 1;
    public bool AutoRestore { get; set; } = true;
    // After Windows sign-in, open the programs of the current location's layout that are not running yet.
    public bool AutoOpenLayoutApps { get; set; } = true;
    // "System" follows Windows, otherwise "Light" or "Dark".
    public string ThemeMode { get; set; } = "System";
    // Ids of installed addons the user switched off. Installed addons are on by default.
    public List<string> DisabledAddons { get; set; } = [];
    public List<LocationRule> Locations { get; set; } = [];
    public List<AppEntry> Apps { get; set; } = [];
    public List<LoginEntry> Logins { get; set; } = [];
}
public sealed class LocationRule
{
    public string Name { get; set; } = "";
    public string Subnet { get; set; } = "";
    public string? Gateway { get; set; }
    public string? AdapterId { get; set; }
}
public sealed class AppEntry
{
    public string Name { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string Hotkey { get; set; } = "";
    public bool RestorePosition { get; set; } = true;
    // Locations at which this app is opened automatically after Windows sign-in.
    public List<string> AutoStartLocations { get; set; } = [];
    // How the program is started: "" = normally, "Admin" = as administrator (Windows asks for confirmation),
    // "User" = as another user with the stored login <RunAsLoginId>.
    public string RunAs { get; set; } = "";
    public string RunAsLoginId { get; set; } = "";
}
public sealed class LoginEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Hotkey { get; set; } = "";
    public string AllowedExecutable { get; set; } = "";
    public string TitleContains { get; set; } = "";
    public string Mode { get; set; } = "Both";
}
public record Rect(int X, int Y, int Width, int Height)
{
    public static Rect From(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}
public record MonitorSnapshot(string Device, string Identity, Rect Bounds, Rect WorkArea, bool Primary, uint Dpi);
public sealed class WindowSnapshot
{
    public string Executable { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Monitor { get; set; } = "";
    public int Flags { get; set; }
    public int ShowCommand { get; set; }
    public Rect Normal { get; set; } = new(0, 0, 800, 600);
    // Actual screen coordinates for normal/snapped windows; Normal remains WINDOWPLACEMENT workspace coordinates.
    public Rect? ScreenBounds { get; set; }
    public Rect? VisibleBounds { get; set; }
    public Point Minimum { get; set; }
    public Point Maximum { get; set; }
}
public sealed class LayoutProfile
{
    public int Version { get; set; } = 1;
    public string Location { get; set; } = "";
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;
    public List<MonitorSnapshot> Monitors { get; set; } = [];
    public List<WindowSnapshot> Windows { get; set; } = [];
}
public static class Storage
{
    public static string Root { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersonalWorkspaceManager");
    public static string SettingsPath => Path.Combine(Root, "settings.json");
    public static JsonSerializerOptions Json { get; } = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static Settings ReadSettings()
    {
        if (!File.Exists(SettingsPath)) { var defaults = new Settings(); Save(SettingsPath, defaults); return defaults; }
        return ParseSettings(File.ReadAllText(SettingsPath));
    }
    public static Settings ParseSettings(string json)
    {
        var s = JsonSerializer.Deserialize<Settings>(json, Json) ?? throw new InvalidDataException("Leere Konfiguration.");
        if (s.Version != 1 || s.Locations is null || s.Apps is null || s.Logins is null) throw new InvalidDataException("Ungültige Konfigurationsversion oder Listen.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in s.Locations)
        {
            if (r is null || string.IsNullOrWhiteSpace(r.Name) || !names.Add(r.Name) || !NetworkDiscovery.ValidSubnet(r.Subnet)) throw new InvalidDataException("Standortnamen müssen eindeutig und Subnetze gültige IPv4-CIDR-Netze sein.");
            if (!string.IsNullOrEmpty(r.Gateway) && (!IPAddress.TryParse(r.Gateway, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)) throw new InvalidDataException("Ungültiges Gateway.");
        }
        var hotkeys = new HashSet<Hotkey>();
        foreach (var a in s.Apps)
        {
            if (a is null || string.IsNullOrWhiteSpace(a.Name) || !ValidTarget(a.Executable) || a.Arguments is null || a.AutoStartLocations is null) throw new InvalidDataException("Apps benötigen einen Namen und einen absoluten Pfad zu einem Programm (.exe), Skript (.bat, .cmd), einer RDP-Verbindung (.rdp) oder Verknüpfung (.lnk).");
            if (!hotkeys.Add(Hotkey.Parse(a.Hotkey))) throw new InvalidDataException("Tastenkürzel doppelt vergeben.");
            if (a.RunAs is not ("" or "Admin" or "User") || a.RunAsLoginId is null || (a.RunAs == "User" && !Guid.TryParseExact(a.RunAsLoginId, "N", out _))) throw new InvalidDataException("Ungültige Startart bei einem Programm.");
        }
        var ids = new HashSet<string>();
        foreach (var l in s.Logins)
        {
            if (l is null || !Guid.TryParseExact(l.Id, "N", out _) || !ids.Add(l.Id) || string.IsNullOrWhiteSpace(l.Name) || !(string.IsNullOrEmpty(l.AllowedExecutable) || ValidExe(l.AllowedExecutable)) || l.TitleContains is null || l.Mode is not ("Both" or "Username" or "Password")) throw new InvalidDataException("Ungültiger Login-Eintrag (ID, Name, optionaler EXE-Pfad oder Modus).");
            if (!hotkeys.Add(Hotkey.Parse(l.Hotkey))) throw new InvalidDataException("Tastenkürzel doppelt vergeben.");
        }
        if (s.ThemeMode is not ("System" or "Light" or "Dark")) s.ThemeMode = "System";
        s.DisabledAddons ??= [];
        return s;
    }
    static readonly string[] LaunchExtensions = [".exe", ".bat", ".cmd", ".rdp", ".lnk"];
    static bool ValidTarget(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && LaunchExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    static bool ValidExe(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    public static string ProfilePath(string location) => Path.Combine(Root, "profiles", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(location))) + ".json");
    public static void DeleteProfile(string location)
    {
        var path = ProfilePath(location);
        if (File.Exists(path)) File.Delete(path);
    }
    // Moves a saved layout to a new location name (the profile file is named after the location).
    public static void RenameProfile(string oldName, string newName)
    {
        var profile = ReadProfile(oldName);
        if (profile is null) return;
        profile.Location = newName;
        Save(ProfilePath(newName), profile);
        if (ProfilePath(oldName) != ProfilePath(newName)) DeleteProfile(oldName);
    }
    public static LayoutProfile? ReadProfile(string location)
    {
        var path = ProfilePath(location);
        if (!File.Exists(path)) return null;
        var p = JsonSerializer.Deserialize<LayoutProfile>(File.ReadAllText(path), Json);
        if (p is null || p.Version != 1 || p.Location != location || p.Monitors is null || p.Windows is null) throw new InvalidDataException("Ungültiges Layoutprofil.");
        return p;
    }
    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Json));
        File.Move(temporary, path, true);
    }
}
