using WorkspaceManager.Addons;

namespace WorkspaceManager.Addon.Remote;

// The sidebar page "Fernwartung": the same connection form as the hotkey window, plus a shortcut to the settings.
internal static class RemotePage
{
    public static void Build(RemoteStore store, TableLayoutPanel body, AddonTabContext context)
    {
        Ui.Paragraph(body, "ID oder Gerätename eingeben. Ist ein Passwort gespeichert, wird es automatisch verwendet.", muted: true);
        var card = Ui.Card(body);
        _ = new ConnectPanel(store, card, connected: null, cancel: null);
        Ui.Add(body, Ui.Actions(Ui.Button("Geräte, Passwörter und Tastenkürzel verwalten", () => context.OpenTab("Fernwartung"))));
    }
}
