using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SimpleDnsCrypt.Utils;

namespace Tests
{
    /// <summary>
    ///     Uninstall.exe used to keep its own third copy of the adapter filter and used to enumerate only
    ///     interfaces that were <c>OperationalStatus.Up</c>, so a card that was unplugged, disabled, or a
    ///     VPN that was not connected at the moment of uninstall kept the static DNS servers the
    ///     application had written to it — the one failure mode <c>docs/testing/dns-safety.md</c> is
    ///     written around, because the next time that card comes up it still points at a proxy that is
    ///     gone.
    ///     <para>
    ///         These tests pin the replacement: one filter shared by both channels, one snapshot of the
    ///         pre-existing DNS state taken immediately before the first write, and a restore that walks
    ///         every adapter the snapshot mentions regardless of whether it is up — and never touches the
    ///         adapter's own enable state.
    ///     </para>
    /// </summary>
    [TestFixture]
    public class InterfaceDnsSnapshotTests
    {
        private string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "sdc-snapshot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        private static AdapterDnsState Card(string name, bool up = true, params string[] servers) =>
            new AdapterDnsState
            {
                Name = name,
                Description = name + " Adapter",
                IsOperational = up,
                Ipv4Servers = servers,
                Ipv6Servers = Array.Empty<string>()
            };

        // ---- one filter, not three ----

        [Test]
        public void SharedFilterStillHidesTheFourteenBlacklistedNames()
        {
            Assert.That(NetworkInterfaceFilter.Blacklist.Length, Is.EqualTo(14),
                "the blacklist size is pinned by DNSCrypt/SimpleDnsCrypt#565; changing it changes which " +
                "adapters a user can see at all");

            Assert.That(NetworkInterfaceFilter.IsHidden("eth0", "Software Loopback Interface 1"), Is.True);
            Assert.That(NetworkInterfaceFilter.IsHidden("Ethernet", "Realtek PCIe GbE Family Controller"), Is.False);
        }

        [Test]
        public void TheApplicationAndTheUninstallerReadTheSameList()
        {
            // Global keeps its name for the app's own call sites, but the content must be the shared
            // array — that is what makes it impossible for the portable and installed channels to
            // disagree about which adapters to restore.
            Assert.That(SimpleDnsCrypt.Config.Global.NetworkInterfaceBlacklist,
                Is.SameAs(NetworkInterfaceFilter.Blacklist),
                "Global carries its own copy again, which is the third-copy defect this change removes");
        }

        // ---- one snapshot, taken before the first write ----

        [Test]
        public void FirstCaptureIsTheOneThatCounts_ALaterCaptureChangesNothing()
        {
            var captured = InterfaceDnsSnapshot.Capture(root, new[]
            {
                Card("Ethernet", true, "192.168.1.1")
            });

            Assert.That(captured, Is.True);

            var second = InterfaceDnsSnapshot.Capture(root, new[]
            {
                Card("Ethernet", true, "127.0.0.1")     // what the app has just written
            });

            Assert.That(second, Is.False, "a second snapshot must not overwrite the pre-change state");

            var loaded = InterfaceDnsSnapshot.Load(root);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Adapter("Ethernet").Ipv4Servers, Is.EquivalentTo(new[] { "192.168.1.1" }),
                "the restore target has to be what was there BEFORE the application touched the card");
        }

        // ---- restore covers down adapters, and never enables anything ----

        [Test]
        public void RestoreCommands_AreProducedForADownAdapter()
        {
            InterfaceDnsSnapshot.Capture(root, new[]
            {
                Card("Ethernet", true, "192.168.1.1"),
                Card("Wi-Fi", false, "192.168.2.1")     // unplugged / disconnected
            });

            // At uninstall time the Wi-Fi card is still down. The old code's Up-only filter dropped it.
            var commands = InterfaceDnsSnapshot.BuildRestoreCommands(root, new[]
            {
                Card("Ethernet", true, "127.0.0.1"),
                Card("Wi-Fi", false, "127.0.0.1")
            });

            Assert.That(commands.Any(c => c.Contains("\"Wi-Fi\"")), Is.True,
                "a disconnected adapter was skipped, so it keeps pointing at a proxy that is gone");
        }

        [Test]
        public void RestoreCommands_NeverTryToEnableAnAdapter()
        {
            InterfaceDnsSnapshot.Capture(root, new[] { Card("Wi-Fi", false, "192.168.2.1") });

            var commands = InterfaceDnsSnapshot.BuildRestoreCommands(root, new[] { Card("Wi-Fi", false, "127.0.0.1") });

            Assert.That(commands, Is.Not.Empty);
            Assert.That(commands.Where(c => c.Contains("enable") || c.Contains("set interface")
                        || c.Contains("Start-Service") || c.Contains("net start")),
                Is.Empty,
                "restoring DNS must not wake, enable or otherwise change the state of the adapter itself");
        }

