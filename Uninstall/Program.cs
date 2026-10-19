using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SimpleDnsCrypt.Utils;

namespace Uninstall
{
    internal class Program
    {
        private static void Main()
        {
            try
            {
                ClearLocalNetworkInterfaces();
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        /// <summary>
        ///     Execute a process with arguments. Failures are swallowed by design: this runs as an MSI
        ///     deferred custom action, and an uninstall that aborts halfway leaves a worse state than one
        ///     that reports a card it could not reach. <see cref="RestoreCount"/> and
        ///     <see cref="LastFailures"/> exist because the MSI log should still be able to show it.
        /// </summary>
        internal static void ExecuteWithArguments(string filename, string arguments)
        {
            try
            {
                const int timeout = 9000;
                using (var process = new Process())
                {
                    process.StartInfo.FileName = filename;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    process.Start();

                    if (process.WaitForExit(timeout) && process.ExitCode != 0)
                    {
                        LastFailures.Add($"{filename} {arguments} -> exit {process.ExitCode}");
                    }
                    else if (!process.HasExited)
                    {
                        LastFailures.Add($"{filename} {arguments} -> timed out");
                    }
                }
            }
            catch (Exception exception)
            {
                LastFailures.Add($"{filename} {arguments} -> {exception.GetType().Name}");
            }
        }

        /// <summary>
        ///     The netsh argument lists the last run produced, for tests and for the MSI log.
        /// </summary>
        internal static readonly List<string> LastCommands = new List<string>();

        internal static readonly List<string> LastFailures = new List<string>();

        internal static int RestoreCount => LastCommands.Count;

        /// <summary>
        ///     Put every adapter this installation touched back to the DNS it had before.
        ///     <para>
        ///         The enumeration, the blacklist and the pre-change snapshot all come from
        ///         <c>SimpleDnsCrypt.Utils</c> now. This file used to keep its own copy of the filter and
        ///         its own <c>OperationalStatus.Up</c> test, which meant a card that was disconnected,
        ///         disabled or otherwise down at uninstall time kept the static servers written to it —
        ///         and the next time it came up it still pointed at a proxy that was gone.
        ///     </para>
        ///     <para>
        ///         Nothing here enables, disables or reconnects an adapter. Writing a persistent DNS
        ///         setting does not need the link to be up, and bringing a card up from an uninstaller is
        ///         not this tool's business.
        ///     </para>
        /// </summary>
        internal static void ClearLocalNetworkInterfaces()
        {
            try
            {
                // Uninstall.exe sits beside SimpleDnsCryptPlus.exe in the portable layout and in the MSI's
                // installation folder, so the snapshot the application wrote is found by the same path
                // the application used.
                var installRoot = AppContext.BaseDirectory;

                LastCommands.Clear();
                LastCommands.AddRange(InterfaceDnsSnapshot.BuildRestoreCommands(installRoot, InterfaceDnsSnapshot.ReadAll()));

                foreach (var arguments in LastCommands)
                {
                    ExecuteWithArguments("netsh", arguments);
                }
            }
            catch
            {
                // A machine with no readable adapters, or a netsh that cannot start. The service and the
                // files are still being removed; a skipped restore is reported, not fatal.
            }
        }
    }
}
