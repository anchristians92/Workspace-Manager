using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace WorkspaceManager;

// Shared by the app (verifies) and tools/LicenseTool (signs). Must stay free of Windows-only APIs.

public sealed record LicensePayload(string Id, string Licensee, int Seats, string Issued, string? Expires, string Product = "WorkspaceManager", int Version = 1);

public enum LicenseCheck { Valid, Malformed, BadSignature, WrongProduct, Expired }

public sealed record LicenseResult(LicenseCheck Check, LicensePayload? Payload);

public static class LicenseToken
{
    // Public half of the signing key (X.509 SubjectPublicKeyInfo, Base64). The private half never ships.
    public const string PublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEo8qpEDGc7h5I9grRMsGjnZ4OUv77WdEsoossmiSk1r6+wL9mXHqdzhcZYbrPNwcDsYe6qKbmmAm9x36b6LSXgg==";

    const string Prefix = "WSM1";
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    static string Encode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static byte[] Decode(string text)
    {
        text = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(text.PadRight(text.Length + (4 - text.Length % 4) % 4, '='));
    }

    public static string Sign(LicensePayload payload, ECDsa privateKey)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        var signature = privateKey.SignData(body, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{Prefix}.{Encode(body)}.{Encode(signature)}";
    }

    public static LicenseResult Verify(string? token, DateOnly today, string? publicKey = null)
    {
        try
        {
            var parts = new string((token ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).Split('.');
            if (parts.Length != 3 || parts[0] != Prefix) return new(LicenseCheck.Malformed, null);
            var body = Decode(parts[1]); var signature = Decode(parts[2]);
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey ?? PublicKey), out _);
            if (!key.VerifyData(body, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) return new(LicenseCheck.BadSignature, null);
            var payload = JsonSerializer.Deserialize<LicensePayload>(body, Json);
            if (payload is null || payload.Version != 1 || payload.Seats < 1 || string.IsNullOrWhiteSpace(payload.Licensee) || string.IsNullOrWhiteSpace(payload.Id)) return new(LicenseCheck.Malformed, null);
            if (payload.Product != "WorkspaceManager") return new(LicenseCheck.WrongProduct, payload);
            if (payload.Expires is not null && (!DateOnly.TryParseExact(payload.Expires, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end < today)) return new(LicenseCheck.Expired, payload);
            return new(LicenseCheck.Valid, payload);
        }
        catch (Exception e) when (e is FormatException or JsonException or CryptographicException or ArgumentException) { return new(LicenseCheck.Malformed, null); }
    }
}