        [Test]
        public void AdapterThatHeldUserServers_GetsThoseServersBack()
        {
            InterfaceDnsSnapshot.Capture(root, new[] { Card("Ethernet", true, "192.168.1.1", "192.168.1.2") });

            var commands = InterfaceDnsSnapshot.BuildRestoreCommands(root, new[] { Card("Ethernet", true, "127.0.0.1") });

            var text = string.Join("\n", commands);
            Assert.That(text, Does.Contain("192.168.1.1"));
            Assert.That(text, Does.Contain("192.168.1.2"));
            Assert.That(commands.Count(c => c.Contains("dnsservers") && c.Contains("\"Ethernet\"")),
                Is.EqualTo(2), "one set for the primary and one add for the second, not two sets");
        }

        [Test]
        public void AdapterThatWasOnDhcp_HasTheStaticEntriesDeleted()
        {
            InterfaceDnsSnapshot.Capture(root, new[] { Card("Ethernet", true) });     // no servers recorded

            var commands = InterfaceDnsSnapshot.BuildRestoreCommands(root, new[] { Card("Ethernet", true, "127.0.0.1") });

            Assert.That(string.Join("\n", commands), Does.Contain("delete dnsservers"));
            Assert.That(string.Join("\n", commands), Does.Not.Contain("set dnsservers"));
        }

        [Test]
        public void BlacklistedAndLoopbackAdapters_AreNeverTouched()
        {
            // Deliberately names from the 2010 blacklist rather than a modern virtual adapter: a
            // vEthernet/WSL switch is NOT hidden (marking it was the whole point of the #565 fix), so
            // using one here would assert the opposite of what this project decided.
            var loopback = new AdapterDnsState
            {
                Name = "Loopback Pseudo-Interface 1",
                Description = "Software Loopback Interface 1",
                IsOperational = true,
                Ipv4Servers = new[] { "127.0.0.1" },
                Ipv6Servers = Array.Empty<string>()
            };
            var wifiDirect = new AdapterDnsState
            {
                Name = "Local Area Connection* 1",
                Description = "Microsoft Wi-Fi Direct Virtual Adapter",
                IsOperational = true,
                Ipv4Servers = new[] { "192.168.1.1" },
                Ipv6Servers = Array.Empty<string>()
            };

            InterfaceDnsSnapshot.Capture(root, new[] { loopback, wifiDirect });

            var commands = InterfaceDnsSnapshot.BuildRestoreCommands(root, new[] { loopback, wifiDirect });

            Assert.That(commands, Is.Empty,
                "the filter that hides an adapter from the UI is the same filter that protects it here; " +
                "restoring a loopback or a hosted adapter would be a change the user never made");
        }

        [Test]
        public void RestoreCommands_WithoutASnapshot_FallBackToDeleteOnly()
        {
            // An install from before this change has no snapshot file. The behaviour must stay what
            // Uninstall.exe does today rather than invent a DNS state nobody recorded.
            var commands = InterfaceDnsSnapshot.BuildRestoreCommands(root, new[] { Card("Ethernet", true, "127.0.0.1") });

            Assert.That(InterfaceDnsSnapshot.Load(root), Is.Null);
            Assert.That(string.Join("\n", commands), Does.Contain("delete dnsservers"));
        }

        [Test]
        public void CommandsUseTheVerifiedNetshSyntax()
        {
            InterfaceDnsSnapshot.Capture(root, new[] { Card("Ethernet", true, "192.168.1.1") });

            var commands = InterfaceDnsSnapshot.BuildRestoreCommands(root, new[] { Card("Ethernet", true, "127.0.0.1") });
            var set = commands.First(c => c.Contains("set dnsservers"));

            // "validate=no" is what lets a down adapter be written to at all: netsh otherwise probes
            // the address through the interface, which a disconnected card cannot do.
            Assert.That(set, Does.Contain("interface ipv4 set dnsservers"));
            Assert.That(set, Does.Contain("name=\"Ethernet\""));
            Assert.That(set, Does.Contain("address=192.168.1.1"));
            Assert.That(set, Does.Contain("validate=no"));
        }

        [Test]
        public void SnapshotFileLivesBesideTheOtherInstallState()
        {
            Assert.That(Path.GetFileName(InterfaceDnsSnapshot.FilePathFor(root)), Is.EqualTo("dns-snapshot.json"));
            Assert.That(InterfaceDnsSnapshot.FilePathFor(root), Is.EqualTo(Path.Combine(root, "dns-snapshot.json")));
        }
    }
}
