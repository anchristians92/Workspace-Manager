using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WorkspaceManager;

public record AdapterSnapshot(string Id, string Name, bool Physical, bool Active, string[] Addresses, string[] Gateways);
public static class NetworkDiscovery
{
    public static List<AdapterSnapshot> Capture()
    {
        var result = new List<AdapterSnapshot>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                var p = adapter.GetIPProperties();
                var index = p.GetIPv4Properties()?.Index;
                var row = new Native.InterfaceRow { Index = (uint)(index ?? 0) };
                var physical = index.HasValue && Native.GetIfEntry2(ref row) == 0 && (row.Flags & 1) != 0 && (row.Flags & 2) == 0 && row.Tunnel == 0 && row.Type is 6 or 71;
                result.Add(new(adapter.Id, adapter.Name, physical, adapter.OperationalStatus == OperationalStatus.Up,
                    p.UnicastAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork).Select(x => x.Address.ToString()).ToArray(),
                    p.GatewayAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !x.Address.Equals(IPAddress.Any)).Select(x => x.Address.ToString()).ToArray()));
            }
            catch (NetworkInformationException) { /* Adapter may disappear during docking. Fail closed. */ }
        }
        return result;
    }
    public static bool ValidSubnet(string? subnet)
    {
        var parts = subnet?.Split('/');
        return parts?.Length == 2 && IPAddress.TryParse(parts[0], out var ip) && ip.AddressFamily == AddressFamily.InterNetwork && int.TryParse(parts[1], out var bits) && bits is >= 0 and <= 32;
    }
    public static bool Contains(string subnet, string address)
    {
        if (!ValidSubnet(subnet) || !IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var parts = subnet.Split('/'); var network = IPAddress.Parse(parts[0]).GetAddressBytes(); var bytes = ip.GetAddressBytes(); var bits = int.Parse(parts[1]);
        for (var i = 0; i < 4; i++) { var mask = (byte)(0xff << (8 - Math.Clamp(bits - i * 8, 0, 8))); if ((network[i] & mask) != (bytes[i] & mask)) return false; }
        return true;
    }
    public static string? Detect(IEnumerable<LocationRule> rules, IEnumerable<AdapterSnapshot> adapters)
    {
        var physical = adapters.Where(a => a.Active && a.Physical && a.Gateways.Length > 0).ToArray();
        var matches = rules.Where(r => physical.Any(a => a.Addresses.Any(ip => Contains(r.Subnet, ip)) &&
            (string.IsNullOrEmpty(r.Gateway) || a.Gateways.Contains(r.Gateway)) &&
            (string.IsNullOrEmpty(r.AdapterId) || string.Equals(a.Id, r.AdapterId, StringComparison.OrdinalIgnoreCase)))).Select(r => r.Name).Distinct().ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
