using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;

namespace Pathfinder.Tests.Equipment
{
    public class StatSheetTests
    {
        [Test]
        public void Value_IsBasePlusFlat_ThenPercent()
        {
            var stats = new StatSheet();
            stats.Add(StatId.MaxHull, 20f, 0f, "armor");
            stats.Add(StatId.MaxHull, 0f, 0.5f, "trait");
            stats.Add(StatId.MaxHull, 0f, 0.1f, "other");

            Assert.AreEqual((100f + 20f) * 1.6f, stats.Get(StatId.MaxHull, 100f), 1e-4f);
            Assert.AreEqual(8f, stats.Get(StatId.MoveSpeed, 8f), "Unmodified stats return the base.");
        }

        [Test]
        public void NegativePercent_Reduces_ButNeverBelowZero()
        {
            var stats = new StatSheet();
            stats.Add(StatId.SecondaryCooldown, 0f, -0.3f, "a");
            Assert.AreEqual(7f, stats.Get(StatId.SecondaryCooldown, 10f), 1e-4f);

            stats.Add(StatId.SecondaryCooldown, 0f, -2f, "b");
            Assert.AreEqual(0f, stats.Get(StatId.SecondaryCooldown, 10f));
        }

        [Test]
        public void RemoveSource_RemovesOnlyThatSource()
        {
            var stats = new StatSheet();
            stats.Add(StatId.FireRate, 0f, 0.15f, "overclock");
            stats.Add(StatId.FireRate, 0f, 0.15f, "other");

            Assert.AreEqual(1, stats.RemoveSource("overclock"));
            Assert.AreEqual(11.5f, stats.Get(StatId.FireRate, 10f), 1e-4f);
        }

        [Test]
        public void GetInt_Rounds()
        {
            var stats = new StatSheet();
            stats.Add(StatId.MagazineSize, 0f, 0.3f, "mag");
            Assert.AreEqual(39, stats.GetInt(StatId.MagazineSize, 30));
        }
    }

    public class EquipmentSlotsTests
    {
        static readonly object A = "A", B = "B", C = "C", D = "D";

        [Test]
        public void DefaultCapacities_MatchTheGdd()
        {
            var slots = new EquipmentSlots<object>();
            Assert.AreEqual(1, slots.Capacity(EquipmentCategory.Dash));
            Assert.AreEqual(2, slots.Capacity(EquipmentCategory.Weapon));
            Assert.AreEqual(1, slots.Capacity(EquipmentCategory.Secondary));
            Assert.AreEqual(3, slots.Capacity(EquipmentCategory.Hull));
        }

        [Test]
        public void Adding_FillsSlots_ThenAsksForASwap()
        {
            var slots = new EquipmentSlots<object>();
            Assert.AreEqual(EquipOutcome.Equipped, slots.TryAdd(A, EquipmentCategory.Weapon));
            Assert.AreEqual(EquipOutcome.Equipped, slots.TryAdd(B, EquipmentCategory.Weapon));
            Assert.AreEqual(EquipOutcome.NeedsSwap, slots.TryAdd(C, EquipmentCategory.Weapon));
            Assert.AreEqual(2, slots.In(EquipmentCategory.Weapon).Count);
        }

        [Test]
        public void Duplicates_LevelUpToThree()
        {
            var slots = new EquipmentSlots<object>();
            slots.TryAdd(A, EquipmentCategory.Hull);

            Assert.AreEqual(EquipOutcome.LeveledUp, slots.TryAdd(A, EquipmentCategory.Hull));
            Assert.AreEqual(EquipOutcome.LeveledUp, slots.TryAdd(A, EquipmentCategory.Hull));
            Assert.AreEqual(EquipOutcome.AlreadyMaxed, slots.TryAdd(A, EquipmentCategory.Hull));
            Assert.AreEqual(3, slots.Find(A).Level);
            Assert.AreEqual(1, slots.In(EquipmentCategory.Hull).Count);
        }

