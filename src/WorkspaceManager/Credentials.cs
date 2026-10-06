using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WorkspaceManager;

public static class Credentials
{
    static string Target(string id) => "PersonalWorkspaceManager/" + id;
    public static void Write(string id, string username, string password)
    {
        if (password.Length * 2 > 2560) throw new InvalidDataException("Passwort ist zu lang für den Windows-Anmeldeinformationsmanager.");
        var pointer = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var c = new Native.Credential { Type = 1, Target = Target(id), Username = username, Blob = pointer, BlobSize = (uint)password.Length * 2, Persist = 2 };
            if (!Native.CredWrite(ref c, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(pointer); }
    }
    public static (string Username, char[] Password) Read(string id)
    {
        if (!Native.CredRead(Target(id), 1, 0, out var pointer)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Zugangsdaten fehlen oder sind nicht zugänglich.");
        try
        {
            var c = Marshal.PtrToStructure<Native.Credential>(pointer);
            var password = new char[c.BlobSize / 2]; Marshal.Copy(c.Blob, password, 0, password.Length);
            return (c.Username, password);
        }
        finally { Native.CredFree(pointer); }
    }
    public static void Delete(string id) { if (!Native.CredDelete(Target(id), 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error()); }
}
public static class LoginSender
{
    static bool busy;
    public static async Task Send(LoginEntry entry)
    {
        if (busy) return;
        busy = true;
        try
        {
            var target = Native.GetForegroundWindow();
            void CheckTarget()
            {
                if (target == 0 || Native.GetForegroundWindow() != target || (!string.IsNullOrEmpty(entry.AllowedExecutable) && !string.Equals(WindowLayouts.Executable(target), entry.AllowedExecutable, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(entry.TitleContains) && !WindowLayouts.Title(target).Contains(entry.TitleContains, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Login abgebrochen: Aktives Fenster passt nicht zum Login-Eintrag oder der Fokus hat gewechselt.");
            }
            CheckTarget();
            var until = DateTime.UtcNow.AddSeconds(3);
            while (new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.Any(key => (Native.GetAsyncKeyState(key) & 0x8000) != 0))
            {
                if (DateTime.UtcNow > until) throw new InvalidOperationException("Login abgebrochen: Hotkey-Tasten bitte loslassen.");
                await Task.Delay(25); CheckTarget();
            }
            var secret = Credentials.Read(entry.Id);
            try
            {
                CheckTarget();
                if (entry.Mode is "Both" or "Username") SendText(secret.Username.AsSpan(), CheckTarget);
                if (entry.Mode == "Both") { CheckTarget(); SendKey(9); await Task.Delay(100); }
                if (entry.Mode is "Both" or "Password") SendText(secret.Password, CheckTarget);
                // Never submit a form automatically; the user reviews the target and presses Enter.
            }
            finally { Array.Clear(secret.Password); }
        }
        finally { busy = false; }
    }
    // For addons: types into a window that was active earlier (for example the one in front when a hotkey opened the addon's window).
    // The window is brought to the front first. If it does not get the focus, or loses it while typing, nothing more is typed.
    internal static async Task TypeInto(nint target, string? username, string? password)
    {
        if (busy) return;
        busy = true;
        try
        {
            if (target == 0 || !Native.IsWindow(target)) throw new InvalidOperationException("Das Fenster, in das getippt werden soll, ist nicht mehr da.");
            Native.SetForegroundWindow(target);
            var until = DateTime.UtcNow.AddSeconds(3);
            while (Native.GetForegroundWindow() != target || new[] { 0x10, 0x11, 0x12, 0x5b, 0x5c }.Any(key => (Native.GetAsyncKeyState(key) & 0x8000) != 0))
            {
                if (DateTime.UtcNow > until) throw new InvalidOperationException("Eintippen abgebrochen: Das Zielfenster hat den Fokus nicht bekommen.");
                await Task.Delay(25);
            }
            await Task.Delay(120);
            void CheckTarget() { if (Native.GetForegroundWindow() != target) throw new InvalidOperationException("Eintippen abgebrochen: Der Fokus hat gewechselt."); }
            CheckTarget();
            if (!string.IsNullOrEmpty(username)) SendText(username.AsSpan(), CheckTarget);
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password)) { CheckTarget(); SendKey(9); await Task.Delay(100); }
            if (!string.IsNullOrEmpty(password)) SendText(password.AsSpan(), CheckTarget);
            // Never submit a form automatically; the user reviews the target and presses Enter.
        }
        finally { busy = false; }
    }
    static void SendText(ReadOnlySpan<char> text, Action check)
    {
        foreach (var c in text)
        {
            check();
            Native.Input[] inputs = [new() { Type = 1, Keyboard = new() { Scan = c, Flags = 4 } }, new() { Type = 1, Keyboard = new() { Scan = c, Flags = 6 } }];
            try { if (Native.SendInput(2, inputs, Marshal.SizeOf<Native.Input>()) != 2) throw new InvalidOperationException("Windows hat die Tastatureingabe blockiert."); }
            finally { Array.Clear(inputs); }
        }
    }
    static void SendKey(ushort key)
    {
        Native.Input[] inputs = [new() { Type = 1, Keyboard = new() { VirtualKey = key } }, new() { Type = 1, Keyboard = new() { VirtualKey = key, Flags = 2 } }];
        if (Native.SendInput(2, inputs, Marshal.SizeOf<Native.Input>()) != 2) throw new InvalidOperationException("Windows hat die Tastatureingabe blockiert.");
    }
}
