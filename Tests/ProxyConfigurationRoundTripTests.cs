using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Nett;
using SimpleDnsCrypt.Config;
using SimpleDnsCrypt.Helper;
using SimpleDnsCrypt.Models;

namespace Tests
{
    /// <summary>
    ///     <see cref="DnscryptProxyConfigurationManager.SaveConfiguration"/> writes the typed model back to
    ///     disk, so any key the model does not carry has never survived a save. dnscrypt-proxy 2.1.18
    ///     documents settings this GUI has no control for — <c>enable_hot_reload</c>,
    ///     <c>bootstrap_resolvers</c>, <c>[ip_encryption]</c>, <c>[captive_portals]</c>, <c>[schedules]</c> —
    ///     and a user who adds one of them by hand loses it the moment the UI saves anything.
    ///     <para>
    ///         Every test here writes such a key into the configuration file, changes one field the
    ///         application does own through the model, saves through the application's own path, and then
    ///         reads the file back off disk. The hand-added key must still be there, and the field the
    ///         application owns must show the value the UI set — both halves are required, because a
    ///         "fix" that preserved the input file untouched instead of the user's key would pass the
    ///         first half and fail the second.
    ///     </para>
    ///     <para>
    ///         Keys are inserted into a named section rather than appended to the file, because in TOML
    ///         a line after a <c>[header]</c> belongs to that table: appending <c>x = 1</c> to the end of
    ///         this fixture's base document asks a question about <c>[anonymized_dns]</c>, not about the
    ///         top level.
    ///     </para>
    /// </summary>
    [TestFixture]
    public class ProxyConfigurationRoundTripTests
    {
        /// <summary>
        ///     Every key below is one <see cref="DnscryptProxyConfiguration"/> carries, so the load succeeds
        ///     and the save has real values to write.
        /// </summary>
        private const string BaseToml = @"
listen_addresses = ['127.0.0.1:53']
max_clients = 250
ipv4_servers = true
require_dnssec = true
force_tcp = false
timeout = 5000
keepalive = 30
fallback_resolvers = ['9.9.9.9:53']
ignore_system_dns = true
cache = true
cache_size = 1024
disabled_server_names = []
#SECTION# [query_log]
format = 'ltsv'
#SECTION# [nx_log]
format = 'ltsv'
#SECTION# [blocked_names]
#SECTION# [blocked_ips]
#SECTION# [anonymized_dns]
skip_incompatible = false
";

        private string configPath;
        private string savedBackup;
        private bool hadBackup;

        [SetUp]
        public void SetUp()
        {
            configPath = Global.DnsCryptConfigurationFilePath;

            // The manager's path is derived from the assembly location, so under a test run it is the
            // test output directory. Pinned anyway: were it ever to resolve into a real installation,
            // this fixture would rewrite that machine's proxy configuration, which is exactly what
            // docs/testing/dns-safety.md R0 forbids.
            Assert.That(configPath, Does.StartWith(AppContext.BaseDirectory),
                $"refusing to write a configuration outside the test output directory: {configPath}");

            Directory.CreateDirectory(Global.DnsCryptFolderPath);

            hadBackup = File.Exists(configPath);
            if (hadBackup)
            {
                savedBackup = File.ReadAllText(configPath);
            }

            WriteConfiguration(Unmark(BaseToml));
            DnscryptProxyConfigurationManager.DnscryptProxyConfiguration = null;
        }

        [TearDown]
        public void TearDown()
        {
            DnscryptProxyConfigurationManager.DnscryptProxyConfiguration = null;

            if (hadBackup)
            {
                File.WriteAllText(configPath, savedBackup);
            }
            else if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }

        /// <summary>
        ///     The <c>#SECTION#</c> markers exist so this file's own layout says which table a line
        ///     belongs to; the base document the tests start from never carries them.
        /// </summary>
        private static string Unmark(string marked)
        {
            return marked.Replace("#SECTION# ", string.Empty);
        }

        private void WriteConfiguration(string toml)
        {
            File.WriteAllText(configPath, toml);
        }

