using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkspaceManager;

public record LiveWindow(nint Handle, uint ProcessId, WindowSnapshot Snapshot);
public static class WindowLayouts
{
    public static List<MonitorSnapshot> Monitors() => Screen.AllScreens.Select(screen =>
    {
        var d = new Native.DisplayDevice { Size = Marshal.SizeOf<Native.DisplayDevice>() };
        var identity = Native.EnumDisplayDevices(screen.DeviceName, 0, ref d, 1) ? d.Id : "";
        var monitor = Native.MonitorFromPoint(new() { X = screen.Bounds.X + screen.Bounds.Width / 2, Y = screen.Bounds.Y + screen.Bounds.Height / 2 }, 2);
        Native.GetDpiForMonitor(monitor, 0, out var dpi, out _);
        return new MonitorSnapshot(screen.DeviceName, identity, Rect.From(screen.Bounds), Rect.From(screen.WorkingArea), screen.Primary, dpi);
    }).OrderBy(m => m.Device, StringComparer.Ordinal).ToList();

    public static bool Compatible(IEnumerable<MonitorSnapshot> saved, IEnumerable<MonitorSnapshot> current)
    {
        var a = ByPosition(saved); var b = ByPosition(current);
        return a.Length > 0 && a.Length == b.Length && a.All(m => !string.IsNullOrEmpty(m.Identity) && m.Dpi > 0) && a.Zip(b).All(pair => SameMonitor(pair.First, pair.Second));
    }
    // Windows numbers the displays (\\.\DISPLAY10, ...) anew whenever they are connected again, so the name says nothing.
    // Monitors are compared by where they are: same screen, same size, same place, same scaling, same primary flag.
    public static MonitorSnapshot[] ByPosition(IEnumerable<MonitorSnapshot> monitors) => [.. monitors.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y)];
    public static bool SameMonitor(MonitorSnapshot a, MonitorSnapshot b) =>
        a.Identity == b.Identity && a.Bounds == b.Bounds && a.WorkArea == b.WorkArea && a.Primary == b.Primary && a.Dpi == b.Dpi;
    public static string Title(nint handle) { var text = new StringBuilder(1024); Native.GetWindowText(handle, text, text.Capacity); return text.ToString(); }
    public static string? Executable(nint handle)
    {
        Native.GetWindowThreadProcessId(handle, out var pid);
        try { using var process = Process.GetProcessById((int)pid); return process.MainModule?.FileName; }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException) { return null; }
    }
    public static List<LiveWindow> Capture(bool includeOwn = false)
    {
        var list = new List<LiveWindow>();
        Native.EnumWindows((handle, _) =>
        {
            if (!Native.IsWindowVisible(handle) || !IsApplicationWindow((long)Native.GetWindowLongPtr(handle, -20), Native.GetWindow(handle, 4) != 0)) return true;
            Native.GetWindowThreadProcessId(handle, out var pid);
            if (!includeOwn && pid == Environment.ProcessId) return true;
            Native.DwmGetWindowAttribute(handle, 14, out var cloaked, 4);
            if (cloaked != 0) return true;
            var title = Title(handle); var exe = Executable(handle);
            if (string.IsNullOrEmpty(title) || exe is null) return true;
            var cls = new StringBuilder(256); Native.GetClassName(handle, cls, cls.Capacity);
            if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            var p = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>() };
            if (!Native.GetWindowPlacement(handle, ref p)) return true;
            Rect? actual = null;
            if (p.ShowCommand == 1 && Native.GetWindowRect(handle, out var r)) actual = new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            Rect? visible = null;
            if (p.ShowCommand == 1 && Native.DwmGetWindowAttributeRect(handle, 9, out var v, Marshal.SizeOf<Native.RECT>()) == 0)
                visible = new(v.Left, v.Top, v.Right - v.Left, v.Bottom - v.Top);
            list.Add(new(handle, pid, new() { Executable = exe, ClassName = cls.ToString(), Title = title, Monitor = Screen.FromHandle(handle).DeviceName,
                Flags = p.Flags & 2, ShowCommand = p.ShowCommand, Normal = new(p.Normal.Left, p.Normal.Top, p.Normal.Right - p.Normal.Left, p.Normal.Bottom - p.Normal.Top),
                ScreenBounds = actual,
                VisibleBounds = visible,
                Minimum = new(p.Minimum.X, p.Minimum.Y), Maximum = new(p.Maximum.X, p.Maximum.Y) }));
            return true;
        }, 0);
        return list;
    }
    public static bool IsApplicationWindow(long extendedStyle, bool hasOwner)
    {
        // WS_EX_APPWINDOW explicitly requests an application/taskbar window even when owned.
        // Owned dialogs without that flag and tool windows remain excluded.
        return (extendedStyle & 0x80) == 0 && (!hasOwner || (extendedStyle & 0x40000) != 0);
    }
    public static LayoutProfile Save(string location)
    {
        var monitors = Monitors();
        if (!Compatible(monitors, monitors)) throw new InvalidOperationException("Monitoridentität oder Skalierung konnte nicht sicher ermittelt werden.");
        var profile = new LayoutProfile { Location = location, Monitors = monitors, Windows = Capture().Select(w => w.Snapshot).Where(ShouldSave).ToList() };
        if (profile.Windows.Count == 0) throw new InvalidOperationException("Keine speicherbaren Fenster gefunden.");
        Storage.Save(Storage.ProfilePath(location), profile); return profile;
    }
    public static bool ShouldSave(WindowSnapshot window) => window.ShowCommand is not (2 or 6 or 7);
    public static List<(LiveWindow Live, WindowSnapshot Saved)> Match(IReadOnlyList<WindowSnapshot> saved, List<LiveWindow> available)
    {
        var matches = new List<(LiveWindow, WindowSnapshot)>(); var used = new HashSet<nint>();
        // Exact titles first; ambiguous same-program windows are never assigned arbitrarily.
        foreach (var s in saved)
        {
            var candidates = available.Where(w => !used.Contains(w.Handle) && SameApp(w.Snapshot, s) && w.Snapshot.Title == s.Title).ToArray();
            if (candidates.Length == 1 && saved.Count(x => SameApp(x, s) && x.Title == s.Title) == 1) { matches.Add((candidates[0], s)); used.Add(candidates[0].Handle); }
        }
        foreach (var s in saved.Where(s => !matches.Any(m => ReferenceEquals(m.Item2, s))))
        {
            var candidates = available.Where(w => !used.Contains(w.Handle) && SameApp(w.Snapshot, s)).ToArray();
            if (candidates.Length == 1 && saved.Count(x => SameApp(x, s) && !matches.Any(m => ReferenceEquals(m.Item2, x))) == 1) { matches.Add((candidates[0], s)); used.Add(candidates[0].Handle); }
        }
        return matches;
    }
    static bool SameApp(WindowSnapshot a, WindowSnapshot b) => string.Equals(a.Executable, b.Executable, StringComparison.OrdinalIgnoreCase) && StableClass(a.ClassName) == StableClass(b.ClassName);

    // Some programs add a number to the window class that is new at every start ("MyApp_089F6938", "HwndWrapper[app;;<GUID>]").
    // Such a part is replaced by a star, so the same window is found again after a restart.
    internal static string StableClass(string name) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(name, @"[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}", "*"),
            @"(?<=_)[0-9A-Fa-f]{6,16}$", "*");
    public static bool Apply(nint handle, WindowSnapshot s)
    {
        if (!Native.IsWindow(handle) || !string.Equals(Executable(handle), s.Executable, StringComparison.OrdinalIgnoreCase) || s.Normal.Width <= 0 || s.Normal.Height <= 0) return false;
        var p = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>(), Flags = s.Flags & 2,
            ShowCommand = s.ShowCommand is 2 or 6 or 7 ? 7 : s.ShowCommand == 3 ? 3 : 4,
            Minimum = new() { X = s.Minimum.X, Y = s.Minimum.Y }, Maximum = new() { X = s.Maximum.X, Y = s.Maximum.Y },
            Normal = new() { Left = s.Normal.X, Top = s.Normal.Y, Right = s.Normal.X + s.Normal.Width, Bottom = s.Normal.Y + s.Normal.Height } };
        // A window that is maximized already stays on its monitor when it is told to maximize again, whatever position it gets.
        // So a maximized window is first placed normally on its saved monitor and only then maximized.
        if (p.ShowCommand == 3)
        {
            var normal = p with { ShowCommand = 1 };
            if (!Native.SetWindowPlacement(handle, in normal)) return false;
        }
        if (!Native.SetWindowPlacement(handle, in p)) return false;
        // Snap keeps an older restore rectangle in WINDOWPLACEMENT. Restore the actual saved
        // screen rectangle separately, without mixing workspace and screen coordinate systems.
        if (s.ShowCommand == 1 && s.ScreenBounds is { Width: > 0, Height: > 0 } bounds)
        {
            if (!Native.SetWindowPos(handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0004 | 0x0010 | 0x0200)) return false;
            if (s.VisibleBounds is { Width: > 0, Height: > 0 } desired &&
                Native.DwmGetWindowAttributeRect(handle, 9, out var current, Marshal.SizeOf<Native.RECT>()) == 0 &&
                Native.GetWindowRect(handle, out var outer))
            {
                var visible = new Rect(current.Left, current.Top, current.Right - current.Left, current.Bottom - current.Top);
                if (visible != desired)
                    return Native.SetWindowPos(handle, 0,
                        outer.Left + desired.X - visible.X, outer.Top + desired.Y - visible.Y,
                        outer.Right - outer.Left + desired.Width - visible.Width,
                        outer.Bottom - outer.Top + desired.Height - visible.Height, 0x0004 | 0x0010 | 0x0200);
            }
            return true;
        }
        return true;
    }
    public static string Restore(string location, string? executable = null)
    {
        var profile = Storage.ReadProfile(location) ?? throw new InvalidOperationException($"Für {location} wurde noch kein Layout gespeichert.");
        if (!Compatible(profile.Monitors, Monitors())) throw new InvalidOperationException("Monitorlandschaft passt nicht zum Profil (Identität, Position, Auflösung, Arbeitsfläche oder Skalierung).");
        var saved = profile.Windows.Where(w => executable is null || string.Equals(w.Executable, executable, StringComparison.OrdinalIgnoreCase)).ToList();
        var matches = Match(saved, Capture()); var success = 0;
        foreach (var pair in matches) if (Apply(pair.Live.Handle, pair.Saved)) success++;
        return $"{location}: {success}/{saved.Count} Fenster wiederhergestellt. Fehlende oder mehrdeutige Fenster werden übersprungen.";
    }
}
