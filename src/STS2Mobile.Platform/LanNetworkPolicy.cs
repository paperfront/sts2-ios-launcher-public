using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace STS2Mobile.Platform;

/// <summary>Explicit LAN hosts and peers: private IPv4 or Tailscale addresses, never DNS or public internet hosts.</summary>
public static class LanNetworkPolicy
{
    public const int DefaultPort = 33771;

    private static string? _hostAddress;
    public static string? HostAddress => Volatile.Read(ref _hostAddress);

    /// <summary>Select an interface for the next native Host/Load action. This does not open a socket.</summary>
    public static void SelectHostAddress(string address)
    {
        RequirePeer(address, DefaultPort);
        Volatile.Write(ref _hostAddress, address);
    }

    /// <summary>Replace the game's wildcard bind with the selected LAN/Tailscale interface.</summary>
    public static string ResolveListenAddress(string requested, int port)
    {
        string selected = HostAddress ?? throw new NotSupportedException("Choose this iPhone's LAN host address before hosting.");
        RequirePeer(selected, port);
        if (port != DefaultPort || requested != "*" && requested != selected)
            throw new NotSupportedException("LAN hosting uses the selected Wi-Fi or Tailscale address on UDP port 33771.");
        return selected;
    }

    /// <summary>Keep the native Host and Load flows on ENet, without changing other command-line switches.</summary>
    public static bool IsFastMultiplayerArgument(string argument) => argument == "fastmp";

    public static bool IsAllowedAddress(string? host)
    {
        if (string.IsNullOrEmpty(host) || host != host.Trim() || host.Contains('%') || !IPAddress.TryParse(host, out var address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            // Require ordinary dotted decimal; IPAddress also accepts ambiguous abbreviated and numeric forms.
            if (address.ToString() != host) return false;
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
        }
        // Tailscale's IPv6 prefix. Other IPv6 ranges need a separately reviewed policy.
        byte[] ip = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetworkV6 && ip[0] == 0xfd && ip[1] == 0x7a &&
            ip[2] == 0x11 && ip[3] == 0x5c && ip[4] == 0xa1 && ip[5] == 0xe0;
    }

    public static void RequirePeer(string host, int port)
    {
        if (!IsAllowedAddress(host) || port is < 1 or > 65535)
            throw new NotSupportedException("Enter a private Wi-Fi or Tailscale IP address and a port from 1 to 65535.");
    }

    /// <summary>IPv4[:port] or [Tailscale IPv6][:port]. No DNS resolution or discovery traffic.</summary>
    public static bool TryParse(string? input, out string host, out int port)
    {
        host = "";
        port = DefaultPort;
        if (string.IsNullOrWhiteSpace(input)) return false;
        string value = input.Trim();
        string? portText = null;
        if (value.StartsWith('['))
        {
            int closing = value.IndexOf(']');
            if (closing < 0) return false;
            host = value[1..closing];
            string tail = value[(closing + 1)..];
            if (tail.Length > 0)
            {
                if (!tail.StartsWith(':')) return false;
                portText = tail[1..];
            }
        }
        else
        {
            int colon = value.IndexOf(':');
            if (colon >= 0 && colon == value.LastIndexOf(':'))
            {
                host = value[..colon];
                portText = value[(colon + 1)..];
            }
            else host = value;
        }
        if (portText != null && (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535)) return false;
        return IsAllowedAddress(host);
    }
}
