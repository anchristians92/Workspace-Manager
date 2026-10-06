using WorkspaceManager.Addon.Remote;

// Small test runner for the logic of the Fernwartung addon (IDs, device names, command lines, settings file).
// Run: dotnet run --project tests\Remote.Tests    (exit code 0 = all passed)

var failures = 0;
void Test(string name, Action body)
{
    try { body(); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL {name}: {e.Message}"); }
}
void Assert(bool condition, string? message = null) { if (!condition) throw new InvalidOperationException(message ?? "Assertion failed"); }

var tv = RemoteTool.TeamViewer; var any = RemoteTool.AnyDesk; var rust = RemoteTool.RustDesk;

Test("TeamViewer IDs", () =>
{
    Assert(tv.NormalizeId("123 456 789") == "123456789");
    Assert(tv.NormalizeId(" 1234567890 ") == "1234567890");
    Assert(tv.NormalizeId("123-456-789") == "123456789");
    Assert(tv.NormalizeId("12345") is null);
    Assert(tv.NormalizeId("") is null);
});

Test("TeamViewer accepts DNS names and IP addresses", () =>
{
    Assert(tv.NormalizeId("pc01.firma.local") == "pc01.firma.local");
    Assert(tv.NormalizeId("  PC-Kasse1  ") == "pc-kasse1");
    Assert(tv.NormalizeId("192.168.1.20") == "192.168.1.20");
    Assert(tv.NormalizeId("300.1.1.1") is null, "not an IPv4 address");
    Assert(tv.NormalizeId("12345") is null, "short digit strings are typos, not host names");
    Assert(tv.NormalizeId("bad name") is null);
    Assert(tv.NormalizeId("-bad.local") is null);
    Assert(tv.NormalizeId("bad-.local") is null);
    Assert(tv.NormalizeId("server..local") is null);
    Assert(any.NormalizeId("pc01.firma.local") is null && rust.NormalizeId("pc01.firma.local") is null, "only TeamViewer takes host names");
    Assert(tv.Resolve([new DeviceEntry { Name = "Server", RemoteId = "SRV01.firma.local" }], "srv01.FIRMA.local") is { Id: "srv01.firma.local", Device.Name: "Server" });
    Assert(tv.BuildCommand(@"C:\TV\TeamViewer.exe", "pc01.firma.local", "pw").Start.ArgumentList.SequenceEqual(["-i", "pc01.firma.local", "--Password", "pw"]));
});

Test("AnyDesk IDs and aliases", () =>
{
    Assert(any.NormalizeId("987 654 321") == "987654321");
    Assert(any.NormalizeId("support@ad") == "support@ad");
    Assert(any.NormalizeId("not valid@ad") is null);
    Assert(any.NormalizeId("abc") is null);
});

Test("RustDesk IDs", () =>
{
    Assert(rust.NormalizeId("123456789") == "123456789");
    Assert(rust.NormalizeId("mein-pc_01") == "mein-pc_01");
    Assert(rust.NormalizeId("a b") is null);
});

Test("Device names win over IDs and are case-insensitive", () =>
{
    var devices = new[] { new DeviceEntry { Name = "Kasse 1", RemoteId = "111 222 333" }, new DeviceEntry { Name = "Server", RemoteId = "444555666" } };
    Assert(tv.Resolve(devices, "kasse 1") is { Id: "111222333", Device.Name: "Kasse 1" });
    Assert(tv.Resolve(devices, "444 555 666") is { Id: "444555666", Device.Name: "Server" });   // typed ID of a saved device
    Assert(tv.Resolve(devices, "777888999") is { Id: "777888999", Device: null });             // unknown but valid ID
    Assert(tv.Resolve(devices, "gibt es nicht") is null);
    Assert(tv.Resolve(devices, "   ") is null);
});

Test("Command lines", () =>
{
    var withPassword = tv.BuildCommand(@"C:\TV\TeamViewer.exe", "123456789", "p\"w d&1");
    Assert(withPassword.Start.ArgumentList.SequenceEqual(["-i", "123456789", "--Password", "p\"w d&1"]), "TeamViewer arguments");
    Assert(withPassword.StandardInput is null);
    Assert(tv.BuildCommand(@"C:\TV\TeamViewer.exe", "123456789", null).Start.ArgumentList.SequenceEqual(["-i", "123456789"]));

    var anyDesk = any.BuildCommand(@"C:\AD\AnyDesk.exe", "987654321", "geheim");
    Assert(anyDesk.Start.ArgumentList.SequenceEqual(["987654321", "--with-password"]), "AnyDesk arguments");
    Assert(anyDesk.Start.RedirectStandardInput && anyDesk.StandardInput == "geheim", "AnyDesk password goes through stdin");
    Assert(!any.BuildCommand(@"C:\AD\AnyDesk.exe", "987654321", null).Start.RedirectStandardInput);

    var rustDesk = rust.BuildCommand(@"C:\RD\rustdesk.exe", "123456789", "pw");
    Assert(rustDesk.Start.ArgumentList.SequenceEqual(["--connect", "123456789", "--password", "pw"]), "RustDesk arguments");
    Assert(!(tv.BuildCommand(@"C:\x.exe", "123456789", "").Start.ArgumentList.Contains("--Password")), "empty password is not passed");
});

Test("Default password and per-device password", () =>
{
    var settings = new ToolSettings();
    var a = new PasswordProfile { Name = "Lokal", IsDefault = true }; var b = new PasswordProfile { Name = "Kunde" };
    settings.Passwords.AddRange([a, b]);
    Assert(settings.DefaultPassword == a);
    Assert(settings.PasswordFor(null) == a);
    Assert(settings.PasswordFor(new DeviceEntry { PasswordId = b.Id }) == b);
    Assert(settings.PasswordFor(new DeviceEntry { PasswordId = "weg" }) == a);   // deleted profile falls back to the default
    a.IsDefault = false;
    Assert(settings.DefaultPassword == a, "first one when none is marked");
});

Test("Settings file round trip keeps no passwords", () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "WorkspaceManager-RemoteTest-" + Guid.NewGuid().ToString("N"));
    try
    {
        var store = RemoteStore.Load(folder);
        store.Settings.Hotkey = "Strg+Alt+R"; store.Settings.DefaultTool = "anydesk";
        var tools = store.Settings.For("anydesk");
        tools.Passwords.Add(new PasswordProfile { Name = "Lokal", IsDefault = true });
        tools.Devices.Add(new DeviceEntry { Name = "Kasse", RemoteId = "987654321" });
        store.Save();
        var text = File.ReadAllText(Path.Combine(folder, "remote.json"));
        Assert(!text.Contains("geheim") && text.Contains("Kasse"));
        var again = RemoteStore.Load(folder);
        Assert(again.Settings.Hotkey == "Strg+Alt+R" && again.Settings.For("anydesk").Devices.Single().RemoteId == "987654321");
        File.WriteAllText(Path.Combine(folder, "remote.json"), "{ kaputt");
        Assert(RemoteStore.Load(folder).Settings.Hotkey == "", "a broken file starts empty");
        Assert(File.Exists(Path.Combine(folder, "remote.json.broken")), "the broken file is kept aside");
    }
    finally { try { Directory.Delete(folder, true); } catch (IOException) { } }
});

