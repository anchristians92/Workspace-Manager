using System.Net;
using System.Text;
using WorkspaceManager.Addon.Vault;

// Small test runner for the logic of the Passwort-Tresor addon (server address, Pleasant requests and answers, settings file).
// The server is simulated, so no real Pleasant Password Server is needed.
// Run: dotnet run --project tests\Vault.Tests    (exit code 0 = all passed)

var failures = 0;
void Test(string name, Func<Task> body)
{
    try { body().GetAwaiter().GetResult(); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Assert(bool condition, string? message = null) { if (!condition) throw new InvalidOperationException(message ?? "Assertion failed"); }

Test("Server address", () =>
{
    Assert(PleasantProvider.NormalizeServer("pleasant.firma.local:10001") == "https://pleasant.firma.local:10001");
    Assert(PleasantProvider.NormalizeServer(" https://pleasant.firma.local:10001/ ") == "https://pleasant.firma.local:10001");
    Assert(PleasantProvider.NormalizeServer("https://pw.firma.local/pleasant/") == "https://pw.firma.local", "only host and port are used");
    foreach (var bad in new[] { "", "   ", "ftp://x", "https://" })
    {
        try { PleasantProvider.NormalizeServer(bad); throw new InvalidOperationException($"'{bad}' should be rejected"); }
        catch (InvalidDataException) { }
    }
    return Task.CompletedTask;
});

Test("Login, search and password", async () =>
{
    var server = new FakeServer();
    var provider = new PleasantProvider(server);
    await provider.ConnectAsync(new("pw.firma.local:10001", "benutzer", "geheim"), default);
    Assert(server.TokenForm == "grant_type=password&username=benutzer&password=geheim", "form body: " + server.TokenForm);
    var found = await provider.SearchAsync("router", default);
    Assert(found.Count == 1 && found[0] is { Id: "id-1", Name: "Router Büro", Username: "admin", Path: "Netz/Büro", Url: "router.firma.local", Notes: "Nur intern" }, "search result");
    Assert(server.SearchBody == "{\"Search\":\"router\"}", "search body: " + server.SearchBody);
    Assert(server.LastAuthorization == "Bearer token-1", "bearer token");
    Assert(await provider.GetPasswordAsync(found[0], default) == "s3cret", "password from a quoted JSON string");
    Assert((await provider.SearchAsync("x", default)).Count == 0 && server.SearchCalls == 1, "one character: no request");
});

Test("Expired token is renewed", async () =>
{
    var server = new FakeServer { RejectNextRequest = true };
    var provider = new PleasantProvider(server);
    await provider.ConnectAsync(new("https://pw.firma.local", "u", "p"), default);
    var found = await provider.SearchAsync("router", default);
    Assert(found.Count == 1 && server.TokenCalls == 2, $"logged in again after 401 (token calls: {server.TokenCalls})");
    Assert(server.LastAuthorization == "Bearer token-2");
});

Test("Wrong password", async () =>
{
    var provider = new PleasantProvider(new FakeServer { WrongPassword = true });
    try { await provider.ConnectAsync(new("https://pw.firma.local", "u", "falsch"), default); throw new InvalidOperationException("should fail"); }
    catch (InvalidOperationException e) { Assert(e.Message.Contains("Anmeldung abgelehnt") && e.Message.Contains("invalid_grant"), e.Message); }
});

Test("Two-factor server is recognized", async () =>
{
    var provider = new PleasantProvider(new FakeServer { RequireOtp = true });
    try { await provider.ConnectAsync(new("https://pw.firma.local", "u", "p"), default); throw new InvalidOperationException("should fail"); }
    catch (InvalidOperationException e) { Assert(e.Message.Contains("zweiten Faktor"), e.Message); }
});

Test("Password with POST fallback and bare text", async () =>
{
    var server = new FakeServer { PasswordNeedsPost = true, BarePassword = true };
    var provider = new PleasantProvider(server);
    await provider.ConnectAsync(new("https://pw.firma.local", "u", "p"), default);
    var entry = new VaultEntry("id-1", "Router", "admin", "", "");
    Assert(await provider.GetPasswordAsync(entry, default) == "s3cret" && server.PasswordPosts == 1);
});

Test("Server not reachable gives a readable message", async () =>
{
    var provider = new PleasantProvider(new FakeServer { Unreachable = true });
    try { await provider.ConnectAsync(new("https://pw.firma.local", "u", "p"), default); throw new InvalidOperationException("should fail"); }
    catch (InvalidOperationException e) { Assert(e.Message.StartsWith("Der Server ist nicht erreichbar"), e.Message); }
});

Test("Settings file and credentials", () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "vault-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = VaultStore.Load(folder);
        Assert(store.Settings.Provider == "pleasant" && store.Settings.ClearClipboardSeconds == 20, "defaults");
        store.Settings.Server = "https://pw.firma.local"; store.Settings.ClearClipboardSeconds = 9999; store.Save();
        var again = VaultStore.Load(folder);
        Assert(again.Settings.Server == "https://pw.firma.local" && again.Settings.ClearClipboardSeconds == 300, "saved and limited");
        Assert(!File.ReadAllText(Path.Combine(folder, "vault.json")).Contains("geheim"), "no password in the file");
        File.WriteAllText(Path.Combine(folder, "vault.json"), "{ kaputt");
        Assert(VaultStore.Load(folder).Settings.Server == "" && File.Exists(Path.Combine(folder, "vault.json.broken")), "damaged file is kept aside");
    }
    finally { try { Directory.Delete(folder, true); } catch (IOException) { } }
    return Task.CompletedTask;
});

