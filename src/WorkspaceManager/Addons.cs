using System.Runtime.CompilerServices;

// First-party addons may use the app's internal UI helpers (Ui, AppTheme, EntryDialog ...).
[assembly: InternalsVisibleTo("WorkspaceManager.Addon.Remote")]
[assembly: InternalsVisibleTo("WorkspaceManager.Addon.Remote.Tests")]
[assembly: InternalsVisibleTo("WorkspaceManager.Addon.Vault")]
[assembly: InternalsVisibleTo("WorkspaceManager.Addon.Vault.Tests")]

namespace WorkspaceManager.Addons;

// ---- Contract between the app and an addon ----

/// <summary>A tab an addon adds under Einstellungen.</summary>
public sealed record AddonTab(string Title, Action<TableLayoutPanel, AddonTabContext> Build);

/// <summary>An entry in the sidebar with its own page (shown while the addon is switched on).</summary>
public sealed record AddonPage(string Title, string Glyph, Action<TableLayoutPanel, AddonTabContext> Build);

/// <summary>What a tab or page needs from the window: an owner for dialogs, a way to redraw the page and a way to open one of the addon's settings tabs.</summary>
public sealed record AddonTabContext(IWin32Window Owner, Action Refresh, Action<string> OpenTab);

/// <summary>A global keyboard shortcut an addon wants (for example "Strg+Alt+T").</summary>
public sealed record AddonHotkey(string Label, string Hotkey, Action Action);

public interface IAddonHost
{
    /// <summary>A folder only for this addon, created on demand (settings files and the like).</summary>
    string DataFolder { get; }
    /// <summary>Call after <see cref="IAddon.Hotkeys"/> changed so the app registers the new shortcuts.</summary>
    void HotkeysChanged();
    void Notify(string text);
}

public interface IAddon
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Version { get; }
    IReadOnlyList<AddonTab> Tabs { get; }
    IReadOnlyList<AddonPage> Pages { get; }
    IReadOnlyList<AddonHotkey> Hotkeys { get; }
    void Start(IAddonHost host);
    void Stop();
}
