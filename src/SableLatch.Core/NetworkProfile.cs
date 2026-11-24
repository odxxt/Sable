using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace SableLatch.Core;

public enum NetworkSetting { Keep, Automatic, Manual }

public sealed record NetworkProfile(
    Guid AdapterId, NetworkSetting AddressMode, IPAddress? Address, int PrefixLength,
    IPAddress? Gateway, NetworkSetting DnsMode, IPAddress? PrimaryDns, IPAddress? SecondaryDns,
    bool Review)
{
    public static NetworkProfile Read(VaultItem item)
    {
        if (!Guid.TryParse(item.Field("adapter"), out var adapter) || adapter == Guid.Empty)
            throw new ArgumentException("Choose a network adapter.", "adapter");
        var addressMode = Mode(item.Field("addressMode"), "addressMode");
        var dnsMode = Mode(item.Field("dnsMode"), "dnsMode");
        if (addressMode == NetworkSetting.Keep && dnsMode == NetworkSetting.Keep)
            throw new ArgumentException("Choose a change for IPv4 or DNS.", "addressMode");

        IPAddress? address = null, gateway = null, primary = null, secondary = null;
        var prefix = 0;
        if (addressMode == NetworkSetting.Manual)
        {
            address = Unicast(item.Field("address"), "address");
            prefix = Prefix(item.Field("mask"));
            gateway = OptionalUnicast(item.Field("gateway"), "gateway");
            var mask = uint.MaxValue << (32 - prefix);
            var assigned = Number(address);
            var host = assigned & ~mask;
            if (prefix <= 30 && (host == 0 || host == ~mask))
                throw new ArgumentException("Use a host address within the subnet.", "address");
            if (gateway is not null && (gateway.Equals(address) || (Number(gateway) & mask) != (assigned & mask)
                || prefix <= 30 && ((Number(gateway) & ~mask) == 0 || (Number(gateway) & ~mask) == ~mask)))
                throw new ArgumentException("Use a different host address in this subnet for the gateway.", "gateway");
        }
        if (dnsMode == NetworkSetting.Manual)
        {
            primary = Unicast(item.Field("primaryDns"), "primaryDns");
            secondary = OptionalUnicast(item.Field("secondaryDns"), "secondaryDns");
            if (primary.Equals(secondary)) throw new ArgumentException("Use a different secondary DNS server.", "secondaryDns");
        }
        return new(adapter, addressMode, address, prefix, gateway, dnsMode, primary, secondary,
            item.Field("review") != "false");
    }

    private static NetworkSetting Mode(string value, string field) => value switch
    {
        "keep" => NetworkSetting.Keep,
        "automatic" => NetworkSetting.Automatic,
        "manual" => NetworkSetting.Manual,
        _ => throw new ArgumentException("Choose Keep current, Automatic, or Manual.", field)
    };

    private static bool TryIPv4(string text, out IPAddress address)
    {
        if (IPAddress.TryParse(text, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork
            && parsed.ToString() == text)
        {
            address = parsed;
            return true;
        }
        address = IPAddress.None;
        return false;
    }

    private static IPAddress Unicast(string text, string field)
    {
        if (!TryIPv4(text, out var address) || address.GetAddressBytes()[0] is 0 or 127 or >= 224)
            throw new ArgumentException("Enter a valid IPv4 address.", field);
        return address;
    }

    private static IPAddress? OptionalUnicast(string text, string field) =>
        text.Length == 0 ? null : Unicast(text, field);

    private static int Prefix(string text)
    {
        if (int.TryParse(text.TrimStart('/'), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && number is >= 1 and <= 32 && !text.StartsWith("//", StringComparison.Ordinal)) return number;
        if (TryIPv4(text, out var address))
        {
            var mask = Number(address);
            var host = ~mask;
            if (mask != 0 && (host & (host + 1)) == 0) return System.Numerics.BitOperations.PopCount(mask);
        }
        throw new ArgumentException("Enter a subnet mask such as 255.255.255.0 or /24.", "mask");
    }

    private static uint Number(IPAddress address) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());
}