Test("Service without setup", async () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "vault-test-" + Guid.NewGuid().ToString("N"));
    try
    {
        var service = new VaultService(VaultStore.Load(folder));
        Assert(!service.Configured);
        try { await service.SearchAsync("router"); throw new InvalidOperationException("should fail"); }
        catch (InvalidOperationException e) { Assert(e.Message.Contains("Noch nicht eingerichtet"), e.Message); }
    }
    finally { try { Directory.Delete(folder, true); } catch (IOException) { } }
});

Test("Website and note", () =>
{
    var entry = new VaultEntry("1", "Router", "admin", "router.firma.local", "Root/Netz/Büro/", "Zugang nur intern.\nPasswort läuft ab.");
    var text = SearchPanel.Details(entry);
    Assert(text == "Ordner: Root/Netz/Büro\r\n\r\nBemerkung:\r\nZugang nur intern.\r\nPasswort läuft ab.", text);
    Assert(SearchPanel.Details(new VaultEntry("2", "Leer", "", "", "")).Contains("Keine Bemerkung"));
    Assert(SearchPanel.WebAddress("router.firma.local")?.AbsoluteUri == "https://router.firma.local/");
    Assert(SearchPanel.WebAddress("http://10.0.0.1:8080/admin")?.Port == 8080);
    foreach (var bad in new[] { "", "C:\\Windows\\System32\\cmd.exe", "file:///C:/x", "javascript:alert(1)", "ssh://host", "zwei Wörter" })
        Assert(SearchPanel.WebAddress(bad) is null, $"'{bad}' must not be opened");
    return Task.CompletedTask;
});

Test("Result list draws and keeps the selection in view", () =>
{
    Assert(ResultList.ShortPath("Root/Firma/Support/Clients/") == "Support  ›  Clients");
    Assert(ResultList.ShortPath("Root/") == "" && ResultList.ShortPath("Netz/Büro") == "Netz  ›  Büro");
    using var list = new ResultList { Size = new Size(700, 330) };
    list.CreateControl();
    list.SetEntries([.. Enumerable.Range(1, 12).Select(i => new VaultEntry("id" + i, i == 1 ? "Virenschutz" : "Eintrag " + i, i % 3 == 0 ? "" : "benutzer" + i, "", "Root/Firma/Support/Clients/"))]);
    Assert(list.Count == 12 && list.SelectedIndex == 0 && list.Selected?.Name == "Virenschutz");
    list.SelectedIndex = 11;
    Assert(list.SelectedIndex == 11, "selection moves");
    list.SetEntries([]);
    Assert(list.SelectedIndex == -1 && list.Selected is null);
    list.SetEntries([new("a", "Router", "admin", "", "Root/Netz/")]);
    using var bitmap = new Bitmap(list.Width, list.Height);
    list.DrawToBitmap(bitmap, new Rectangle(Point.Empty, list.Size));
    var colors = new HashSet<int>(); for (var y = 0; y < bitmap.Height; y += 7) for (var x = 0; x < bitmap.Width; x += 7) colors.Add(bitmap.GetPixel(x, y).ToArgb());
    Assert(colors.Count > 4, "something was drawn");
    var render = Array.IndexOf(args, "--render");
    if (render >= 0)
    {
        list.SetEntries([.. Enumerable.Range(1, 12).Select(i => new VaultEntry("id" + i, new[] { "Virenschutz", "Datenbank Server 1", "Verwaltungsserver", "Datenbank Leser", "Suchdienst", "Mailkonto Verwaltung" }[i % 6], new[] { "benutzer1", "benutzer2", "benutzer3", "benutzer4", "benutzer5", "benutzer6" }[i % 6], "", "Root/Firma/Support/Clients/"))]);
        list.SelectedIndex = 0;
        using var shot = new Bitmap(list.Width, list.Height); list.DrawToBitmap(shot, new Rectangle(Point.Empty, list.Size)); shot.Save(args[render + 1]);
    }
    return Task.CompletedTask;
});

