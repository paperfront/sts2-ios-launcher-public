using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace STS2Mobile.Platform;

/// <summary>Explicit LAN joins only: private IPv4 or Tailscale addresses, never DNS or public internet hosts.</summary>
public static class LanNetworkPolicy
{
    public const int DefaultPort = 33771;

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
