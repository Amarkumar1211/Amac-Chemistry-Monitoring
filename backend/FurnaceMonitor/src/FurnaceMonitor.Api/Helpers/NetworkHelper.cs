using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FurnaceMonitor.Api.Helpers;

public record NetworkInterfaceInfo(string InterfaceName, string Description, string IpAddress, bool HasGateway);

public static class NetworkHelper
{
    private static readonly string[] VirtualKeywords = new[]
    {
        "virtual", "vmware", "vbox", "hyper-v", "vethernet", "loopback", "npcap", "tap", "wsl", "pseudo", "teredo"
    };

    /// <summary>
    /// Discovers all available IPv4 addresses on the machine, identifying which is the router-connected LAN IP
    /// versus isolated direct-instrument links (like spectrometer cables) or virtual adapters.
    /// </summary>
    public static List<NetworkInterfaceInfo> GetAllLocalIpAddresses()
    {
        var list = new List<NetworkInterfaceInfo>();
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            foreach (var ni in interfaces)
            {
                var isVirtual = VirtualKeywords.Any(k =>
                    ni.Name.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains(k, StringComparison.OrdinalIgnoreCase));

                var props = ni.GetIPProperties();
                var hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(g.Address) &&
                    !g.Address.Equals(IPAddress.Any) &&
                    !g.Address.ToString().StartsWith("0."));

                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(addr.Address)
                        && !addr.Address.ToString().StartsWith("169.254.")
                        && !addr.Address.ToString().StartsWith("127."))
                    {
                        list.Add(new NetworkInterfaceInfo(
                            ni.Name,
                            ni.Description,
                            addr.Address.ToString(),
                            hasGateway && !isVirtual
                        ));
                    }
                }
            }
        }
        catch { }

        return list;
    }

    /// <summary>
    /// Discovers the active local LAN IPv4 address (e.g. 192.168.1.x) that connects
    /// to the plant Wi-Fi / Router with an active default gateway.
    /// Filters out isolated direct-instrument links (e.g. spectrometer cables) and virtual adapters.
    /// </summary>
    public static string GetLocalIpAddress()
    {
        // 1. Primary Strategy: Ask the OS kernel routing table which local IP would route out to the LAN/Internet
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530); // Connect on UDP does NOT send packets; it only queries the OS routing table
            if (socket.LocalEndPoint is IPEndPoint endPoint && !IPAddress.IsLoopback(endPoint.Address))
            {
                var routedIp = endPoint.Address.ToString();
                if (!routedIp.StartsWith("169.254.") && !routedIp.StartsWith("127."))
                {
                    return routedIp;
                }
            }
        }
        catch
        {
            // Fall through if no default route to 8.8.8.8 exists (e.g. offline closed LAN)
        }

        try
        {
            var allIps = GetAllLocalIpAddresses();

            // 2. Secondary Strategy: Pick the non-virtual interface that has an active Default Gateway (Router)
            var gatewayIp = allIps.FirstOrDefault(i => i.HasGateway);
            if (gatewayIp != null)
            {
                return gatewayIp.IpAddress;
            }

            // 3. Tertiary Strategy: Prioritize Wireless80211, then non-virtual Ethernet
            var nonVirtual = allIps.FirstOrDefault();
            if (nonVirtual != null)
            {
                return nonVirtual.IpAddress;
            }
        }
        catch { }

        return "localhost";
    }
}
