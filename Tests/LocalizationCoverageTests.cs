using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SimpleDnsCrypt.Helper;

namespace Tests
{
    /// <summary>
    /// Guards the failure that bit us during the rename: LocalizationEx.GetUiString returns null
    /// when a culture has no resources, and five call sites pass that result straight to
    /// string.Format, which throws ArgumentNullException(Parameter 'format') and leaves the app
    /// stuck on the splash dialog. There is no neutral Translation.resx to fall back to, so a
    /// culture without a satellite file is enough to reproduce it.
    /// </summary>
    public class LocalizationCoverageTests
    {
        /// <summary>Keys used as the format argument of string.Format - a null here crashes.</summary>
        private static readonly string[] FormatKeys =
        {
            "loader_validate_folder",
            "loader_missing_files",
            "loader_loading",
            "loader_successfully_loaded",
            "loader_failed_loading",
            // Added with the first Plus updater and present only in the neutral file: proof that a
            // satellite lacks a key still resolves it, which is what keeps new English-only strings
            // from crashing a German user's app.
            "updater_available",
            "updater_staged",
            "updater_failed"
        };

        private static IEnumerable<string> SupportedShortCodes =>
            LocalizationEx.GetSupportedLanguages().Select(l => l.ShortCode);

        [Test]
        public void EveryOfferedLanguageResolvesTheFormatStringsUsedByTheLoader()
        {
            var broken = new List<string>();

            foreach (var code in SupportedShortCodes)
            {
                var culture = new CultureInfo(code, false);

                foreach (var key in FormatKeys)
                {
                    if (LocalizationEx.GetUiString(key, culture) == null)
                    {
                        broken.Add($"{code}/{key}");
                    }
                }
            }

            Assert.IsEmpty(broken,
                "GetUiString returned null for: " + string.Join(", ", broken) +
                " - string.Format(null, ...) throws and the app never leaves the splash dialog.");
        }

        [Test]
        public void EveryOfferedLanguageHasATranslationFile()
        {
            var resources = TranslationFileCultures();
            var missing = SupportedShortCodes
                .Select(c => c.ToLowerInvariant())
                .Where(c => !resources.Contains(c))
                .ToList();

            Assert.IsEmpty(missing,
                "offered in the language dropdown but no Translation.<code>.resx exists: " +
                string.Join(", ", missing));
        }

        [Test]
        public void EveryTranslationFileIsOfferedInTheLanguageDropdown()
        {
            var offered = SupportedShortCodes.Select(c => c.ToLowerInvariant()).ToList();
            var orphaned = TranslationFileCultures()
                .Where(c => !offered.Contains(c))
                .ToList();

            Assert.IsEmpty(orphaned,
                "translation files that no user can ever select: " + string.Join(", ", orphaned));
        }

        /// <summary>
        /// The 34 language files came out of one POEditor export and still carry an identical key set.
        /// That is the one property worth freezing: a key that reaches only some languages produces an
        /// interface that is half translated, and the only way to notice is to compare them.
        ///
        /// The neutral Translation.resx is deliberately excluded. New strings are added there first
        /// and reach every culture through ResourceManager's invariant fallback, so a key present only
        /// in neutral is the designed state, not drift.
        /// </summary>
        [Test]
        public void AllLanguageFilesCarryTheSameKeys()        {
            var dir = Path.Combine(FindRepoRoot(), "SimpleDnsCrypt", "Resources");
            var byLanguage = Directory.GetFiles(dir, "Translation.*.resx")
                .ToDictionary(KeyFromFileName, ReadKeys);

            Assert.Greater(byLanguage.Count, 1, "expected the full set of language files");

            var reference = byLanguage.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).First();
            var baseline = new HashSet<string>(reference.Value);

            var drift = new List<string>();

            foreach (var entry in byLanguage)
            {
                var missing = baseline.Except(entry.Value).ToList();
                var extra = entry.Value.Except(baseline).ToList();

                if (missing.Count > 0 || extra.Count > 0)
                {
                    drift.Add($"{entry.Key}: missing [{string.Join(", ", missing.OrderBy(k => k))}] " +
                              $"unexpected [{string.Join(", ", extra.OrderBy(k => k))}]");
                }
            }