        [Test]
        public void Replace_PutsTheNewItemInAtLevelOne()
        {
            var slots = new EquipmentSlots<object>();
            slots.TryAdd(A, EquipmentCategory.Dash);
            slots.TryAdd(A, EquipmentCategory.Dash);

            slots.Replace(EquipmentCategory.Dash, 0, B);

            Assert.IsNull(slots.Find(A));
            Assert.AreEqual(1, slots.Find(B).Level);
        }

        [Test]
        public void LevelUp_FailsAtMaximum()
        {
            var slots = new EquipmentSlots<object>();
            slots.TryAdd(D, EquipmentCategory.Secondary);
            Assert.IsTrue(slots.LevelUp(D));
            Assert.IsTrue(slots.LevelUp(D));
            Assert.IsFalse(slots.LevelUp(D));
            Assert.IsFalse(slots.LevelUp(C), "Not equipped.");
        }

        [Test]
        public void ExtraCapacity_AllowsMoreItems()
        {
            var slots = new EquipmentSlots<object>();
            slots.SetCapacity(EquipmentCategory.Dash, 2);
            slots.TryAdd(A, EquipmentCategory.Dash);
            Assert.AreEqual(EquipOutcome.Equipped, slots.TryAdd(B, EquipmentCategory.Dash));
        }
    }

    public class EquipmentRollerTests
    {
        static LootCandidate[] Pool() => Enumerable.Range(0, 20)
            .Select(i => new LootCandidate(i < 12 ? Rarity.Common : i < 17 ? Rarity.Rare : Rarity.Epic))
            .ToArray();

        [Test]
        public void Offers_DistinctItems()
        {
            for (ulong seed = 0; seed < 200; seed++)
            {
                var picks = EquipmentRoller.Roll(Pool(), 4, 0, Rarity.Common, new Pcg32(seed, 1));
                Assert.AreEqual(4, picks.Count);
                Assert.AreEqual(4, picks.Distinct().Count());
            }
        }

        [Test]
        public void MinimumRarity_IsRespected()
        {
            var pool = Pool();
            for (ulong seed = 0; seed < 200; seed++)
            {
                var picks = EquipmentRoller.Roll(pool, 3, 0, Rarity.Rare, new Pcg32(seed, 2));
                Assert.That(picks.All(i => pool[i].Rarity >= Rarity.Rare));
            }
        }

        [Test]
        public void ExcludedItems_AreNeverOffered()
        {
            var pool = Pool().Select((c, i) => new LootCandidate(c.Rarity, 1f, excluded: i % 2 == 0)).ToArray();
            for (ulong seed = 0; seed < 100; seed++)
                Assert.That(EquipmentRoller.Roll(pool, 3, 1, Rarity.Common, new Pcg32(seed, 3)).All(i => i % 2 == 1));
        }

        [Test]
        public void SmallPool_ReturnsWhatIsAvailable()
        {
            var pool = new[] { new LootCandidate(Rarity.Epic), new LootCandidate(Rarity.Common) };
            var picks = EquipmentRoller.Roll(pool, 3, 0, Rarity.Common, new Pcg32(4, 4));

            Assert.AreEqual(2, picks.Count, "Falls back across rarities and stops when the pool runs dry.");
        }

        [Test]
        public void Odds_FavourCommonsEarly_AndImproveLater()
        {
            var pool = Pool();
            float EpicShare(int galaxy)
            {
                int epics = 0, total = 0;
                for (ulong seed = 0; seed < 2000; seed++)
                {
                    int pick = EquipmentRoller.Roll(pool, 1, galaxy, Rarity.Common, new Pcg32(seed, 5))[0];
                    total++;
                    if (pool[pick].Rarity == Rarity.Epic)
                        epics++;
                }
                return epics / (float)total;
            }

            Assert.That(EpicShare(0), Is.InRange(0.03f, 0.08f), "About 5% Epic in galaxy 1.");
            Assert.Greater(EpicShare(2), EpicShare(0) * 2f, "Galaxy 3 rolls Epics far more often.");
        }
    }
}
