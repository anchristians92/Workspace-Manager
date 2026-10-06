using System.Text.Json;

namespace WorkspaceManager.Addon.Vault;

// The file holds the choices only (program, server address, shortcut). User name and password for the login
// to the password manager live in the Windows credential store.

internal sealed class VaultSettings
{
    public string Provider { get; set; } = "pleasant";
    public string Server { get; set; } = "";
    public string Hotkey { get; set; } = "";
    // The clipboard is emptied again after this many seconds (only if it still holds the copied password).
    public int ClearClipboardSeconds { get; set; } = 20;
}

internal sealed class VaultStore
{
    const string CredentialId = "addon-vault-login";
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    readonly string path;

    public VaultSettings Settings { get; private set; } = new();

    VaultStore(string folder) => path = Path.Combine(folder, "vault.json");

    public static VaultStore Load(string folder)
    {
        var store = new VaultStore(folder);
        try
        {
            if (File.Exists(store.path)) store.Settings = JsonSerializer.Deserialize<VaultSettings>(File.ReadAllText(store.path), Json) ?? new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A damaged file is kept aside instead of being overwritten, then the addon starts empty.
            try { File.Move(store.path, store.path + ".broken", true); } catch (Exception inner) when (inner is IOException or UnauthorizedAccessException) { }
        }
        if (!VaultProviders.All.Any(p => p.Id == store.Settings.Provider)) store.Settings.Provider = VaultProviders.All[0].Id;
        store.Settings.ClearClipboardSeconds = Math.Clamp(store.Settings.ClearClipboardSeconds, 5, 300);
        return store;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Settings, Json));
        File.Move(temporary, path, true);
    }

    public void WriteLogin(string username, string password) => Credentials.Write(CredentialId, username, password);

    public (string Username, string Password)? ReadLogin()
    {
        try
        {
            var secret = Credentials.Read(CredentialId);
            try { return (secret.Username, new string(secret.Password)); }
            finally { Array.Clear(secret.Password); }
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    public void DeleteLogin()
    {
        try { Credentials.Delete(CredentialId); } catch (System.ComponentModel.Win32Exception) { }
    }
}