var renderForm = Array.IndexOf(args, "--render-form");
if (renderForm >= 0)
{
    // The whole search window with one example entry (to look at the layout; the server is not involved).
    var folder = Path.Combine(Path.GetTempPath(), "vault-shot-" + Guid.NewGuid().ToString("N"));
    var store = VaultStore.Load(folder);
    using var form = new Form();
    WorkspaceManager.Ui.Prepare(form, "Passwort-Tresor", new Size(580, 820));
    var body = WorkspaceManager.Ui.ScrollContent(form);
    WorkspaceManager.Ui.Paragraph(body, "Passwort-Tresor", 18, true);
    var panel = new SearchPanel(new VaultService(store), store, body, () => 0, null);
    WorkspaceManager.Ui.Finish(form);
    form.Show(); Application.DoEvents();
    IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(All(x)));
    All(form).OfType<ResultList>().Single().SetEntries([new("1", "Router Büro", "admin", "router.firma.local", "Root/Firma/Netz/Büro/", "Nur intern erreichbar.\nPasswort läuft im Dezember ab.\nAnsprechpartner: Herr Beispiel")]);
    Application.DoEvents();
    using var shot = new Bitmap(form.Width, form.Height); form.DrawToBitmap(shot, new Rectangle(Point.Empty, form.Size)); shot.Save(args[renderForm + 1]);
}

Console.WriteLine(failures == 0 ? "\nAll tests passed." : $"\n{failures} test(s) failed.");
return failures == 0 ? 0 : 1;

// A Pleasant server in a few lines.
sealed class FakeServer : HttpMessageHandler
{
    public string TokenForm = "", SearchBody = "", LastAuthorization = "";
    public int TokenCalls, SearchCalls, PasswordPosts;
    public bool RejectNextRequest, WrongPassword, RequireOtp, PasswordNeedsPost, BarePassword, Unreachable;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel)
    {
        if (Unreachable) throw new HttpRequestException("kein Netz");
        var path = request.RequestUri!.AbsolutePath;
        if (path.EndsWith("/OAuth2/Token", StringComparison.OrdinalIgnoreCase))
        {
            TokenCalls++;
            TokenForm = await request.Content!.ReadAsStringAsync(cancel);
            if (RequireOtp) { var r = Json(HttpStatusCode.BadRequest, "{\"error\":\"otp_required\"}"); r.Headers.Add("X-Pleasant-OTP", "required"); return r; }
            if (WrongPassword) return Json(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\",\"error_description\":\"invalid_grant\"}");
            return Json(HttpStatusCode.OK, $"{{\"access_token\":\"token-{TokenCalls}\",\"token_type\":\"bearer\",\"expires_in\":3600}}");
        }
        LastAuthorization = request.Headers.Authorization?.ToString() ?? "";
        if (RejectNextRequest) { RejectNextRequest = false; return Json(HttpStatusCode.Unauthorized, "{}"); }
        if (path.EndsWith("/rest/search", StringComparison.OrdinalIgnoreCase))
        {
            SearchCalls++;
            SearchBody = await request.Content!.ReadAsStringAsync(cancel);
            return Json(HttpStatusCode.OK, "{\"Credentials\":[{\"Id\":\"id-1\",\"Name\":\"Router Büro\",\"Username\":\"admin\",\"Url\":\"router.firma.local\",\"Notes\":\"Nur intern\",\"GroupId\":\"g\",\"Path\":\"Netz/Büro\"}],\"Groups\":[]}");
        }
        if (path.EndsWith("/password", StringComparison.OrdinalIgnoreCase))
        {
            if (PasswordNeedsPost && request.Method == HttpMethod.Get) return Json(HttpStatusCode.MethodNotAllowed, "{}");
            if (request.Method == HttpMethod.Post) PasswordPosts++;
            return BarePassword ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("s3cret") } : Json(HttpStatusCode.OK, "\"s3cret\"");
        }
        return Json(HttpStatusCode.NotFound, "{}");
    }

    static HttpResponseMessage Json(HttpStatusCode code, string body) => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
