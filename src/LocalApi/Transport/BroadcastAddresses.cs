using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MarstekOverlay.LocalApi.Transport;

internal static class BroadcastAddresses
{
    /// <summary>The limited broadcast leaves through one interface only, so each connected IPv4 subnet is also
    /// addressed by its own broadcast address.</summary>
    public static IReadOnlyCollection<IPAddress> Enumerate()
    {
        HashSet<IPAddress> addresses = [IPAddress.Broadcast];
        try
        {
            var subnets = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                    && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Where(unicast => unicast.Address.AddressFamily == AddressFamily.InterNetwork);

            foreach (var subnet in subnets)
                addresses.Add(DirectedBroadcast(subnet.Address, subnet.IPv4Mask));
        }
        catch (NetworkInformationException)
        {
            // The limited broadcast alone still reaches the primary subnet.
        }

        return addresses;
    }

    internal static IPAddress DirectedBroadcast(IPAddress address, IPAddress mask)
    {
        var broadcast = address.GetAddressBytes()
            .Zip(mask.GetAddressBytes(), (addressByte, maskByte) => (byte)(addressByte | ~maskByte))
            .ToArray();

        return new IPAddress(broadcast);
    }
}
