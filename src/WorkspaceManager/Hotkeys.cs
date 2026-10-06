namespace WorkspaceManager;

public readonly record struct Hotkey(uint Modifiers, uint Key)
{
    public static Hotkey Parse(string? value)
    {
        var parts = value?.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (parts.Length < 2) throw new InvalidDataException("Hotkey benötigt Modifikatortaste, z. B. Ctrl+Alt+I.");
        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            uint m = part.ToUpperInvariant() switch { "CTRL" or "STRG" => 2, "ALT" => 1, "SHIFT" or "UMSCHALT" => 4, "WIN" => 8, _ => throw new InvalidDataException($"Unbekannte Hotkey-Taste: {part}") };
            if ((modifiers & m) != 0) throw new InvalidDataException("Modifikatortaste doppelt.");
            modifiers |= m;
        }
        var last = parts[^1]; if (last.Length == 1 && char.IsAsciiDigit(last[0])) last = "D" + last;
        if (!Enum.TryParse<Keys>(last, true, out var key) || !Enum.IsDefined(key) || (int)key < 8 || (int)key > 254 || key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin) throw new InvalidDataException("Ungültige Hotkey-Taste.");
        return new(modifiers, (uint)key);
    }
}
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    readonly Dictionary<int, Action> actions = [];
    public HotkeyWindow() => CreateHandle(new CreateParams { Caption = "PersonalWorkspaceManager.Hotkeys", Parent = new nint(-3) });
    public List<string> Configure(Settings settings, Action<AppEntry> launch, Action<LoginEntry> login, IEnumerable<(string Label, string Hotkey, Action Action)>? extra = null)
    {
        foreach (var id in actions.Keys) Native.UnregisterHotKey(Handle, id);
        actions.Clear(); var failures = new List<string>(); var next = 1;
        void Register(string label, string text, Action action)
        {
            var hotkey = Hotkey.Parse(text); var id = next++;
            if (Native.RegisterHotKey(Handle, id, hotkey.Modifiers | 0x4000, hotkey.Key)) actions[id] = action;
            else failures.Add($"{label}: {text} ist bereits belegt oder nicht verfügbar.");
        }
        foreach (var app in settings.Apps) Register(app.Name, app.Hotkey, () => launch(app));
        foreach (var loginEntry in settings.Logins) Register(loginEntry.Name, loginEntry.Hotkey, () => login(loginEntry));
        foreach (var (label, text, action) in extra ?? [])
        {
            try { Register(label, text, action); }
            catch (InvalidDataException e) { failures.Add($"{label}: {e.Message}"); }
        }
        return failures;
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && actions.TryGetValue((int)m.WParam, out var action)) action();
        base.WndProc(ref m);
    }
    public void Dispose() { foreach (var id in actions.Keys) Native.UnregisterHotKey(Handle, id); DestroyHandle(); }
}
