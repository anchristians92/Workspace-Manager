namespace WorkspaceManager.Addon.Vault;

// What the addon needs from a password manager. A new program is one more class that implements this interface
// and one more line in VaultProviders; the window, the settings and the typing stay the same.

internal sealed record VaultEntry(string Id, string Name, string Username, string Url, string Path, string Notes = "");

internal sealed record VaultConnection(string Server, string Username, string Password);

internal interface IVaultProvider
{
    string Id { get; }
    string Name { get; }
    /// <summary>An example for the server field, shown as help text.</summary>
    string ServerHint { get; }
    /// <summary>Logs in. Throws an exception with a message the user can read if that fails.</summary>
    Task ConnectAsync(VaultConnection connection, CancellationToken cancel);
    Task<IReadOnlyList<VaultEntry>> SearchAsync(string text, CancellationToken cancel);
    Task<string> GetPasswordAsync(VaultEntry entry, CancellationToken cancel);
}

internal static class VaultProviders
{
    public static IReadOnlyList<(string Id, string Name)> All { get; } = [("pleasant", "Pleasant Password Server")];

    public static IVaultProvider Create(string id) => id switch
    {
        "pleasant" => new PleasantProvider(),
        _ => throw new InvalidDataException($"Das Programm „{id}“ wird nicht unterstützt."),
    };

    public static string NameOf(string id) => All.FirstOrDefault(p => p.Id == id).Name ?? id;
}
