using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Scraps pickups (GDD §7.4) and the wave director (GDD §7.2).</summary>
    public class EconomyAndWaveTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator Scraps_InsideMagnetRange_AreCollected()
        {
            yield return LoadRun();
            World.Pickups.SpawnScraps(Player.Position + new Vector2(2f, 0f), 5, new Pcg32(1, 1));

            yield return WaitUntilTicks(() => World.Pickups.Count == 0, maxTicks: 90);

            Assert.AreEqual(5, Player.Scraps);
        }

        [UnityTest]
        public IEnumerator Scraps_OutOfRange_StayThenExpire()
        {
            yield return LoadRun();
            World.Pickups.SpawnScraps(Player.Position + new Vector2(12f, 0f), 4, new Pcg32(2, 2));

            yield return WaitTicks(60);
            Assert.AreEqual(4, World.Pickups.TotalAmount, "Far pickups wait for the pilot.");
            Assert.AreEqual(0, Player.Scraps);

            yield return WaitTicks(Mathf.CeilToInt(PickupSystem.Lifetime * 60f));
            Assert.AreEqual(0, World.Pickups.Count, "Uncollected Scraps despawn after their lifetime.");
        }

        [UnityTest]
        public IEnumerator Director_StartsNextWaveOnceMostOfTheWaveIsDead()
        {
            yield return LoadRun(keepDirector: true);

            yield return WaitUntilTicks(() => Director.Wave == 1 && CombatRegistry.Enemies.Count > 0, maxTicks: 300);
            yield return WaitTicks(60); // let the whole staggered wave arrive
            Assert.AreEqual(1, Director.Wave);
            int firstBudget = Director.CurrentBudget;

            var enemies = new List<Enemy>(CombatRegistry.Enemies);
            foreach (var enemy in enemies)
                Kill(enemy);

            yield return WaitUntilTicks(() => Director.Wave == 2, maxTicks: 30);
            Assert.AreEqual(2, Director.Wave);
            Assert.GreaterOrEqual(Director.CurrentBudget, firstBudget, "The budget grows each wave.");
        }

        /// <summary>Playtest 7: a level finished while its last Carrier's drones were still flying.</summary>
        [UnityTest]
        public IEnumerator Director_FinishesOnlyOnceEveryEnemyIsDead_LaunchedDronesToo()
        {
            yield return LoadRun(keepDirector: true);
            Director.Begin(new Seed(77), 0, 1, waves: 1);
            yield return WaitUntilTicks(() => Director.Wave == 1 && Director.RemainingInWave > 0 && CombatRegistry.Enemies.Count == Director.RemainingInWave, maxTicks: 300);
            yield return WaitTicks(60); // let the whole staggered wave arrive

            // A drone a Carrier launched: alive, but not one of the wave's own enemies.
            var drone = Spawn("enemy.drone", new Vector2(12f, 8f));
            foreach (var enemy in new List<Enemy>(CombatRegistry.Enemies))
            {
                if (enemy != drone)
                    Kill(enemy);
            }
            yield return WaitTicks(10);
            Assert.AreEqual(0, Director.RemainingInWave, "The wave's own enemies are dead.");
            Assert.IsFalse(Director.IsComplete, "A launched drone is still alive, so the waves aren't done.");

            Kill(drone);
            yield return WaitUntilTicks(() => Director.IsComplete, maxTicks: 10);
            Assert.IsTrue(Director.IsComplete, "With the last drone dead, the waves are done.");
        }

        [UnityTest]
        public IEnumerator Director_SpawnsAwayFromTheShip()
        {
            yield return LoadRun(keepDirector: true);

            yield return WaitUntilTicks(() => CombatRegistry.Enemies.Count > 0, maxTicks: 300);

            Assert.That(CombatRegistry.Enemies.Count, Is.GreaterThan(0), "First wave never arrived.");
            foreach (var enemy in CombatRegistry.Enemies)
                Assert.That(Vector2.Distance(enemy.Position, Player.Position), Is.GreaterThan(5.5f));
        }

        [UnityTest]
        public IEnumerator Vacuum_BringsEveryScrapHome()
        {
            yield return LoadRun();
            World.Pickups.SpawnScraps(Player.Position + new Vector2(18f, 10f), 8, new Pcg32(3, 3));
            int scraps = Player.Scraps;
            yield return WaitTicks(30);
            Assert.Greater(World.Pickups.TotalAmount, 0, "Far outside the magnet, the Scraps stay put.");

            World.Pickups.Vacuum = true; // a won fight
            yield return WaitUntilTicks(() => World.Pickups.TotalAmount == 0, 240);
            Assert.AreEqual(0, World.Pickups.TotalAmount);
            Assert.AreEqual(scraps + 8, Player.Scraps, "Every piece flew to the ship.");
        }

        [UnityTest]
        public IEnumerator DebugScraps_GrantWithoutBanking()
        {
            yield return LoadRun();
            int scraps = Player.Scraps, gained = Player.TotalScrapsGained;
            Object.FindAnyObjectByType<Pathfinder.Game.Dev.DebugHotkeys>().GrantScraps(100);
            Assert.AreEqual(scraps + 100, Player.Scraps, "F8 gives 100 Scraps.");
            Assert.AreEqual(gained, Player.TotalScrapsGained, "A debug grant doesn't bank back as Dark Matter.");
        }
    }
}
