using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkspaceManager.Addon.Vault;

// Pleasant Password Server, REST API: https://pleasantpasswords.com/info/pleasant-password-server/m-programmatic-access/restful-api
// Login with OAuth2 (password grant) gives a bearer token that is kept in memory and renewed when it runs out.
// Servers with two-factor login are not supported.
internal sealed class PleasantProvider : IVaultProvider
{
    const string ApiVersion = "v6";
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    readonly HttpClient http;
    VaultConnection? connection;
    string? token;
    DateTime tokenUntil;

    public PleasantProvider(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(20);
    }

    public string Id => "pleasant";
    public string Name => "Pleasant Password Server";
    public string ServerHint => "https://pleasant.firma.local:10001";

    // "pleasant.firma.local:10001" and "https://pleasant.firma.local:10001/" both mean the same server.
    internal static string NormalizeServer(string server)
    {
        var text = server.Trim();
        if (text.Length == 0) throw new InvalidDataException("Bitte die Serveradresse eintragen.");
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || uri.Host.Length == 0)
            throw new InvalidDataException("Das ist keine gültige Serveradresse. Beispiel: " + "https://pleasant.firma.local:10001");
        return uri.GetLeftPart(UriPartial.Authority);
    }

    string Api(string path) => $"{NormalizeServer(connection!.Server)}/api/{ApiVersion}/rest/{path}";

    public async Task ConnectAsync(VaultConnection newConnection, CancellationToken cancel)
    {
        var server = NormalizeServer(newConnection.Server);
        if (newConnection.Username.Length == 0 || newConnection.Password.Length == 0) throw new InvalidDataException("Bitte Benutzername und Passwort eintragen.");
        connection = newConnection with { Server = server };
        token = null;

        using var request = new HttpRequestMessage(HttpMethod.Post, server + "/OAuth2/Token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password", ["username"] = newConnection.Username, ["password"] = newConnection.Password,
            }),
        };
        using var response = await Send(request, cancel);
        var body = await response.Content.ReadAsStringAsync(cancel);
        if (!response.IsSuccessStatusCode)
        {
            if (response.Headers.Contains("X-Pleasant-OTP"))
                throw new InvalidOperationException("Der Server verlangt einen zweiten Faktor. Das wird vom Addon noch nicht unterstützt.");
            throw new InvalidOperationException(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
                ? "Anmeldung abgelehnt. Bitte Benutzername und Passwort prüfen." + Detail(body)
                : $"Der Server hat die Anmeldung mit Fehler {(int)response.StatusCode} beantwortet." + Detail(body));
        }
        var parsed = Parse<TokenReply>(body);
        if (string.IsNullOrEmpty(parsed?.AccessToken)) throw new InvalidOperationException("Der Server hat keinen Zugangstoken geliefert. Stimmt die Adresse?");
        token = parsed.AccessToken;
        tokenUntil = DateTime.UtcNow.AddSeconds(Math.Max(60, parsed.ExpiresIn) - 30);
    }

    public async Task<IReadOnlyList<VaultEntry>> SearchAsync(string text, CancellationToken cancel)
    {
        text = text.Trim();
        if (text.Length < 2) return [];
        var body = await Authorized(HttpMethod.Post, "search", new { Search = text }, cancel);
        var result = Parse<SearchReply>(body);
        return [.. (result?.Credentials ?? []).Take(50).Select(c => new VaultEntry(c.Id ?? "", c.Name ?? "", c.Username ?? "", c.Url ?? "", c.Path ?? "", c.Notes ?? ""))
            .Where(e => e.Id.Length > 0)];
    }

    public async Task<string> GetPasswordAsync(VaultEntry entry, CancellationToken cancel)
    {
        string raw;
        try { raw = await Authorized(HttpMethod.Get, $"credential/{Uri.EscapeDataString(entry.Id)}/password", null, cancel); }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound)
        {
            // Some versions want the password as a POST with a comment for the usage log.
            raw = await Authorized(HttpMethod.Post, $"credential/{Uri.EscapeDataString(entry.Id)}/password", new { comment = "WorkspaceManager" }, cancel);
        }
        raw = raw.Trim();
        // The server answers with a JSON string ("secret") or the bare text.
        if (raw.StartsWith('"')) raw = JsonSerializer.Deserialize<string>(raw) ?? "";
        return raw.Length > 0 ? raw : throw new InvalidOperationException("Der Server hat kein Passwort geliefert.");
    }

    // One call with the token. When it is missing, expired or rejected, the stored login is used once to get a new one.
    async Task<string> Authorized(HttpMethod method, string path, object? payload, CancellationToken cancel)
    {
        if (connection is null) throw new InvalidOperationException("Noch nicht angemeldet.");
        if (token is null || DateTime.UtcNow >= tokenUntil) await ConnectAsync(connection, cancel);
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, Api(path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (payload is not null) request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Send(request, cancel);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0) { await ConnectAsync(connection, cancel); continue; }
            var body = await response.Content.ReadAsStringAsync(cancel);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(response.StatusCode == HttpStatusCode.Forbidden
                    ? "Dafür fehlt dem Benutzer die Berechtigung." + Detail(body)
                    : $"Der Server hat mit Fehler {(int)response.StatusCode} geantwortet." + Detail(body), null, response.StatusCode);
            return body;
        }
    }

    async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken cancel)
    {
        try { return await http.SendAsync(request, cancel); }
        catch (TaskCanceledException) when (!cancel.IsCancellationRequested) { throw new InvalidOperationException("Der Server antwortet nicht (Zeitüberschreitung). Stimmt die Adresse?"); }
        catch (HttpRequestException e) { throw new InvalidOperationException("Der Server ist nicht erreichbar: " + (e.InnerException?.Message ?? e.Message)); }
    }

    static string Detail(string body)
    {
        var parsed = Parse<ErrorReply>(body);
        var text = parsed?.ErrorDescription ?? parsed?.Message ?? parsed?.Error;
        return string.IsNullOrWhiteSpace(text) ? "" : " (" + text.Trim() + ")";
    }

    static T? Parse<T>(string body) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(body, Json); }
        catch (JsonException) { return null; }
    }

    // The server names these fields in snake_case.
    sealed class TokenReply
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    sealed class ErrorReply
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
        public string? Message { get; set; }
    }

    sealed class SearchReply { public List<SearchCredential>? Credentials { get; set; } }

    sealed class SearchCredential
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? Url { get; set; }
        public string? Path { get; set; }
        public string? Notes { get; set; }
    }
}
