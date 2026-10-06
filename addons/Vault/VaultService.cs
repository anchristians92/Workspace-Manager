namespace WorkspaceManager.Addon.Vault;

// Keeps the login to the password manager open, so a search works right away when the shortcut is pressed.
// The login is made in the background when the addon starts and again whenever the settings change.
internal sealed class VaultService
{
    readonly VaultStore store;
    readonly Func<string, IVaultProvider> create;
    readonly SemaphoreSlim gate = new(1, 1);
    IVaultProvider? provider;

    public string Status { get; private set; } = "Noch nicht verbunden.";
    public bool Connected { get; private set; }
    public bool Configured => store.Settings.Server.Trim().Length > 0 && store.ReadLogin() is not null;
    public event Action? StatusChanged;

    public VaultService(VaultStore store, Func<string, IVaultProvider>? create = null)
    {
        this.store = store; this.create = create ?? VaultProviders.Create;
    }

    public async Task<IVaultProvider> EnsureAsync(CancellationToken cancel = default)
    {
        await gate.WaitAsync(cancel);
        try
        {
            if (provider is not null && Connected) return provider;
            return await ConnectLocked(cancel);
        }
        finally { gate.Release(); }
    }

    async Task<IVaultProvider> ConnectLocked(CancellationToken cancel)
    {
        var settings = store.Settings;
        if (settings.Server.Trim().Length == 0 || store.ReadLogin() is not { } login)
        {
            Set(false, "Noch nicht eingerichtet. Server und Zugangsdaten trägst du unter Einstellungen → Addons → Passwort-Tresor ein.");
            throw new InvalidOperationException(Status);
        }
        var next = create(settings.Provider);
        try
        {
            await next.ConnectAsync(new VaultConnection(settings.Server, login.Username, login.Password), cancel);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Set(false, "Anmeldung fehlgeschlagen: " + e.Message);
            throw;
        }
        provider = next;
        Set(true, $"Verbunden mit {next.Name} als {login.Username}.");
        return next;
    }

    // Logs in again with the current settings (after the settings were changed).
    public async Task ReconnectAsync(CancellationToken cancel = default)
    {
        await gate.WaitAsync(cancel);
        try { provider = null; await ConnectLocked(cancel); }
        finally { gate.Release(); }
    }

    // Forgets the login (the credentials were deleted).
    public void Reset()
    {
        provider = null; Set(false, "Noch nicht eingerichtet.");
    }

    // Starts the login without waiting for it. Errors end up in <see cref="Status"/>.
    public void WarmUp() => _ = Task.Run(async () => { try { await ReconnectAsync(); } catch (Exception e) when (e is not OutOfMemoryException) { } });

    public async Task<IReadOnlyList<VaultEntry>> SearchAsync(string text, CancellationToken cancel = default) =>
        await (await EnsureAsync(cancel)).SearchAsync(text, cancel);

    public async Task<string> GetPasswordAsync(VaultEntry entry, CancellationToken cancel = default) =>
        await (await EnsureAsync(cancel)).GetPasswordAsync(entry, cancel);

    // Tries the values from the settings page without saving them.
    public static async Task TestAsync(string providerId, VaultConnection connection, Func<string, IVaultProvider>? create = null, CancellationToken cancel = default) =>
        await (create ?? VaultProviders.Create)(providerId).ConnectAsync(connection, cancel);

    void Set(bool connected, string status)
    {
        Connected = connected; Status = status;
        StatusChanged?.Invoke();
    }
}
