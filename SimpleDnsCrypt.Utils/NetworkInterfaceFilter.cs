using System;
using System.Linq;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    ///     The one adapter filter this repository has. It used to exist three times —
    ///     <c>Global.NetworkInterfaceBlacklist</c> for the UI, <c>LocalNetworkInterfaceManager</c> for the
    ///     reads, and a private copy inside <c>Uninstall/Program.cs</c> — which meant the portable and the
    ///     installed channels could disagree about which adapters to restore DNS on. Both now call this.
    /// </summary>
    public static class NetworkInterfaceFilter
    {
        /// <summary>
        ///     Names a 2010 Windows used. Deliberately unchanged in content and size
        ///     (DNSCrypt/SimpleDnsCrypt#565: hiding the wrong adapter costs a user their connection), and
        ///     pinned by <c>Tests/NetworkInterfaceFilterTests.cs</c>.
        /// </summary>
        public static readonly string[] Blacklist =
        {
            "Microsoft Virtual",
            "Hamachi Network",
            "VMware Virtual",
            "VirtualBox",
            "Software Loopback",
            "Microsoft ISATAP",
            "Microsoft-ISATAP",
            "Teredo Tunneling Pseudo-Interface",
            "Microsoft Wi-Fi Direct Virtual",
            "Microsoft Teredo Tunneling Adapter",
            "Von Microsoft gehosteter",
            "Microsoft hosted",
            "Virtueller Microsoft-Adapter",
            "TAP"
        };

        /// <summary>
        ///     Tokens that mark an adapter as virtual, tunnel or loopback without hiding it. Wider than
        ///     <see cref="Blacklist"/> on purpose: a Hyper-V switch, a Wintun VPN and a Bluetooth PAN are
        ///     all operational today and none of them appear in the blacklist, so they arrive in the list
        ///     wearing a cable icon. Marking wrongly costs an icon; hiding wrongly costs a connection.
        /// </summary>
        public static readonly string[] VirtualMarkers =
        {
            "Hyper-V",
            "vEthernet",
            "WSL",
            "Virtual",
            "Virtueller",
            "gehosteter",
            "hosted",
            "Loopback",
            "ISATAP",
            "Teredo",
            "WAN Miniport",
            "Tunnel",
            "VPN",
            "Hamachi",
            "VMware",
            "VirtualBox",
            "TAP",
            "Wintun",
            "Tailscale",
            "WireGuard",
            "Bluetooth",
            "Wi-Fi Direct"
        };

        /// <summary>
        ///     Whether the adapter must not be listed or restored at all. Matched against both the
        ///     interface name and the description, because a blacklist token can appear in either
        ///     depending on the driver and the Windows language.
        /// </summary>
        public static bool IsHidden(string? name, string? description)
        {
            var text = (description ?? string.Empty) + "\n" + (name ?? string.Empty);

            return Blacklist.Any(entry => text.Contains(entry, StringComparison.Ordinal));
        }
    }
}