            Assert.IsEmpty(drift,
                $"language files no longer agree with {reference.Key} ({baseline.Count} keys):\n" +
                string.Join("\n", drift));
        }

        /// <summary>
        /// Keys that exist only in the neutral file are how this fork adds a string without touching
        /// the 34 POEditor exports, and the network-card badge tooltip is one of them. Nothing else
        /// here proves the invariant fallback still delivers them: the test above skips the neutral
        /// file on purpose, so if the lookup ever stopped falling back, a German user would simply
        /// lose that line and CI would stay green.
        /// </summary>
        [Test]
        public void EveryOfferedLanguageResolvesTheKeysAddedToTheNeutralFileOnly()
        {
            var dir = Path.Combine(FindRepoRoot(), "SimpleDnsCrypt", "Resources");
            var neutral = ReadKeys(Path.Combine(dir, "Translation.resx"));
            var satellite = ReadKeys(Path.Combine(dir, "Translation.de.resx"));
            var neutralOnly = neutral.Except(satellite).ToList();

            CollectionAssert.Contains(neutralOnly, "network_card_virtual_hint",
                "the badge tooltip key now exists in the satellite files too, which means this test " +
                "has stopped measuring the fallback path it was written to protect");

            var broken = new List<string>();

            foreach (var code in SupportedShortCodes)
            {
                var culture = new CultureInfo(code, false);

                foreach (var key in neutralOnly)
                {
                    if (LocalizationEx.GetUiString(key, culture) == null)
                    {
                        broken.Add($"{code}/{key}");
                    }
                }
            }

            Assert.IsEmpty(broken,
                $"neutral-only keys (there are {neutralOnly.Count}) that resolve for no culture: " +
                string.Join(", ", broken));
        }

        /// <summary>
        /// A literal in the markup is a string no user can ever read in their own language: the 34 POEditor
        /// exports are frozen (that project belongs to upstream's author), so a hard-coded label stays
        /// English for as long as this fork exists.
        /// <para>
        /// Parsed from the XAML tree, not grepped, for two reasons measured while writing this:
        /// <c>SizeToContent="WidthAndHeight"</c> in BaseWindow.xaml matches a "Content=" regex and is not
        /// text, and a TextBlock that carries its text as element content matches no attribute regex at
        /// all. Only values containing a letter are considered - the two "|" separators between grid
        /// columns are glyphs, not prose, and asking for a resource key for one would be nonsense.
        /// </para>
        /// </summary>
        [Test]
        public void NoViewOrWindowCarriesAnUntranslatedProseLiteral()
        {
            var root = FindRepoRoot();
            var files = Directory.GetFiles(Path.Combine(root, "SimpleDnsCrypt", "Views"), "*.xaml")
                .Concat(Directory.GetFiles(Path.Combine(root, "SimpleDnsCrypt", "Windows"), "*.xaml"));

            // Text, Content and Header are the three dependency properties this interface shows text in.
            var offenders = new List<string>();

            foreach (var file in files)
            {
                var document = System.Xml.Linq.XDocument.Load(file);
                var name = Path.GetFileName(file);

                foreach (var element in document.Descendants())
                {
                    foreach (var property in new[] { "Text", "Content", "Header" })
                    {
                        var value = element.Attribute(property)?.Value?.Trim();

                        if (value != null && value.Length > 0 && !value.StartsWith("{", StringComparison.Ordinal)
                            && value.Any(char.IsLetter))
                        {
                            offenders.Add($"{name} <{element.Name.LocalName} {property}=\"{value}\">");
                        }
                    }

                    // A TextBlock/Label/Button whose text is the element value rather than an attribute.
                    var isTextBearer = element.Name.LocalName == "TextBlock"
                                      || element.Name.LocalName == "Label"
                                      || element.Name.LocalName == "Button";

                    if (isTextBearer && !element.HasElements && element.Value.Any(char.IsLetter)
                        && !element.Value.TrimStart().StartsWith("{", StringComparison.Ordinal))
                    {
                        offenders.Add($"{name} <{element.Name.LocalName}> value \"{element.Value.Trim()}\"");
                    }
                }
            }

            Assert.IsEmpty(offenders,
                "strings that can never be translated, add them to Translation.resx and bind with " +
                $"{{{{lex:Loc Key=...}}}}:\n{string.Join("\n", offenders)}");
        }

