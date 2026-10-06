using System.Runtime.CompilerServices;
using WorkspaceManager.Addons;

[assembly: InternalsVisibleTo("WorkspaceManager.Addon.Remote.Tests")]

namespace WorkspaceManager.Addon.Remote;

// Fernwartung: connect to TeamViewer, AnyDesk or RustDesk from a hotkey, with saved devices and passwords.
public sealed class RemoteAddon : IAddon
{
    RemoteStore? store;

    public string Id => "remote";
    public string Name => "Fernwartung";
    public string Description => "Verbindung zu TeamViewer, AnyDesk oder RustDesk per Tastenkürzel herstellen, mit gespeicherten Geräten und Passwörtern.";
    public string Version => typeof(RemoteAddon).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public IReadOnlyList<AddonTab> Tabs => store is { } s && host is { } h ? [new("Fernwartung", (body, context) => RemoteTab.Build(s, h, body, context))] : [];

    // The sidebar entry: the connection form right on a page.
    public IReadOnlyList<AddonPage> Pages => store is { } s ? [new("Fernwartung", "", (body, context) => RemotePage.Build(s, body, context))] : [];

    public IReadOnlyList<AddonHotkey> Hotkeys => store is { } s && !string.IsNullOrWhiteSpace(s.Settings.Hotkey)
        ? [new("Fernwartung Verbindung herstellen", s.Settings.Hotkey, () => ConnectForm.ShowSingle(s))]
        : [];

    IAddonHost? host;

    public void Start(IAddonHost host)
    {
        this.host = host;
        store = RemoteStore.Load(host.DataFolder);
    }

    public void Stop()
    {
        ConnectForm.CloseOpen();
        store = null; host = null;
    }
}
