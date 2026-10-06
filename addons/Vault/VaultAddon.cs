using System.Runtime.CompilerServices;
using WorkspaceManager.Addons;

[assembly: InternalsVisibleTo("WorkspaceManager.Addon.Vault.Tests")]

namespace WorkspaceManager.Addon.Vault;

// Passwort-Tresor: search entries in a password manager (Pleasant Password Server) and type them into the window in front.
public sealed class VaultAddon : IAddon
{
    VaultStore? store;
    VaultService? service;
    IAddonHost? host;

    public string Id => "vault";
    public string Name => "Passwort-Tresor";
    public string Description => "Einträge aus einem Passwort-Programm (zum Beispiel Pleasant Password Server) per Tastenkürzel suchen und direkt eintippen.";
    public string Version => typeof(VaultAddon).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public IReadOnlyList<AddonTab> Tabs => store is { } s && service is { } v && host is { } h ? [new("Passwort-Tresor", (body, context) => VaultTab.Build(s, v, h, body, context))] : [];

    public IReadOnlyList<AddonPage> Pages => store is { } s && service is { } v ? [new("Passwort-Tresor", "", (body, context) => VaultPage.Build(v, s, body, context))] : [];

    public IReadOnlyList<AddonHotkey> Hotkeys => store is { } s && service is { } v && !string.IsNullOrWhiteSpace(s.Settings.Hotkey)
        ? [new("Passwort-Tresor öffnen", s.Settings.Hotkey, () => SearchForm.ShowSingle(v, s))]
        : [];

    public void Start(IAddonHost host)
    {
        this.host = host;
        store = VaultStore.Load(host.DataFolder);
        service = new VaultService(store);
        // Log in now, so the first search is quick.
        if (service.Configured) service.WarmUp();
    }

    public void Stop()
    {
        SearchForm.CloseOpen();
        store = null; service = null; host = null;
    }
}
