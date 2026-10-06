using System.Text.Json;

namespace WorkspaceManager.Addon.Remote;

// Passwords live in the Windows credential store, never in the settings file. The file holds names, IDs and choices only.

internal sealed class PasswordProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public bool IsDefault { get; set; }
}

internal sealed class DeviceEntry
{
    public string Name { get; set; } = "";
    public string RemoteId { get; set; } = "";
    // Name of a saved password; empty means "use the default password".
    public string? PasswordId { get; set; }
}

internal sealed class ToolSettings
{
    public string? Executable { get; set; }
    public List<PasswordProfile> Passwords { get; set; } = [];
    public List<DeviceEntry> Devices { get; set; } = [];

    public PasswordProfile? DefaultPassword => Passwords.FirstOrDefault(p => p.IsDefault) ?? Passwords.FirstOrDefault();
    public PasswordProfile? PasswordFor(DeviceEntry? device) =>
        device?.PasswordId is { } id && Passwords.FirstOrDefault(p => p.Id == id) is { } own ? own : DefaultPassword;
}

internal sealed class RemoteSettings
{
    public string Hotkey { get; set; } = "";
    public string DefaultTool { get; set; } = "teamviewer";
    // Which programs the addon offers. Empty or missing: the ones that are installed, or TeamViewer if none is found.
    public List<string>? EnabledTools { get; set; }
    public Dictionary<string, ToolSettings> Tools { get; set; } = [];

    public IReadOnlyList<RemoteTool> ActiveTools()
    {
        if (EnabledTools is { Count: > 0 })
        {
            var chosen = RemoteTool.All.Where(t => EnabledTools.Contains(t.Id)).ToList();
            if (chosen.Count > 0) return chosen;
        }
        var installed = RemoteTool.All.Where(t => t.FindExecutable(For(t.Id).Executable) is not null).ToList();
        return installed.Count > 0 ? installed : [RemoteTool.TeamViewer];
    }

    // The program a form starts with: the saved default if it is offered, otherwise the first one.
    public RemoteTool StartTool()
    {
        var active = ActiveTools();
        return active.FirstOrDefault(t => t.Id == DefaultTool) ?? active[0];
    }

    public ToolSettings For(string toolId)
    {
        if (!Tools.TryGetValue(toolId, out var settings)) Tools[toolId] = settings = new();
        return settings;
    }
}

internal sealed class RemoteStore
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    readonly string path;

    public RemoteSettings Settings { get; private set; } = new();

    RemoteStore(string folder) => path = Path.Combine(folder, "remote.json");

    public static RemoteStore Load(string folder)
    {
        var store = new RemoteStore(folder);
        try
        {
            if (File.Exists(store.path)) store.Settings = JsonSerializer.Deserialize<RemoteSettings>(File.ReadAllText(store.path), Json) ?? new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged file is kept aside instead of being overwritten, then the addon starts empty.
            try { File.Move(store.path, store.path + ".broken", true); } catch (Exception inner) when (inner is IOException or UnauthorizedAccessException) { }
        }
        return store;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Settings, Json));
        File.Move(temporary, path, true);
    }

    static string CredentialId(PasswordProfile profile) => "addon-remote-" + profile.Id;

    public void WritePassword(PasswordProfile profile, string password) => Credentials.Write(CredentialId(profile), "remote", password);

    public string? ReadPassword(PasswordProfile profile)
    {
        try
        {
            var secret = Credentials.Read(CredentialId(profile));
            try { return new string(secret.Password); }
            finally { Array.Clear(secret.Password); }
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    public void DeletePassword(PasswordProfile profile)
    {
        try { Credentials.Delete(CredentialId(profile)); } catch (System.ComponentModel.Win32Exception) { }
    }
}