        /// <summary>
        ///     Rewrites the current file with <paramref name="entry"/> as a top-level key, i.e. before the
        ///     first table header.
        /// </summary>
        private void AddTopLevelEntry(string entry)
        {
            var lines = File.ReadAllLines(configPath).ToList();
            var firstHeader = lines.FindIndex(line => line.TrimStart().StartsWith("[", StringComparison.Ordinal));
            lines.Insert(firstHeader < 0 ? lines.Count : firstHeader, entry);
            File.WriteAllText(configPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }

        /// <summary>
        ///     Rewrites the current file with <paramref name="entry"/> inside the named table, next to the
        ///     keys that table already holds.
        /// </summary>
        private void AddEntryInsideTable(string tableHeader, string entry)
        {
            var lines = File.ReadAllLines(configPath).ToList();
            var header = lines.FindIndex(line => string.Equals(line.Trim(), tableHeader, StringComparison.Ordinal));
            Assert.That(header, Is.GreaterThanOrEqualTo(0), $"{tableHeader} is not in the test's base document");

            var next = lines.FindIndex(header + 1, line => line.TrimStart().StartsWith("[", StringComparison.Ordinal));
            lines.Insert(next < 0 ? lines.Count : next, entry);
            File.WriteAllText(configPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }

        /// <summary>
        ///     Loads the file on disk, sets one field the application owns, and saves.
        /// </summary>
        private void ChangeAnOwnedFieldAndSave()
        {
            Assert.IsTrue(DnscryptProxyConfigurationManager.LoadConfiguration(),
                "the test's own TOML must parse into the application's model");

            // 250 -> 300 is a field the UI owns (SettingsView binds max_clients), so this is the
            // "user pressed Save" half of the scenario rather than a synthetic call.
            DnscryptProxyConfigurationManager.DnscryptProxyConfiguration.max_clients = 300;

            Assert.IsTrue(DnscryptProxyConfigurationManager.SaveConfiguration(),
                "SaveConfiguration reported failure, so nothing below is evidence");

            // Prove the write happened before asserting anything about what survived: an untouched file
            // would make every "still there" assertion below vacuous.
            AssertThatValue(Toml.ReadString(File.ReadAllText(configPath)).Get("max_clients").Get<long>(),
                Is.EqualTo(300L), "max_clients");
        }

        private TomlTable ReadSaved()
        {
            return Toml.ReadString(File.ReadAllText(configPath));
        }

        private void AssertThatValue(object actual, IResolveConstraint constraint, string what)
        {
            Assert.That(actual, constraint, $"{what} is not what the saved file says it should be:\n{File.ReadAllText(configPath)}");
        }

        /// <summary>
        ///     Asserts a <c>key = value</c> line in the saved document, ignoring which quote character
        ///     Nett chose when it wrote the string.
        /// </summary>
        private static void AssertThatLineValue(string tomlText, string key, string value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                tomlText, @"(?m)^\s*" + System.Text.RegularExpressions.Regex.Escape(key) + @"\s*=\s*(.+?)\s*$");
            Assert.IsTrue(match.Success, $"{key} is absent from the saved file:\n{tomlText}");
            Assert.That(match.Groups[1].Value.Replace('"', '\'').TrimEnd(','), Is.EqualTo("'" + value + "'").Or.EqualTo(value),
                $"{key} survived but not as {value}");
        }

        [Test]
        public void UnknownTopLevelKey_SurvivesASave()
        {
            AddTopLevelEntry("enable_hot_reload = true");
            ChangeAnOwnedFieldAndSave();

            var saved = ReadSaved();
            Assert.That(saved.ContainsKey("enable_hot_reload"), Is.True,
                "a top-level key the model does not carry was dropped by the save");
            AssertThatValue(saved.Get("enable_hot_reload").Get<bool>(), Is.True, "enable_hot_reload");
        }

        [Test]
        public void UnknownTopLevelKeyWithStringArrayValue_SurvivesASave()
        {
            AddTopLevelEntry("bootstrap_resolvers = ['9.9.9.9:53', '2001:4860:4860::8888:53']");
            ChangeAnOwnedFieldAndSave();

            var saved = ReadSaved();
            Assert.That(saved.ContainsKey("bootstrap_resolvers"), Is.True,
                "a key whose value is an array of strings was dropped");

            var entries = saved.Get("bootstrap_resolvers").Get<TomlArray>().Items;
            AssertThatValue(entries.Length, Is.EqualTo(2), "bootstrap_resolvers entry count");
            AssertThatValue(saved.Get("bootstrap_resolvers").Get<TomlArray>()[0].Get<string>(),
                Is.EqualTo("9.9.9.9:53"), "bootstrap_resolvers[0]");
            AssertThatValue(saved.Get("bootstrap_resolvers").Get<TomlArray>()[1].Get<string>(),
                Is.EqualTo("2001:4860:4860::8888:53"), "bootstrap_resolvers[1]");
        }

        [Test]
        public void UnknownKeyInsideAnOwnedTable_SurvivesASave()
        {
            // [query_log] is a table the model owns; the hand-added key sits inside it. Losing this one
            // is the worse failure of the two, because the section looks untouched in the UI.
            AddEntryInsideTable("[query_log]", "file_mode = 384");
            ChangeAnOwnedFieldAndSave();

            var saved = ReadSaved();
            var queryLog = saved.Get("query_log").Get<TomlTable>();
            Assert.That(queryLog.ContainsKey("file_mode"), Is.True,
                "a key added by hand inside a section the application owns was dropped");
            AssertThatValue(queryLog.Get("file_mode").Get<long>(), Is.EqualTo(384L), "query_log.file_mode");
            AssertThatValue(queryLog.Get("format").Get<string>(), Is.EqualTo("ltsv"),
                "the value the application owns inside that same section");
        }

        [Test]
        public void UnknownWholeTable_SurvivesASave()
        {
            WriteConfiguration(Unmark(File.ReadAllText(configPath)) + "\n[ip_encryption]\nsecret_key = 'a-private-then-public-key'\n");
            ChangeAnOwnedFieldAndSave();

            var saved = ReadSaved();
            Assert.That(saved.ContainsKey("ip_encryption"), Is.True,
                "a section the model does not know about was dropped entirely");

            var section = saved.Get("ip_encryption").Get<TomlTable>();
            AssertThatValue(section.Get("secret_key").Get<string>(), Is.EqualTo("a-private-then-public-key"),
                "ip_encryption.secret_key");
        }

        [Test]
        public void UnknownTableArray_SurvivesASave()
        {
            WriteConfiguration(Unmark(File.ReadAllText(configPath)) +
                "\n[[captive_portals.profile]]\nregistration_port = 8080\nname = 'my-ssid'\n");
            ChangeAnOwnedFieldAndSave();

            var saved = ReadSaved();
            Assert.That(saved.ContainsKey("captive_portals"), Is.True,
                "a table array the model does not carry was dropped");

            // The element's own fields are checked in the text rather than through the DOM because Nett
            // exposes a parsed [[table]] as an array whose element type the reader has to choose itself;
            // the file is what dnscrypt-proxy reads, so that is the contract worth asserting.
            // Quote style is ignored: TOML accepts 'x' and "x" alike, and Nett re-quotes on write.
            var text = File.ReadAllText(configPath);
            Assert.That(text.Replace('"', '\''), Does.Contain("[[captive_portals.profile]]"));
            AssertThatLineValue(text, "registration_port", "8080");
            AssertThatLineValue(text, "name", "my-ssid");
        }

        [Test]
        public void EveryKeyInTheSavedFile_ComesFromTheFileOrFromTheModel()
        {
            // The invariant the preservation step is responsible for: it may only carry over keys the
            // file already held. It must not invent a third source of keys.
            // This deliberately does NOT assert that the save writes only the file's own keys - the
            // model emits every property it carries, so a key the GUI owns that this file omitted
            // appears with its default. That asymmetry is recorded in docs/backlog-triage.md and is
            // not what this fixture guards.
            AddTopLevelEntry("enable_hot_reload = true");
            ChangeAnOwnedFieldAndSave();

            var fromTheFile = Unmark(BaseToml)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                .Select(line => line.Trim().TrimStart('[').TrimEnd(']')
                    .Split(new[] { '.' }, 2)[0]
                    .Split('=')[0].Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var fromTheModel = typeof(DnscryptProxyConfiguration)
                .GetProperties()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var neither = ReadSaved().Keys
                .Where(key => !fromTheFile.Contains(key) && !fromTheModel.Contains(key) && key != "enable_hot_reload")
                .ToList();

            Assert.That(neither, Is.Empty,
                $"the save introduced keys that came from neither the file nor the model: {string.Join(", ", neither)}");
        }

        [Test]
        public void DifferentlyCasedKeyTheModelOwns_IsNotPreservedAlongsideTheWrittenOne()
        {
            // TOML keys are case-sensitive and dnscrypt-proxy documents them lowercase, but a hand edit
            // can introduce a stray spelling. Copying that alongside the model's own lowercase key would
            // put two spellings of one setting in the same file, so a source key is only "unknown" when
            // no target key matches it ignoring case.
            AddTopLevelEntry("REQUIRE_DNSSEC = false");
            ChangeAnOwnedFieldAndSave();

            var saved = ReadSaved();
            var text = File.ReadAllText(configPath);
            Assert.That(saved.ContainsKey("REQUIRE_DNSSEC"), Is.False,
                $"a differently-cased copy of an owned key was preserved:\n{text}");

            // One setting, one spelling. What the surviving line says is decided by the load path, which
            // maps properties ignoring case (TomlSettings.UseTargetPropertySelector(IgnoreCase)), so a
            // stray spelling can win the value on the way in. That is upstream behaviour this fixture
            // does not change, and it is recorded in docs/backlog-triage.md.
            var spellings = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => string.Equals(line.Split('=')[0].Trim(), "require_dnssec", StringComparison.OrdinalIgnoreCase));
            AssertThatValue(spellings, Is.EqualTo(1), "number of require_dnssec spellings in the saved file");
        }

        [Test]
        public void OwnedFieldChangedWhileTheFileHeldAnotherValue_TheUiValueWins()
        {
            // The stale-copy risk of preserving what was read: if the merge wrote the read value back,
            // the UI would silently undo itself.
            AddTopLevelEntry("ovh_unkown = 'x'");
            AddEntryInsideTable("[query_log]", "file_mode = 384");
            ChangeAnOwnedFieldAndSave();

            AssertThatValue(ReadSaved().Get("max_clients").Get<long>(), Is.EqualTo(300L), "max_clients");
            AssertThatValue(ReadSaved().Get("query_log").Get<TomlTable>().Get("format").Get<string>(),
                Is.EqualTo("ltsv"), "query_log.format written by the model");
        }
    }
}
