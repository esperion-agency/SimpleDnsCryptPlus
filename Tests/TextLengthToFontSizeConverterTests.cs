using System.Globalization;
using NUnit.Framework;
using SimpleDnsCrypt.Converters;

namespace Tests
{
    /// <summary>
    /// Tile titles are sized by this converter, and a tile is 160 device-independent pixels wide, so
    /// the two-step table is the only thing standing between "Ethernet" and a 34-character
    /// "vEthernet (WSL (Hyper-V firewall))" and the address line underneath them.
    ///
    /// These cases are a pin, not a red-first test: the branches they describe already behave this
    /// way. They exist because the next change collapses two identical branches, and nobody should be
    /// able to do that without the table being written down first.
    /// </summary>
    public class TextLengthToFontSizeConverterTests
    {
        private static readonly TextLengthToFontSizeConverter Converter = new TextLengthToFontSizeConverter();

        [TestCase(1, 14)]
        [TestCase(10, 14)]
        [TestCase(11, 14)]
        [TestCase(14, 14)]
        [TestCase(15, 10)]
        [TestCase(34, 10)]
        public void TheTitleSizeStepsAtFourteenCharacters(int length, int expectedSize)
        {
            var text = new string('a', length);

            var size = Converter.Convert(text, typeof(double), null, CultureInfo.InvariantCulture);

            Assert.AreEqual(expectedSize, size,
                $"a {length}-character adapter name must render at {expectedSize}px");
        }

        [Test]
        public void AnEmptyNameStillGetsTheLargerSize()
        {
            // A tile can be built before its binding has a value, and the converter must not hand
            // back the small size for that - it is the case that would otherwise look like a bug
            // in the layout rather than in the converter.
            var size = Converter.Convert(string.Empty, typeof(double), null, CultureInfo.InvariantCulture);

            Assert.AreEqual(14, size);
        }
    }
}