        /// <summary>
        /// Every <c>{lex:Loc Key=...}</c> in the markup must name a string that actually resolves.
        /// <c>LocalizeDictionary.OutputMissingKeys="False"</c> in MainView.xaml means a key that does not
        /// exist renders as an empty label rather than as something obviously wrong, so a typo in a key
        /// name silently deletes UI text - and a test that only checks the resx files cannot see it,
        /// because the resx is valid on its own. This reads the references out of the XAML and asks the
        /// same lookup the running application uses.
        /// </summary>
        [Test]
        public void EveryKeyReferencedByTheMarkupResolvesForEveryOfferedLanguage()
        {
            var root = FindRepoRoot();
            var files = Directory.GetFiles(Path.Combine(root, "SimpleDnsCrypt", "Views"), "*.xaml")
                .Concat(Directory.GetFiles(Path.Combine(root, "SimpleDnsCrypt", "Windows"), "*.xaml"))
                .Concat(Directory.GetFiles(Path.Combine(root, "SimpleDnsCrypt", "Styles"), "*.xaml"));

            var referenced = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                             File.ReadAllText(file), @"lex:Loc\s+Key=([A-Za-z0-9_.]+)"))
                {
                    referenced.Add(match.Groups[1].Value);
                }
            }

            // A reference set that came out empty would pass every assertion below, so the denominator
            // is part of the claim: this is measuring the interface, not a directory it cannot read.
            Assert.Greater(referenced.Count, 100,
                $"only {referenced.Count} markup keys found - the scan is broken, not the app");

            var broken = new List<string>();
            foreach (var code in SupportedShortCodes)
            {
                var culture = new CultureInfo(code, false);
                foreach (var key in referenced)
                {
                    if (string.IsNullOrEmpty(LocalizationEx.GetUiString(key, culture)))
                    {
                        broken.Add($"{code}/{key}");
                    }
                }
            }

            Assert.IsEmpty(broken,
                $"{referenced.Count} keys are referenced by the markup; these resolve to nothing and " +
                $"render as blank labels: " + string.Join(", ", broken));
        }

        private static string KeyFromFileName(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return name.Substring("Translation.".Length).ToLowerInvariant();
        }

        private static HashSet<string> ReadKeys(string resxPath)
        {
            // Parsed, not matched: the resx schema comment contains literal <data name="...">
            // examples, and a regex would read those as keys in every file - which agrees across
            // languages, so a regex-only check would pass while measuring the wrong thing.
            var document = System.Xml.Linq.XDocument.Load(resxPath);

            Assert.That(document.Root, Is.Not.Null, $"{resxPath} has no root element");

            return document.Root!
                .Elements("data")
                .Select(e => (string?)e.Attribute("name"))
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .ToHashSet();
        }

        private static HashSet<string> TranslationFileCultures()
        {
            var repoRoot = FindRepoRoot();
            var dir = Path.Combine(repoRoot, "SimpleDnsCrypt", "Resources");

            Assert.IsTrue(Directory.Exists(dir), $"translation resources not found at {dir}");

            return new HashSet<string>(
                Directory.GetFiles(dir, "Translation.*.resx")
                    .Select(f => Path.GetFileName(f).Substring("Translation.".Length,
                        Path.GetFileName(f).Length - "Translation.".Length - ".resx".Length))
                    .Select(c => c.ToLowerInvariant()));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SimpleDnsCrypt.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "could not locate the solution root from " + AppContext.BaseDirectory);

            return dir!.FullName;
        }
    }
}
