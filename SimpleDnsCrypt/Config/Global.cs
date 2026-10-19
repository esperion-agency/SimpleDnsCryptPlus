using System;
using System.IO;
using System.Reflection;

namespace SimpleDnsCrypt.Config
{
    public static class Global
    {
        /// <summary>
        ///     The name of this application.
        /// </summary>
        public const string ApplicationName = "Simple DNSCrypt Plus";

        /// <summary>
        ///     Remote URI where the application finds its update information, per process architecture.
        ///
        ///     These point at releases of this repository. Upstream 0.7.x read
        ///     raw.githubusercontent.com/bitbeans/.../update.yml and verified the installer against
        ///     Christian Hermann's minisign key, so no other project can publish to that channel and
        ///     a fork that kept the URL would be trusting a key it does not hold.
        /// </summary>
        public const string ApplicationUpdateUri =
            "https://github.com/xardyx2/SimpleDnsCryptPlus/releases/latest/download/update-x86.json";

        /// <summary>
        ///     See <see cref="ApplicationUpdateUri"/>.
        /// </summary>
        public const string ApplicationUpdateUri64 =
            "https://github.com/xardyx2/SimpleDnsCryptPlus/releases/latest/download/update-x64.json";

        /// <summary>
        ///		Output folder for logs.
        /// </summary>
        public const string LogDirectoryName = "logs";

        /// <summary>
        ///     The folder where the dnscrypt-proxy lives in.
        /// </summary>
        public const string DnsCryptProxyFolderName = "dnscrypt-proxy";

        public const string DnsCryptProxyExecutableName86 = "dnscrypt-proxy86.exe";
        public const string DnsCryptProxyExecutableName64 = "dnscrypt-proxy64.exe";

        public const string DnsCryptConfigurationFileName = "dnscrypt-proxy.toml";
        public const string DnsCryptExampleConfigurationFileName = DnsCryptConfigurationFileName + ".example";

        /// <summary>
        ///		Logfile name of dnscrypt-proxy.
        /// </summary>
        public const string DnsCryptLogFileName = "dnscrypt-proxy.log";

        /// <summary>
        ///     Time we wait on a service start (ms).
        /// </summary>
        public const int ServiceStartTime = 2500;

        /// <summary>
        ///     Time we wait on a service stop (ms).
        /// </summary>
        public const int ServiceStopTime = 2500;

        /// <summary>
        ///     Time we wait on a service uninstall (ms).
        /// </summary>
        public const int ServiceUninstallTime = 2500;

        /// <summary>
        ///     Time we wait on a service install (ms).
        /// </summary>
        public const int ServiceInstallTime = 3000;

        public const string DomainBlockLogFileName = "blocked.log";
        public const string QueryLogFileName = "query.log";

        public const string WhitelistRuleFileName = "domain-whitelist.txt";
        public const string BlacklistRuleFileName = "domain-blacklist.txt";
        public const string BlacklistFileName = "blacklist.txt";

        public const string CloakingRulesFileName = "cloaking-rules.txt";
        public const string ForwardingRulesFileName = "forwarding-rules.txt";

        public const string GlobalResolver = "0.0.0.0:53";
        public const string DefaultResolverIpv4 = "127.0.0.1:53";
        public const string DefaultResolverIpv6 = "[::1]:53";

        /// <summary>
        ///     List of the default fall back resolvers.
        /// </summary>
        public static readonly string[] DefaultFallbackResolvers =
        {
            "9.9.9.9:53",
            "8.8.8.8:53"
        };

        /// <summary>
        ///     List of files must exist.
        /// </summary>
        public static readonly string[] DnsCryptProxyFiles =
        {
            DnsCryptProxyExecutableName64,
            DnsCryptProxyExecutableName86,
            DnsCryptExampleConfigurationFileName,
            "LICENSE"
        };

        /// <summary>
        ///     List of interfaces, marked as hidden. The content lives in
        ///     <see cref="SimpleDnsCrypt.Utils.NetworkInterfaceFilter"/> so that the UI and
        ///     <c>Uninstall.exe</c> cannot disagree about which adapters exist; this alias is what the
        ///     application's own call sites read.
        /// </summary>
        public static readonly string[] NetworkInterfaceBlacklist = SimpleDnsCrypt.Utils.NetworkInterfaceFilter.Blacklist;

        /// <summary>
        ///     Tokens that mark an adapter as virtual, tunnel or loopback without hiding it.
        ///     Deliberately wider than <see cref="NetworkInterfaceBlacklist"/>, which is a list of
        ///     names a 2010 Windows used: a Hyper-V switch, a Bluetooth PAN and a Wintun-based VPN
        ///     are all operational today and none of them appear there, so they show up in the
        ///     interface list looking like a cable. Marking one wrongly costs an icon; hiding one
        ///     wrongly costs the user their Wi-Fi (DNSCrypt/SimpleDnsCrypt#565), which is why this
        ///     list decorates and never filters.
        /// </summary>
        public static readonly string[] VirtualAdapterMarkers = SimpleDnsCrypt.Utils.NetworkInterfaceFilter.VirtualMarkers;

        public static string InstallPath { get; } = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static string DnsCryptFolderPath { get; } = Path.Combine(InstallPath, DnsCryptProxyFolderName);

        public static string DnsCryptProxyExecutablePath { get; } = Path.Combine(DnsCryptFolderPath,
            Environment.Is64BitOperatingSystem
                ? DnsCryptProxyExecutableName64
                : DnsCryptProxyExecutableName86);

        public static string DnsCryptConfigurationVersionFilePath { get; } = Path.Combine(DnsCryptFolderPath, "configVersion.txt");
        public static string DnsCryptConfigurationFilePath { get; } = Path.Combine(DnsCryptFolderPath, DnsCryptConfigurationFileName);
        public static string DnsCryptExampleConfigurationFilePath { get; } = Path.Combine(DnsCryptFolderPath, DnsCryptExampleConfigurationFileName);
        public static string DnsCryptLogFilePath { get; } = Path.Combine(InstallPath, LogDirectoryName, DnsCryptLogFileName);
    }
}
