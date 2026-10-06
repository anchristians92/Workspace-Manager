using System.Reflection;
using WorkspaceManager.Addons;

namespace WorkspaceManager;

// ---- Loading and running addons ----

internal sealed record InstalledAddon(string Id, string Name, string Description, string Version, IAddon? Instance, string? Error);

// Addons live in <app folder>\Addons\<Name>\WorkspaceManager.Addon.*.dll. A broken addon is listed with its error
// and never takes the app down.
internal sealed class AddonManager : IDisposable
{
    public static AddonManager? Current { get; private set; }

    readonly List<InstalledAddon> installed = [];
    readonly Dictionary<string, IAddon> running = [];
    readonly string dataRoot;
    readonly Action hotkeysChanged;
    readonly Action<string> notify;

    public IReadOnlyList<InstalledAddon> Installed => installed;

    public AddonManager(string addonsFolder, string dataRoot, Action hotkeysChanged, Action<string> notify)
    {
        this.dataRoot = dataRoot; this.hotkeysChanged = hotkeysChanged; this.notify = notify;
        Discover(addonsFolder);
        Current = this;
    }

    void Discover(string folder)
    {
        if (!Directory.Exists(folder)) return;
        foreach (var directory in Directory.EnumerateDirectories(folder).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            foreach (var file in Directory.EnumerateFiles(directory, "WorkspaceManager.Addon.*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    foreach (var type in assembly.GetTypes().Where(t => typeof(IAddon).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }))
                    {
                        var addon = (IAddon)Activator.CreateInstance(type)!;
                        installed.Add(new(addon.Id, addon.Name, addon.Description, addon.Version, addon, null));
                    }
                }
                catch (Exception e) when (e is not OutOfMemoryException)
                {
                    var name = Path.GetFileName(directory);
                    installed.Add(new(name, name, "", "", null, e.Message));
                }
            }
    }

    // Starts the enabled addons and stops the ones that were switched off.
    public void Apply(Settings settings)
    {
        foreach (var item in installed.Where(i => i.Instance is not null))
        {
            var enabled = !settings.DisabledAddons.Contains(item.Id, StringComparer.OrdinalIgnoreCase);
            if (enabled && !running.ContainsKey(item.Id))
            {
                try { item.Instance!.Start(new AddonHost(item.Id, dataRoot, hotkeysChanged, notify)); running[item.Id] = item.Instance; }
                catch (Exception e) when (e is not OutOfMemoryException) { notify($"Addon {item.Name} konnte nicht gestartet werden: {e.Message}"); }
            }
            else if (!enabled && running.Remove(item.Id))
            {
                try { item.Instance!.Stop(); } catch (Exception e) when (e is not OutOfMemoryException) { notify($"Addon {item.Name} konnte nicht beendet werden: {e.Message}"); }
            }
        }
    }

    public bool IsRunning(string id) => running.ContainsKey(id);

    public IEnumerable<(string Label, string Hotkey, Action Action)> Hotkeys() =>
        running.Values.SelectMany(addon => addon.Hotkeys).Where(h => !string.IsNullOrWhiteSpace(h.Hotkey))
            .Select(h => (h.Label, h.Hotkey, (Action)(() =>
            {
                try { h.Action(); } catch (Exception e) when (e is not OutOfMemoryException) { notify($"{h.Label}: {e.Message}"); }
            })));

    public IEnumerable<(IAddon Addon, AddonPage Page)> Pages() => running.Values.SelectMany(addon => addon.Pages.Select(page => (addon, page)));

    public IEnumerable<(IAddon Addon, AddonTab Tab)> Tabs() => running.Values.SelectMany(addon => addon.Tabs.Select(tab => (addon, tab)));

    public void Dispose()
    {
        foreach (var addon in running.Values.ToList())
            try { addon.Stop(); } catch (Exception e) when (e is not OutOfMemoryException) { }
        running.Clear();
        if (Current == this) Current = null;
    }

    sealed class AddonHost(string id, string dataRoot, Action hotkeysChanged, Action<string> notify) : IAddonHost
    {
        public string DataFolder
        {
            get { var path = Path.Combine(dataRoot, "addons", id); Directory.CreateDirectory(path); return path; }
        }
        public void HotkeysChanged() => hotkeysChanged();
        public void Notify(string text) => notify(text);
    }
}
