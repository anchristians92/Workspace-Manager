using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace WorkspaceManager;

public sealed class LicenseFile
{
    // "Private": free for non-commercial use. "Commercial": 30-day trial, then a license key is required.
    public string Mode { get; set; } = "Private";
    public string? TrialStart { get; set; }
    public string Key { get; set; } = "";
}

public enum LicenseKind { Private, Licensed, Trial, Blocked }

public sealed record LicenseState(LicenseKind Kind, string Headline, string Detail, int TrialDaysLeft = 0)
{
    public bool Allowed => Kind != LicenseKind.Blocked;
}

internal static class LicenseService
{
    public const int TrialDays = 30;
    const string InstallerKey = @"SOFTWARE\WorkspaceManager";
    static LicenseFile? current;
    static string FilePath => Path.Combine(Storage.Root, "license.json");

    public static LicenseFile Current => current ??= EnforceManaged(LoadOrInitialize());
    public static LicenseState State => Evaluate(Current, DateOnly.FromDateTime(DateTime.Today), null, IsManagedDevice);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)] static extern int NetGetJoinInformation(string? server, out nint name, out int status);
    [DllImport("netapi32.dll")] static extern int NetApiBufferFree(nint buffer);
    static bool? managed;

    // Company-managed device: joined to an Active Directory domain or to Microsoft Entra ID (work or school account).
    // Private computers are almost always in a workgroup, so this is a useful filter for "private" use. It is a heuristic.
    public static bool IsManagedDevice => managed ??= DetectManagedDevice();

    static bool DetectManagedDevice()
    {
        try
        {
            if (NetGetJoinInformation(null, out var buffer, out var status) == 0)
            {
                NetApiBufferFree(buffer);
                if (status == 3) return true; // NetSetupDomainName
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo");
            if (key is { SubKeyCount: > 0 }) return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        return false;
    }

    // A managed device cannot be "private": switch it to commercial use once, keeping an existing trial start.
    static LicenseFile EnforceManaged(LicenseFile file)
    {
        if (!IsManagedDevice || file.Mode == "Commercial") return file;
        file.Mode = "Commercial";
        file.TrialStart ??= DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        try { Storage.Save(FilePath, file); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return file;
    }

    public static void Update(Action<LicenseFile> change)
    {
        var file = Current; change(file);
        Storage.Save(FilePath, file);
    }

    static LicenseFile LoadOrInitialize()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<LicenseFile>(File.ReadAllText(FilePath), Storage.Json) ?? new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { /* fall through and start over */ }
        // First start: take over the choice made in the installer, otherwise stay private.
        var file = new LicenseFile();
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(InstallerKey);
            if (key?.GetValue("InstallerLicenseMode") as string == "Commercial")
            {
                file.Mode = "Commercial";
                file.TrialStart = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                file.Key = (key.GetValue("InstallerLicenseKey") as string ?? "").Trim();
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
        try { Storage.Save(FilePath, file); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return file;
    }

    public static string Describe(LicenseCheck check, LicensePayload? payload) => check switch
    {
        LicenseCheck.Malformed => "Der Schlüssel ist unvollständig oder beschädigt. Bitte den ganzen Schlüssel einfügen.",
        LicenseCheck.BadSignature => "Der Schlüssel ist ungültig (die Signatur stimmt nicht).",
        LicenseCheck.WrongProduct => "Der Schlüssel gehört nicht zu WorkspaceManager.",
        LicenseCheck.Expired => $"Der Schlüssel ist abgelaufen{(payload?.Expires is { } end ? " (gültig bis " + DateOnly.ParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + ")" : "")}.",
        _ => "Der Schlüssel ist gültig."
    };

    public static LicenseState Evaluate(LicenseFile file, DateOnly today, string? publicKey = null, bool managedDevice = false)
    {
        if (file.Mode != "Commercial" && !managedDevice)
            return new(LicenseKind.Private, "Privat / nicht-kommerziell", "Kostenlos für den privaten Gebrauch. Für gewerbliche Nutzung ist eine Lizenz nötig.");
        string? keyProblem = null;
        if (!string.IsNullOrWhiteSpace(file.Key))
        {
            var result = LicenseToken.Verify(file.Key, today, publicKey);
            if (result.Check == LicenseCheck.Valid && result.Payload is { } p)
                return new(LicenseKind.Licensed, $"Lizenziert für {p.Licensee}",
                    $"{p.Seats} {(p.Seats == 1 ? "Gerät" : "Geräte")} · " + (p.Expires is { } end ? "gültig bis " + DateOnly.ParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "unbefristet"));
            keyProblem = Describe(result.Check, result.Payload);
        }
        var start = DateOnly.TryParseExact(file.TrialStart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : today;
        var left = TrialDays - Math.Max(0, today.DayNumber - start.DayNumber);
        var note = keyProblem is null ? "" : keyProblem + " ";
        if (left > 0)
            return new(LicenseKind.Trial, $"Testphase: noch {left} {(left == 1 ? "Tag" : "Tage")}",
                note + "Danach wird für die gewerbliche Nutzung eine Lizenz benötigt.", left);
        return new(LicenseKind.Blocked, "Testphase abgelaufen",
            note + "Für die weitere gewerbliche Nutzung wird eine Lizenz benötigt. Bis dahin sind die Funktionen pausiert.");
    }
}
