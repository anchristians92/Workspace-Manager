using System.Globalization;
using System.Security.Cryptography;
using WorkspaceManager;

// License generator for WorkspaceManager. Keep the private key OUTSIDE the repository.
//
//   LicenseTool keygen --out <folder>
//   LicenseTool issue  --key <private.key> --licensee "Firma GmbH" --seats 10 [--days 365 | --perpetual]
//   LicenseTool verify <token> [--public <base64>]

static string? Option(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static int Fail(string message) { Console.Error.WriteLine("Fehler: " + message); return 1; }

static void Usage()
{
    Console.WriteLine("""
        LicenseTool - Lizenzschlüssel für WorkspaceManager

          keygen --out <Ordner>
              Erzeugt ein Schlüsselpaar. Der private Schlüssel kommt in <Ordner>\license-private.key
              und darf NIE ins Repository. Der öffentliche Schlüssel wird ausgegeben und in
              src\WorkspaceManager\LicenseToken.cs eingetragen.

          issue --key <private.key> --licensee "<Name>" --seats <Anzahl> [--days <Tage> | --perpetual] [--note "<Notiz>"]
              Erzeugt einen Lizenzschlüssel. Ohne --days oder --perpetual gilt er unbefristet.
              Jeder Schlüssel wird in issued-licenses.csv neben dem privaten Schlüssel protokolliert.

          verify <Schlüssel> [--public <Base64>]
              Prüft einen Schlüssel gegen den eingebauten (oder angegebenen) öffentlichen Schlüssel.
        """);
}

if (args.Length == 0) { Usage(); return 0; }

switch (args[0])
{
    case "keygen":
    {
        var folder = Option(args, "--out");
        if (folder is null) return Fail("--out <Ordner> fehlt.");
        Directory.CreateDirectory(folder);
        var privatePath = Path.Combine(folder, "license-private.key");
        if (File.Exists(privatePath)) return Fail($"{privatePath} existiert bereits und wird nicht überschrieben.");
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        File.WriteAllText(privatePath, key.ExportPkcs8PrivateKeyPem());
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        File.WriteAllText(Path.Combine(folder, "license-public.txt"), publicKey);
        Console.WriteLine("Privater Schlüssel: " + privatePath + "  (sichern, nicht weitergeben, nicht ins Repository!)");
        Console.WriteLine("Öffentlicher Schlüssel (in LicenseToken.PublicKey eintragen):");
        Console.WriteLine(publicKey);
        return 0;
    }
    case "issue":
    {
        var keyPath = Option(args, "--key"); var licensee = Option(args, "--licensee");
        if (keyPath is null || licensee is null) return Fail("--key und --licensee sind Pflicht.");
        if (!int.TryParse(Option(args, "--seats"), out var seats) || seats < 1) return Fail("--seats muss eine Zahl ab 1 sein.");
        var today = DateOnly.FromDateTime(DateTime.Today);
        string? expires = null;
        if (!args.Contains("--perpetual") && Option(args, "--days") is { } days)
        {
            if (!int.TryParse(days, out var count) || count < 1) return Fail("--days muss eine Zahl ab 1 sein.");
            expires = today.AddDays(count).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        using var key = ECDsa.Create();
        key.ImportFromPem(File.ReadAllText(keyPath));
        var payload = new LicensePayload(Guid.NewGuid().ToString("N"), licensee.Trim(), seats, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), expires);
        var token = LicenseToken.Sign(payload, key);
        var check = LicenseToken.Verify(token, today, Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
        if (check.Check != LicenseCheck.Valid) return Fail("Der erzeugte Schlüssel besteht die eigene Prüfung nicht: " + check.Check);
        LicenseLog.Append(LicenseLog.PathFor(keyPath), LicenseLog.NewEntry(payload.Id, payload.Licensee, Option(args, "--note") ?? "", payload.Seats, payload.Issued, payload.Expires, token));
        Console.WriteLine($"Lizenznehmer: {payload.Licensee}\nGeräte:       {payload.Seats}\nGültig bis:   {payload.Expires ?? "unbefristet"}\n\n{token}");
        return 0;
    }
    case "verify":
    {
        if (args.Length < 2) return Fail("Schlüssel fehlt.");
        var result = LicenseToken.Verify(args[1], DateOnly.FromDateTime(DateTime.Today), Option(args, "--public"));
        Console.WriteLine(result.Check);
        if (result.Payload is { } p) Console.WriteLine($"{p.Licensee}, {p.Seats} Geräte, gültig bis {p.Expires ?? "unbefristet"}");
        return result.Check == LicenseCheck.Valid ? 0 : 2;
    }
    default:
        Usage();
        return 1;
}
