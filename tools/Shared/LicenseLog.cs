using System.Globalization;

namespace WorkspaceManager;

// Shared by tools/LicenseTool and tools/LicenseGenerator: the log of every license that was issued.
// A plain semicolon-separated text file (opens in Excel), stored next to the private key.

public sealed record IssuedLicense(string Id, string Created, string CreatedBy, string Licensee, string Note, int Seats, string ValidFrom, string? ValidUntil, string Key)
{
    public string Status(DateOnly today) =>
        ValidUntil is null ? "unbefristet" :
        DateOnly.TryParseExact(ValidUntil, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) && end < today ? "abgelaufen" : "gültig";
}

public static class LicenseLog
{
    const string Header = "id;erstellt;erstellt_von;lizenznehmer;notiz;geraete;gueltig_ab;gueltig_bis;schluessel";

    public static string PathFor(string privateKeyPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(privateKeyPath))!, "issued-licenses.csv");

    static string Clean(string text) => text.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ').Trim();

    public static IssuedLicense NewEntry(string id, string licensee, string note, int seats, string validFrom, string? validUntil, string key) =>
        new(id, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), Environment.UserName, Clean(licensee), Clean(note), seats, validFrom, validUntil, key);

    public static void Append(string path, IssuedLicense entry)
    {
        if (!File.Exists(path)) File.WriteAllText(path, Header + "\n");
        File.AppendAllText(path, string.Join(';', entry.Id, entry.Created, Clean(entry.CreatedBy), Clean(entry.Licensee), Clean(entry.Note), entry.Seats,
            entry.ValidFrom, entry.ValidUntil ?? "unbefristet", entry.Key) + "\n");
    }

    // Newest first. Lines that do not parse are skipped.
    public static List<IssuedLicense> Read(string path)
    {
        var result = new List<IssuedLicense>();
        if (!File.Exists(path)) return result;
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            var p = line.Split(';', 9);
            if (p.Length != 9 || !int.TryParse(p[5], out var seats)) continue;
            result.Add(new IssuedLicense(p[0], p[1], p[2], p[3], p[4], seats, p[6], p[7] == "unbefristet" ? null : p[7], p[8]));
        }
        result.Reverse();
        return result;
    }
}
