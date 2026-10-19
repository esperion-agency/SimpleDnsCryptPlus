using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace Tests
{
    /// <summary>
    /// MainView drew every section as its own copy of the same checkered Border, with the margin and
    /// padding of each copy tuned by hand. That is how the vertical rhythm split between 0,14 and
    /// 0,10, and no compiler can see any of it: markup that stops referring to a style still builds,
    /// and a merged dictionary whose Source points at a file that does not exist stays silent until
    /// something asks for a key inside it — which is exactly what the instantiating view test failed
    /// to catch, so the path is checked here instead.
    ///
    /// The markup is parsed rather than matched with a regex, for the reason spelled out in
    /// LocalizationCoverageTests: an XML reader cannot mistake a comment or a reformatted attribute
    /// order for structure, and a guard that survives reformatting is a guard.
    /// </summary>
    public class MainViewStructureTests
    {
        /// <summary>The style a framed section picks up, spelled exactly as the markup spells it.</summary>
        private const string SectionFrame = "{StaticResource SectionBorderStyle}";

        /// <summary>
        /// Framed sections in MainView. Twelve, not thirteen: the Border that wraps the whole tab
        /// control carries its own Margin and Padding and was never one of the copies. Pinned as a
        /// number so a revert to hand-copied frames fails loudly instead of quietly halving coverage.
        /// </summary>
        private const int FramedSectionCount = 12;

        private const string BrandGreen = "#FF8ab329";

        private static string ViewPath =>
            Path.Combine(RepoRoot(), "SimpleDnsCrypt", "Views", "MainView.xaml");

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SimpleDnsCrypt.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "could not locate the solution root from " + AppContext.BaseDirectory);

            return dir!.FullName;
        }

        private static List<XElement> LoadedView()
        {
            Assert.IsTrue(File.Exists(ViewPath), $"MainView.xaml not found at {ViewPath}");

            return XDocument.Load(ViewPath).Descendants().ToList();
        }

        [Test]
        public void EveryFramedSectionPicksUpTheSharedBorder()
        {
            var styled = LoadedView()
                .Count(e => (string?)e.Attribute("Style") == SectionFrame);

            Assert.AreEqual(FramedSectionCount, styled,
                $"expected {FramedSectionCount} sections on SectionBorderStyle; a different number " +
                "means either a frame went back to being hand-copied or a section lost its border");
        }

        [Test]
        public void TheCheckeredFrameIsNoLongerCopiedThroughTheView()
        {
            // The root Border may keep its own inline copy; every other one belongs in the style.
            var inlineFrames = LoadedView()
                .Count(e => (string?)e.Attribute("Viewport") == "0,0,8,8");

            Assert.That(inlineFrames, Is.LessThanOrEqualTo(1),
                $"the DrawingBrush checkered border appears {inlineFrames} times in MainView; it is " +
                "one definition in SectionStyles.xaml and the root Border at most");
        }

        [Test]
        public void NoFramedSectionCarriesTheOldVerticalRhythm()
        {
            // Border only. DockPanel, Grid and Button use "0,10,0,0" legitimately inside a section,
            // and asking them to agree with the section rhythm was an over-broad reading of this
            // invariant - the first run of this test failed on nine elements that had nothing to
            // do with the gap between frames.
            var stale = LoadedView()
                .Where(e => e.Name.LocalName == "Border" && (string?)e.Attribute("Margin") == "0,10,0,0")
                .Select(e => (string?)e.Attribute("DataContext") ?? "Border")
                .ToList();

            Assert.IsEmpty(stale,
                "sections still using the 10px gap instead of the shared 14px rhythm: " +
                string.Join(", ", stale));
        }

        [Test]
        public void TheTileIsNotPinnedToAFixedInnerWidth()
        {
            // The tile was 140 + 30 device-independent pixels wide inside a UniformGrid cell of
            // whatever width the window happened to give it, which is what made the grid look
            // ragged rather than merely left-aligned.
            var pinned = LoadedView()
                .Count(e => (string?)e.Attribute("Width") == "140");

            Assert.AreEqual(0, pinned,
                "a 140px ColumnDefinition is back, so the network cards will not line up with their gutters");
        }

        [Test]
        public void SectionHeadingsUseTheSharedTextAndIconStyles()
        {
            // The count of headings is not a structural constant, so this is phrased as the absence
            // of the copies instead: anything still carrying an inline green 20px heading is a
            // heading that will drift the next time someone nudges one of them.
            var copiedText = LoadedView()
                .Where(e => (string?)e.Attribute("FontSize") == "20" &&
                            (string?)e.Attribute("Foreground") == BrandGreen &&
                            e.Attribute("Style") == null)
                .Select(e => e.Name.LocalName)
                .ToList();

            var copiedIcons = LoadedView()
                .Where(e => e.Name.LocalName == "PackIconMaterial" &&
                            (string?)e.Attribute("Width") == "20" &&
                            (string?)e.Attribute("Foreground") == BrandGreen &&
                            e.Attribute("Style") == null)
                .Select(e => (string?)e.Attribute("Kind") ?? "unnamed")
                .ToList();

            Assert.IsEmpty(copiedText,
                "headings still hand-sized instead of using SectionHeaderText: " + string.Join(", ", copiedText));
            Assert.IsEmpty(copiedIcons,
                "header icons still hand-sized instead of using SectionHeaderIcon: " +
                string.Join(", ", copiedIcons));
        }

        [Test]
        public void EveryMergedDictionaryNamesAFileThatExists()
        {
            // WPF resolves a merged Source lazily. Nothing that builds or instantiates the view
            // notices a typo, so the only check that can fail early is this one.
            var view = XDocument.Load(ViewPath);
            var projectDir = Path.Combine(RepoRoot(), "SimpleDnsCrypt");
            var missing = new List<string>();

            // The element really is named "ResourceDictionary.MergedDictionaries" - the dot is part
            // of the local name in XML, so matching a bare "MergedDictionaries" finds nothing and
            // turns this whole test into a pass that measured an empty list.
            var merges = view.Descendants()
                .Where(e => e.Name.LocalName.EndsWith("MergedDictionaries", StringComparison.Ordinal))
                .ToList();

            Assert.AreEqual(1, merges.Count,
                $"MainView is expected to merge one dictionary (the section styles); found {merges.Count}");

            foreach (var dictionary in merges.First().Elements().Where(e => e.Attribute("Source") != null))
            {
                var source = (string?)dictionary.Attribute("Source");

                Assert.That(source, Is.Not.Null,
                    "a merged dictionary in MainView.xaml carries a Source attribute with no value");

                var onDisk = Path.Combine(projectDir,
                    source!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(onDisk))
                {
                    missing.Add($"{source} (looked for {onDisk})");
                }
            }

            Assert.IsEmpty(missing,
                "merged dictionaries that point at no file, so every StaticResource in them is " +
                "missing at runtime: " + string.Join(", ", missing));
        }
    }
}