Test("Executable lookup prefers the configured path", () =>
{
    var file = Path.Combine(Path.GetTempPath(), "fake-" + Guid.NewGuid().ToString("N") + ".exe");
    File.WriteAllText(file, "");
    try
    {
        Assert(tv.FindExecutable(file) == file);
        Assert(tv.FindExecutable(@"C:\gibt\es\nicht.exe") != @"C:\gibt\es\nicht.exe");
    }
    finally { File.Delete(file); }
});

Test("Offered programs", () =>
{
    var settings = new RemoteSettings { EnabledTools = ["rustdesk", "anydesk"] };
    Assert(settings.ActiveTools().Select(t => t.Id).SequenceEqual(["anydesk", "rustdesk"]), "keeps the fixed order, ignores the order of the setting");
    settings.DefaultTool = "teamviewer";
    Assert(settings.StartTool().Id == "anydesk", "default not offered: first offered program");
    settings.DefaultTool = "rustdesk";
    Assert(settings.StartTool().Id == "rustdesk");
    settings.EnabledTools = ["teamviewer"];
    Assert(settings.ActiveTools().Count == 1 && settings.ActiveTools()[0].Id == "teamviewer");
    settings.EnabledTools = ["gibt-es-nicht"];
    Assert(settings.ActiveTools().Count >= 1 && settings.ActiveTools().All(t => RemoteTool.All.Contains(t)), "unknown ids fall back to detection");
    settings.EnabledTools = [];
    Assert(settings.ActiveTools().Count >= 1, "empty list falls back to detection");
});

Console.WriteLine(failures == 0 ? "\nAll tests passed." : $"\n{failures} test(s) failed.");
return failures == 0 ? 0 : 1;
