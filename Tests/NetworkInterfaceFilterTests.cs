using System;
using System.Linq;
using System.Net.NetworkInformation;
using NUnit.Framework;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Helper;

namespace Tests
{
    /// <summary>
    /// The hidden-interface filter exists to keep loopback, ISATAP and tunnel adapters out of a
    /// user's list. Its side effect is that a virtual machine is built entirely out of blacklisted
    /// descriptions, so the application shows zero interfaces inside the only environment where
    /// changing live DNS settings is safe to test. The escape hatch is what makes that testable.
    ///
    /// The marker is a separate question from the filter. Hiding an adapter is a promise, and the
    /// 2010 blacklist over-keeps it (see the #565 note on TheHiddenByDefaultListHasNotChanged);
    /// decorating one is cosmetic, so it may be wrong. They are therefore two functions with two
    /// standards, and the tests below keep them from silently merging again.
    /// </summary>
    public class NetworkInterfaceFilterTests
    {
        [Test]
        public void AVirtualAdapterIsHiddenByDefault()
        {
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "VMware Virtual Ethernet Adapter #0", "Ethernet 2", allowVirtual: false));
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "VirtualBox Host-Only Ethernet Adapter", "VirtualBox Host-only", allowVirtual: false));
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "TAP-Windows Adapter V9", "tap0", allowVirtual: false));
        }

        [Test]
        public void ABlacklistedNameCountsEvenWhenTheDescriptionDoesNot()
        {
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "Some vendor adapter", "Microsoft ISATAP Tunnel Adapter", allowVirtual: false));
        }

        [Test]
        public void TheGermanAdapterNamesAreFilteredToo()
        {
            // The blacklist carries German spellings because the original author localised them.
            // A user on a German guest would otherwise see adapters an English user never sees.
            Assert.IsTrue(LocalNetworkInterfaceManager.IsFilteredOut(
                "Virtueller Microsoft-Adapter für ISATAP", "ISATAP-Adapter 2", allowVirtual: false));
        }

        [Test]
        public void APhysicalInterfaceIsNeverFiltered()
        {
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(
                "Intel(R) Ethernet Connection (17) I219-V", "Ethernet", allowVirtual: false));
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(
                "Realtek 8822CE Wireless LAN", "Wi-Fi", allowVirtual: false));
        }

        [Test]
        public void TheSameAdapterIsListedWhenVirtualInterfacesAreAllowed()
        {
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(
                "VMware Virtual Ethernet Adapter #0", "Ethernet 2", allowVirtual: true));
        }

        [Test]
        public void MissingOrNullInterfaceTextIsNotTreatedAsABlacklistHit()
        {
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(null!, null!, allowVirtual: false));
            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut("", "  ", allowVirtual: false));
        }

        [Test]
        public void TheEnvironmentVariableNamedInTheDocsIsTheOneThatIsRead()
        {
            const string Name = "SIMPLEDNSCRYPT_ALLOW_VIRTUAL_NICS";

            var before = Environment.GetEnvironmentVariable(Name);

            try
            {
                Environment.SetEnvironmentVariable(Name, null);
                Assert.IsFalse(LocalNetworkInterfaceManager.AllowVirtualNetworkInterfaces,
                    "off unless the variable says otherwise");

                Environment.SetEnvironmentVariable(Name, "1");
                Assert.IsTrue(LocalNetworkInterfaceManager.AllowVirtualNetworkInterfaces);

                Environment.SetEnvironmentVariable(Name, "0");
                Assert.IsFalse(LocalNetworkInterfaceManager.AllowVirtualNetworkInterfaces,
                    "only 1 turns it on, so a stray value cannot silently widen the list");
            }
            finally
            {
                Environment.SetEnvironmentVariable(Name, before);
            }
        }

        [Test]
        public void EveryHiddenAdapterIsAlsoMarked()
        {
            // "Hidden implies marked" is the property that lets the tile carry an honest badge:
            // an adapter the filter swallows is virtual by the application's own definition, so
            // turning "show hidden cards" on must not present it as if it were a plain cable.
            var unmarked = Global.NetworkInterfaceBlacklist
                .Where(entry => !LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(entry, "Ethernet",
                    NetworkInterfaceType.Ethernet))
                .ToList();

            Assert.IsEmpty(unmarked,
                "blacklisted without being marked, so the badge would lie when hidden cards are shown: " +
                string.Join(", ", unmarked));
        }

        [Test]
        public void AnAdapterTypeNetDoesNotNameIsMarked()
        {
            // Kaspersky's VPN adapter reports ifType 53, which has no NetworkInterfaceType member in
            // .NET, so it arrives as a raw number. The interface list must not read that as a cable
            // just because the enum ran out of names — InterfaceTypeToIconConverter already lumps
            // every unrecognised type in with the physical ones, and this is where that stops.
            Assert.IsTrue(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                "Kaspersky VPN", "Local Area Connection", (NetworkInterfaceType)53));

            // The type alone carries the decision; no token in the text is needed.
            Assert.IsTrue(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                "A Vendor Product Name", "Connection 4", (NetworkInterfaceType)53));
        }

        [Test]
        public void TheMarkerCatchesWhatTheBlacklistMisses()
        {
            // The complaint this answers: a modern machine has Hyper-V switches, a Bluetooth PAN and
            // WAN Miniports that the 2010 blacklist never knew, so they appear in the list looking
            // like ordinary cards. Each of these is listed AND marked, which is the whole point —
            // nothing here is hidden, only explained.
            var modern = new[]
            {
                ("Hyper-V Virtual Ethernet Adapter", "vEthernet (WSL (Hyper-V firewall))"),
                ("Hyper-V Virtual Ethernet Adapter #2", "vEthernet (Default Switch)"),
                ("Bluetooth Device (Personal Area Network)", "Bluetooth Network Connection"),
                ("WAN Miniport (IP)", "WAN Miniport (IP)"),
            };

            var missed = modern
                .Where(card => LocalNetworkInterfaceManager.IsFilteredOut(card.Item1, card.Item2, allowVirtual: false))
                .Select(card => card.Item2)
                .ToList();

            Assert.IsEmpty(missed,
                "these are now hidden, which was not the agreed behaviour: " + string.Join(", ", missed));

            foreach (var card in modern)
            {
                Assert.IsTrue(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                    card.Item1, card.Item2, NetworkInterfaceType.Ethernet),
                    $"{card.Item2} shows in the list but is not marked, so it reads as a physical card");
            }
        }

        [Test]
        public void TheMarkerFlagsLoopbackAndTunnelByType()
        {
            // Descriptions here are deliberately vendor-neutral: the type is what decides, so an
            // adapter that renames itself cannot escape the badge.
            Assert.IsTrue(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                "Generic Loopback thing", "Loopback 1", NetworkInterfaceType.Loopback));
            Assert.IsTrue(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                "Generic Tunnel thing", "Tunnel 1", NetworkInterfaceType.Tunnel));
            Assert.IsTrue(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                "Generic Serial thing", "PPPoE", NetworkInterfaceType.Ppp));
        }

        [Test]
        public void APhysicalAdapterIsNeverMarked()
        {
            // Both rows are real adapters on the machine this was developed on. Widening the token
            // list is cheap and tempting; these two are what stops it from eating the cable.
            Assert.IsFalse(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                "Realtek Gaming 2.5GbE Family Controller", "Ethernet", NetworkInterfaceType.Ethernet));
            Assert.IsFalse(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                "RZ616 Wi-Fi 6E 160MHz", "Wi-Fi", NetworkInterfaceType.Wireless80211));
        }

        [Test]
        public void TheMarkerIsCaseInsensitiveButTheBlacklistIsNot()
        {
            // The split is on purpose and has to stay visible. Matching hides things, so it only
            // ever matches the exact spelling the list carries; marking is decoration, so it may
            // read a driver's casing however it likes.
            const string LowercaseVmware = "vmware virtual ethernet adapter";

            Assert.IsFalse(LocalNetworkInterfaceManager.IsFilteredOut(
                LowercaseVmware, "Ethernet 2", allowVirtual: false),
                "the filter grew case-insensitive: it now hides adapters it used to list");
            Assert.IsTrue(LocalNetworkInterfaceManager.LooksLikeVirtualAdapter(
                LowercaseVmware, "Ethernet 2", NetworkInterfaceType.Ethernet));
        }

        [Test]
        public void TheHiddenByDefaultListHasNotChanged()
        {
            // Content, not count: swapping one entry for another keeps the length and still breaks
            // a user's interface list. This is the tripwire for the complaint that the filter eats
            // real cards (DNSCrypt/SimpleDnsCrypt#565, "Wi-Fi card missing from the list"), which
            // is why the fix for modern virtual adapters is a marker and not a longer blacklist.
            // Adding an entry here is a decision someone has to make out loud.
            CollectionAssert.AreEqual(new[]
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
            }, Global.NetworkInterfaceBlacklist,
            "NetworkInterfaceBlacklist changed; see DNSCrypt/SimpleDnsCrypt#565 before accepting this");
        }
    }
}
