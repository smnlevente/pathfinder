using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Pathfinder.Core.Run;
using Pathfinder.Game.Content;
using UnityEditor;

namespace Pathfinder.Tests.Game
{
    /// <summary>Pictures in the menus (playtest 7), and the credit their licence asks for.</summary>
    public class IconTests
    {
        const string DatabasePath = "Assets/_Project/Data/ContentDatabase.asset";
        const string IconsDir = "Assets/_Project/Art/Icons";

        [Test]
        public void ShopItemsWeaponsAndLabUpgrades_HaveIcons()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            Assert.IsNotNull(database);
            var definitions = new List<ContentDefinition>();
            definitions.AddRange(database.Equipment);
            definitions.AddRange(database.PrimaryWeapons);
            definitions.AddRange(database.SecondaryWeapons);
            definitions.AddRange(database.MetaUpgrades);
            foreach (var definition in definitions)
                Assert.IsNotNull(definition.Icon, $"{definition.Id} has an icon.");
        }

        [Test]
        public void CurrenciesAndMapNodes_HaveIcons()
        {
            var icons = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath).Icons;
            Assert.IsNotNull(icons, "The content database has its icon set.");
            Assert.IsNotNull(icons.Scraps);
            Assert.IsNotNull(icons.DarkMatter);
            Assert.IsNotNull(icons.Cores);
            Assert.IsNotNull(icons.Reinforce);
            foreach (MapNodeType type in System.Enum.GetValues(typeof(MapNodeType)))
                Assert.IsNotNull(icons.ForNode(type), $"{type} nodes have an icon.");
        }

        [Test]
        public void TheBotCommander_IsUnlockedInTheLab_ForTwoCores()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            var frigate = database.FindShip("ship.bot-commander");
            Assert.IsNotNull(frigate, "The third ship is in the database.");
            Assert.AreEqual(3, database.Ships.Length);
            Assert.IsNotNull(frigate.Icon, "Ships have an icon.");
            var unlock = System.Array.Find(database.MetaUpgrades, u => u.UnlockTarget == frigate);
            Assert.IsNotNull(unlock, "A Lab upgrade unlocks it.");
            Assert.AreEqual(Pathfinder.Core.Meta.MetaCurrency.SingularityCores, unlock.Currency);
            Assert.AreEqual(2, unlock.CostOfTier(0));
        }

        /// <summary>The icons are CC BY 3.0: every one in the project must be credited.</summary>
        [Test]
        public void EveryIcon_IsCredited()
        {
            string credits = File.ReadAllText("CREDITS.md");
            foreach (string path in Directory.GetFiles(IconsDir, "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                StringAssert.Contains($"[{name}](https://game-icons.net/", credits, $"{name} is credited in CREDITS.md.");
            }
        }
    }
}
