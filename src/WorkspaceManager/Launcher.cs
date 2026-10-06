using System.ComponentModel;
using System.Diagnostics;
using System.Security;

namespace WorkspaceManager;

// Builds how a program is started: normally, as administrator or as another user with a stored login.
internal static class AppLauncher
{
    // Splits "DOMAIN\user" and "user@domain.tld"; a plain name belongs to the domain (or computer) of this PC.
    internal static (string User, string? Domain) SplitUser(string name)
    {
        name = name.Trim();
        var slash = name.IndexOf('\\');
        if (slash > 0) return (name[(slash + 1)..], name[..slash]);
        if (name.Contains('@')) return (name, null);
        return (name, Environment.UserDomainName);
    }

    // Scripts, RDP files and shortcuts are not programs of their own: they need a program that opens them.
    // That matters when no shell is involved (another user). The shell does it by itself for a normal start.
    internal static (string File, string Arguments) Target(string path, string arguments)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".exe": return (path, arguments);
            case ".bat" or ".cmd": return ("cmd.exe", arguments.Length == 0 ? $"/c \"\"{path}\"\"" : $"/c \"\"{path}\" {arguments}\"");
            case ".rdp": return ("mstsc.exe", arguments.Length == 0 ? $"\"{path}\"" : $"\"{path}\" {arguments}");
            default: throw new InvalidOperationException("Verknüpfungen (.lnk) können nicht als anderer Benutzer gestartet werden. Wähle direkt das Programm, auf das sie zeigt.");
        }
    }

    // <readLogin> gives the stored user name and password of a login (the credential store in the app).
    internal static ProcessStartInfo Create(AppEntry app, IReadOnlyList<LoginEntry> logins, Func<string, (string Username, char[] Password)>? readLogin = null)
    {
        var folder = Path.GetDirectoryName(app.Executable)!;
        switch (app.RunAs)
        {
            case "Admin":
                return new ProcessStartInfo(app.Executable, app.Arguments) { UseShellExecute = true, Verb = "runas", WorkingDirectory = folder };
            case "User":
                var login = logins.FirstOrDefault(l => l.Id == app.RunAsLoginId)
                    ?? throw new InvalidOperationException($"Der Login, mit dem „{app.Name}“ gestartet werden soll, existiert nicht mehr. Bitte beim Programm einen anderen wählen.");
                var (file, arguments) = Target(app.Executable, app.Arguments);
                var secret = (readLogin ?? Credentials.Read)(login.Id);
                try
                {
                    if (secret.Username.Length == 0 || secret.Password.Length == 0) throw new InvalidOperationException($"Der Login „{login.Name}“ enthält keinen Benutzernamen oder kein Passwort. Zum Starten werden beide gebraucht.");
                    var (user, domain) = SplitUser(secret.Username);
                    var password = new SecureString();
                    foreach (var c in secret.Password) password.AppendChar(c);
                    password.MakeReadOnly();
                    return new ProcessStartInfo(file, arguments)
                    {
                        UseShellExecute = false, UserName = user, Domain = domain, Password = password, LoadUserProfile = true, WorkingDirectory = folder,
                    };
                }
                finally { Array.Clear(secret.Password); }
            default:
                return new ProcessStartInfo(app.Executable, app.Arguments) { UseShellExecute = true, WorkingDirectory = folder };
        }
    }

    // Starts the program. Turns Windows' answers for the two special cases into messages the user can act on.
    internal static Process? Start(AppEntry app, IReadOnlyList<LoginEntry> logins)
    {
        var start = Create(app, logins);
        try { return Process.Start(start); }
        catch (Win32Exception e) when (app.RunAs == "Admin" && e.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException($"„{app.Name}“ wurde nicht gestartet: Die Rückfrage von Windows (Administrator) wurde abgebrochen.");
        }
        catch (Win32Exception e) when (app.RunAs == "User")
        {
            throw new InvalidOperationException($"„{app.Name}“ konnte nicht als anderer Benutzer gestartet werden: {e.Message} Prüfe Benutzername und Passwort des Logins.");
        }
        finally { start.Password?.Dispose(); }
    }
}

// Errors the program cannot show in a window (it runs in the tray) are written to <data folder>\error.log.
internal static class ErrorLog
{
    public static void Write(Exception e)
    {
        try
        {
            Directory.CreateDirectory(Storage.Root);
            var path = Path.Combine(Storage.Root, "error.log");
            // Keeps the file small: when it gets too big it starts again.
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {e.GetType().FullName}: {e.Message}{Environment.NewLine}{e.StackTrace}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception inner) when (inner is IOException or UnauthorizedAccessException) { }
    }
}
