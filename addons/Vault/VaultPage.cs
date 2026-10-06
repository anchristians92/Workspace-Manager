using WorkspaceManager.Addons;

namespace WorkspaceManager.Addon.Vault;

// The sidebar page "Passwort-Tresor": the same search as the hotkey window. Typing into other windows only works from the
// hotkey window, here an entry is copied.
internal static class VaultPage
{
    public static void Build(VaultService service, VaultStore store, TableLayoutPanel body, AddonTabContext context)
    {
        Ui.Paragraph(body, "Eintrag suchen und das Passwort in die Zwischenablage kopieren. Zum direkten Eintippen in ein anderes Fenster nutzt du das Tastenkürzel.", muted: true);
        var card = Ui.Card(body);
        _ = new SearchPanel(service, store, card, typeTarget: null, finished: null);
        if (!service.Configured)
            Ui.Add(body, Ui.Actions(Ui.Button("Verbindung einrichten", () => context.OpenTab("Passwort-Tresor"), true)));
        else
            Ui.Add(body, Ui.Actions(Ui.Button("Verbindung und Tastenkürzel einstellen", () => context.OpenTab("Passwort-Tresor"))));
    }
}
