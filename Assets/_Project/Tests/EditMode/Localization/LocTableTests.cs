using System.Collections.Generic;
using NUnit.Framework;
using Pathfinder.Core.Localization;

namespace Pathfinder.Tests.EditMode.Localization
{
    public class LocTableTests
    {
        [Test]
        public void Parse_ReadsKeysValuesAndMetadata()
        {
            var table = LocTable.Parse("# A comment\n@language = English\n\nhub.title = PROJECT PATHFINDER\nshop.price = {amount} SCRAPS\n");

            Assert.AreEqual("English", table.Meta["language"]);
            Assert.AreEqual(2, table.Count);
            Assert.IsTrue(table.TryGet("hub.title", out string title));
            Assert.AreEqual("PROJECT PATHFINDER", title);
            Assert.IsTrue(table.TryGet("shop.price", out string price));
            Assert.AreEqual("{amount} SCRAPS", price);
        }

        [Test]
        public void Parse_UnescapesLineBreaks_AndKeepsEqualsInValues()
        {
            var table = LocTable.Parse("a = first\\nsecond\nb = 1 + 1 = 2\nc = back\\\\slash\r\nd=tight");

            Assert.AreEqual("first\nsecond", table.Entries["a"]);
            Assert.AreEqual("1 + 1 = 2", table.Entries["b"], "Only the first '=' separates key and value.");
            Assert.AreEqual("back\\slash", table.Entries["c"]);
            Assert.AreEqual("tight", table.Entries["d"], "Windows line endings and missing spaces are fine.");
        }

        [Test]
        public void Parse_QuotesKeepSpaces()
        {
            var table = LocTable.Parse("god = \"    [GOD]\"\nsep = \", \"\nplain =   trimmed  ");
            Assert.AreEqual("    [GOD]", table.Entries["god"]);
            Assert.AreEqual(", ", table.Entries["sep"]);
            Assert.AreEqual("trimmed", table.Entries["plain"]);
        }

        [Test]
        public void Parse_WarnsAboutDuplicatesAndBadLines()
        {
            var warnings = new List<string>();
            var table = LocTable.Parse("a = one\nnot a pair\na = two", warnings);

            Assert.AreEqual("two", table.Entries["a"], "The later value wins.");
            Assert.AreEqual(2, warnings.Count);
        }

        [Test]
        public void Format_FillsNamedPlaceholders_WithFormats()
        {
            string text = LocFormat.Format("{amount} Scraps in {seconds:0.0} s ({percent:0}%)",
                new (string, object)[] { ("amount", 40), ("seconds", 2.345f), ("percent", 49.6f) });
            Assert.AreEqual("40 Scraps in 2.3 s (50%)", text);
        }

        [Test]
        public void Format_KeepsUnknownPlaceholders_AndLiteralBraces()
        {
            Assert.AreEqual("{missing} stays, {braces}", LocFormat.Format("{missing} stays, {{braces}}", new (string, object)[0]));
            Assert.AreEqual("plain", LocFormat.Format("plain", null));
        }

        [Test]
        public void Format_UsesTheInvariantCulture()
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.AreEqual("1.5", LocFormat.Format("{x:0.0}", new (string, object)[] { ("x", 1.5f) }), "No comma decimals from the OS locale.");
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
        }
    }
}
