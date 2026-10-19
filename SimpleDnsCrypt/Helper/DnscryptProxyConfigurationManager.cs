using Nett;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Models;
using System;
using System.IO;

namespace SimpleDnsCrypt.Helper
{
    /// <summary>
    /// Class to load and save the dnscrypt configuration (TOML format).
    /// </summary>
    public static class DnscryptProxyConfigurationManager
    {
        public static TomlSettings ReadTomlSettings =>
            TomlSettings.Create(s => s.ConfigurePropertyMapping(m => m.UseTargetPropertySelector(standardSelectors => standardSelectors.IgnoreCase)));

        /// <summary>
        /// The global dnscrypt configuration. Non-null once <see cref="LoadConfiguration"/> returns true;
        /// callers that have passed that check assert it rather than re-check it.
        /// </summary>
        public static DnscryptProxyConfiguration? DnscryptProxyConfiguration { get; set; }

        /// <summary>
        /// Loads the configuration from a .toml file.
        /// </summary>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        public static bool LoadConfiguration()
        {
            try
            {
                var configFile = Global.DnsCryptConfigurationFilePath;
                if (!File.Exists(configFile)) return false;
                DnscryptProxyConfiguration = Toml.ReadFile<DnscryptProxyConfiguration>(configFile, ReadTomlSettings);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Saves the configuration to a .toml file.
        /// </summary>
        /// <returns><c>true</c> on success, otherwise <c>false</c></returns>
        public static bool SaveConfiguration()
        {
            if (DnscryptProxyConfiguration == null) return false;

            try
            {
                var settings = TomlSettings.Create(s => s.ConfigurePropertyMapping(m => m.UseKeyGenerator(standardGenerators => standardGenerators.LowerCase)));
                var configFile = Global.DnsCryptConfigurationFilePath;

                // The model decides the values it owns and nothing else: whatever the file holds that the
                // model has no property for is read back in and carried over, so a setting a user added by
                // hand survives a save from the GUI instead of being silently deleted.
                var table = Toml.Create(DnscryptProxyConfiguration, settings);
                if (File.Exists(configFile))
                {
                    TomlKeyPreservation.CopyUnknownKeys(table, Toml.ReadFile(configFile, ReadTomlSettings));
                }

                Toml.WriteFile(table, configFile, settings);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
