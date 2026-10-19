using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SimpleDnsCrypt.Utils
{
    /// <summary>
    ///     One adapter's DNS servers as they were, plus whether the link was up. Read before the
    ///     application writes, replayed by <c>Uninstall.exe</c>.
    /// </summary>
    public sealed class AdapterDnsState
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        ///     Recorded for the log only. Nothing here acts on it: a disconnected card still has
        ///     persistent DNS settings, and writing them back does not need the link.
        /// </summary>
        public bool IsOperational { get; set; }

        public IReadOnlyList<string> Ipv4Servers { get; set; } = new List<string>();
        public IReadOnlyList<string> Ipv6Servers { get; set; } = new List<string>();
    }

    /// <summary>
    ///     The one snapshot of DNS state as it was before this installation first touched an adapter,
    ///     and the netsh arguments that put it back.
    ///     <para>
    ///         Only one snapshot is ever kept. A second <see cref="Capture"/> is refused rather than
    ///         overwritten, because by then the file on disk holds the proxy's own address — snapshotting
    ///         again would record the thing we are trying to be able to undo.
    ///     </para>
    /// </summary>
    public static class InterfaceDnsSnapshot
    {
        public const string FileName = "dns-snapshot.json";

        public static string FilePathFor(string installRoot) => Path.Combine(installRoot, FileName);

        /// <summary>
        ///     Writes the snapshot. Returns false, leaving the existing file untouched, when one is
        ///     already there. Throws nothing: the caller is about to change DNS, and a snapshot that
        ///     cannot be written must not stop that.
        /// </summary>
        public static bool Capture(string installRoot, IEnumerable<AdapterDnsState> state)
            => Capture(installRoot, () => state);

        /// <summary>
        ///     The lazy form, for a caller that is about to change DNS and would rather not enumerate
        ///     every adapter on a machine that already has its snapshot.
        /// </summary>
        public static bool Capture(string installRoot, Func<IEnumerable<AdapterDnsState>> readState)
        {
            var path = FilePathFor(installRoot);

            if (File.Exists(path))
            {
                return false;
            }

            var document = new SnapshotDocument
            {
                CapturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Adapters = (readState?.Invoke() ?? Enumerable.Empty<AdapterDnsState>()).ToList()
            };

            try
            {
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(document, Formatting.Indented));
                File.Move(temp, path, true);
                return true;
            }
            catch (Exception)
            {
                // A read-only install folder, or a scanner holding the file. The DNS change goes ahead
                // without a snapshot, and uninstall then falls back to clearing the static entries the
                // way it always has.
                return false;
            }
        }

        /// <summary>
        ///     The stored snapshot, or null when the installation predates this file.
        /// </summary>
        public static DnsSnapshotData? Load(string installRoot)
        {
            var path = FilePathFor(installRoot);

            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var document = JsonConvert.DeserializeObject<SnapshotDocument>(File.ReadAllText(path));

                return document == null ? null : new DnsSnapshotData(document.Adapters);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        ///     The netsh argument lists that undo this installation's DNS writes.
        ///     <para>
        ///         With a snapshot, only the adapters named in it are touched — an adapter the
        ///         application never wrote to is not ours to change, and the old tool's blanket
        ///         "delete every DNS server on every up adapter" did exactly that. Without one (an install
        ///         from before this file existed) the behaviour stays what it was: clear the static
        ///         entries on every non-hidden adapter.
        ///     </para>
        /// </summary>
        public static IReadOnlyList<string> BuildRestoreCommands(string installRoot, IEnumerable<AdapterDnsState> current)
        {
            var adapters = (current ?? Enumerable.Empty<AdapterDnsState>()).ToList();
            var snapshot = Load(installRoot);
            var commands = new List<string>();

            foreach (var adapter in adapters)
            {
                if (NetworkInterfaceFilter.IsHidden(adapter.Name, adapter.Description))
                {
                    continue;
                }

                // The command is aimed at the adapter by name, so a card that is down or unplugged is
                // written just the same. Nothing here enables, disables or reconnects an adapter: that
                // is the user's call, and doing it from an uninstaller would be a surprise.
                var recorded = snapshot?.TryGet(adapter.Name);

                if (recorded == null && snapshot != null)
                {
                    continue;   // present now, never touched by us
                }

                foreach (var family in new[] { "ipv4", "ipv6" })
                {
                    var before = recorded == null
                        ? null
                        : (family == "ipv4" ? recorded.Ipv4Servers : recorded.Ipv6Servers);

                    var now = family == "ipv4" ? adapter.Ipv4Servers : adapter.Ipv6Servers;

                    if (before == null)
                    {
                        // No snapshot at all, or a family the legacy tool would have cleared anyway.
                        if (now.Count > 0 || snapshot == null)
                        {
                            commands.Add($"interface {family} delete dnsservers name=\"{adapter.Name}\" all");
                        }

                        continue;
                    }

                    if (before.Count == 0)
                    {
                        if (now.Count > 0)
                        {
                            commands.Add($"interface {family} delete dnsservers name=\"{adapter.Name}\" all");
                        }

                        continue;
                    }

                    commands.Add($"interface {family} set dnsservers name=\"{adapter.Name}\" " +
                                 $"address={before[0]} validate=no");

                    for (var i = 1; i < before.Count; i++)
                    {
                        commands.Add($"interface {family} add dnsservers name=\"{adapter.Name}\" " +
                                     $"address={before[i]} index={i + 1} validate=no");
                    }
                }
            }

            return commands;
        }

        /// <summary>
        ///     Reads the live DNS state of every adapter, including the ones that are down.
        /// </summary>
        public static IReadOnlyList<AdapterDnsState> ReadAll()
        {
            var states = new List<AdapterDnsState>();

            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                var properties = nic.GetIPProperties();

                states.Add(new AdapterDnsState
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    IsOperational = nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up,
                    Ipv4Servers = properties.DnsAddresses
                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(a => a.ToString()).ToList(),
                    Ipv6Servers = properties.DnsAddresses
                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        .Select(a => a.ToString()).ToList()
                });
            }

            return states;
        }

        private sealed class SnapshotDocument
        {
            public string CapturedAtUtc { get; set; } = string.Empty;
            public List<AdapterDnsState> Adapters { get; set; } = new List<AdapterDnsState>();
        }
    }

    /// <summary>
    ///     A loaded snapshot, keyed by adapter name.
    /// </summary>
    public sealed class DnsSnapshotData
    {
        private readonly Dictionary<string, AdapterDnsState> _adapters;

        internal DnsSnapshotData(List<AdapterDnsState> adapters)
        {
            // Interface names are unique on one machine and Windows itself addresses them by name;
            // a duplicate from a corrupt file keeps its first entry rather than throwing on restore.
            _adapters = new Dictionary<string, AdapterDnsState>(StringComparer.OrdinalIgnoreCase);

            foreach (var adapter in adapters ?? new List<AdapterDnsState>())
            {
                if (adapter?.Name != null && !_adapters.ContainsKey(adapter.Name))
                {
                    _adapters[adapter.Name] = adapter;
                }
            }
        }

        /// <summary>
        ///     The recorded state for a name, or an empty one when the name is not in the snapshot.
        ///     Prefer <see cref="TryGet"/> when "absent" has to be told apart from "was on DHCP".
        /// </summary>
        public AdapterDnsState Adapter(string name) => TryGet(name) ?? new AdapterDnsState { Name = name };

        public AdapterDnsState? TryGet(string name) =>
            name != null && _adapters.TryGetValue(name, out var found) ? found : null;

        public IEnumerable<string> Names => _adapters.Keys;
    }
}
