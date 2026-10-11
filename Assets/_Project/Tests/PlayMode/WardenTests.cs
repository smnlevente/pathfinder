using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The Binary Wardens (GDD §7.5), in the combat sandbox.</summary>
    public class WardenTests : SandboxTestBase
    {
        Enemy _alpha, _beta;
        WardenBrain _alphaBrain, _betaBrain;

        IEnumerator SpawnWardens(bool godMode = true)
        {
            yield return LoadRun();
            Player.GodMode = godMode;
            Player.PlaceForLevel(new Vector2(0f, -15f)); // outside the orbit
            _alpha = World.SpawnEnemy(EnemyDef("boss.binary-wardens"), new Vector2(0f, 8f));
            _alphaBrain = _alpha.GetComponent<WardenBrain>();
            yield return WaitTicks(2);
            _beta = _alphaBrain.TwinEnemy;
            _betaBrain = _alphaBrain.Twin;
        }

        IEnumerator WaitForSplit() => WaitUntilTicks(() => !_alphaBrain.IsSplitting && !_betaBrain.IsSplitting, 240);

        static void Hit(CombatEntity target, float amount) =>
            target.ApplyDamage(new DamageInfo(amount, Team.Player, 0, target.Position, Vector2.up));

        [UnityTest]
        public IEnumerator Splits_IntoTwoCoresCirclingOnOppositeSides()
        {
            yield return SpawnWardens();
            Assert.IsNotNull(_beta, "The core splits off its twin on its first tick.");
            Assert.AreEqual(WardenBrain.Role.Alpha, _alphaBrain.CurrentRole);
            Assert.AreEqual(WardenBrain.Role.Beta, _betaBrain.CurrentRole);
            Assert.AreEqual(_alpha.Vitals.MaxHull, _beta.Vitals.MaxHull, "The twins share their health.");
            Assert.IsTrue(_alpha.Armoured && _beta.Armoured, "Separating, they can't be hurt.");

            yield return WaitTicks(240);
            Assert.IsFalse(_alpha.Armoured || _beta.Armoured);
            Assert.Less((_alpha.Position + _beta.Position).magnitude, 1.5f, "They circle the arena centre on opposite sides.");
            Assert.Greater(Vector2.Distance(_alpha.Position, _beta.Position), 9.5f);
            Assert.IsTrue(_alphaBrain.TetherActive, "Joined by the tether.");

            Vector2 before = _alpha.Position;
            yield return WaitTicks(60);
            Assert.Greater(Vector2.Angle(before, _alpha.Position), 10f, "The pair rotates.");
        }

        [UnityTest]
        public IEnumerator ALoneCore_OnlyGoesOffline_ThenRebootsAtHalfHealth()
        {
            yield return SpawnWardens();
            yield return WaitForSplit();

            Hit(_alpha, 1_000_000f);
            Assert.IsTrue(_alpha.IsAlive, "While its twin stands, a destroyed core only goes offline.");
            Assert.IsTrue(_alphaBrain.IsOffline && _alpha.Armoured);
            Assert.IsFalse(_alphaBrain.TetherShown, "The link is down.");

            float hull = _alpha.Vitals.Hull;
            Hit(_alpha, 500f);
            Assert.AreEqual(hull, _alpha.Vitals.Hull, "Offline, it can't be hurt.");

            var boss = (IMultiPartBoss)_alphaBrain;
            Assert.AreEqual(2, boss.PartCount);
            Assert.Greater(boss.PartDowntime(0), 4f, "The HUD counts the reboot down.");

            yield return WaitUntilTicks(() => !_alphaBrain.IsOffline, 360);
            Assert.IsFalse(_alphaBrain.IsOffline, "It reboots after 5 s.");
            Assert.AreEqual(_alpha.Vitals.MaxHull * 0.5f, _alpha.Vitals.Hull, 1f, "At half health.");
            Assert.IsTrue(_alphaBrain.TetherShown && !_alphaBrain.TetherActive, "The tether flickers back before it bites.");
        }

        [UnityTest]
        public IEnumerator BothFall_WhenTheSecondGoesDownWhileTheFirstIsOffline()
        {
            yield return SpawnWardens();
            yield return WaitForSplit();

            Hit(_beta, 1_000_000f);
            Assert.IsTrue(_betaBrain.IsOffline);
            yield return WaitTicks(30);
            Hit(_alpha, 1_000_000f);
            yield return null;
            yield return null;

            Assert.IsTrue(IsDead(_alpha) && IsDead(_beta), "Both cores fall together.");
            Assert.AreEqual(0, CombatRegistry.Enemies.Count);
            Assert.AreEqual(30, World.Pickups.TotalAmount, "The boss drops 30 Scraps, once.");
        }

        [UnityTest]
        public IEnumerator Tether_HurtsAShipItSweepsOver()
        {
            yield return SpawnWardens(godMode: false);
            Player.PlaceForLevel(Vector2.zero); // the tether always runs through the arena centre

            bool tethered = false;
            void OnDamaged(CombatEntity entity, DamageInfo info, DamageResult result) =>
                tethered |= info.Kind == DamageKind.Contact && info.SourceId == _alpha.EntityId;
            Player.Damaged += OnDamaged;
            try
            {
                yield return WaitUntilTicks(() => tethered, 300);
            }
            finally
            {
                Player.Damaged -= OnDamaged;
            }
            Assert.IsTrue(tethered, "Once linked, the tether hurts a ship it crosses.");
        }

        /// <summary>
        /// Balance check: a god-mode bot in the middle of the arena with the default loadout. It plays the fight as
        /// intended: it shoots whichever core has more health left, so both wear down together, and finishes the other
        /// one while the first is offline.
        /// </summary>
        [UnityTest]
        public IEnumerator Balance_TimeToKill()
        {
            yield return SpawnWardens();
            Player.PlaceForLevel(Vector2.zero);

            Script(tick =>
            {
                Enemy target = null;
                if (!IsDead(_alpha) && !IsDead(_beta))
                {
                    if (_alphaBrain.IsOffline || _betaBrain.IsOffline)
                        target = _alphaBrain.IsOffline ? _beta : _alpha;
                    else
                        target = _alpha.Vitals.Hull >= _beta.Vitals.Hull ? _alpha : _beta;
                }
                bool shoot = target != null && !target.Armoured;
                Vector2 aim = target != null ? (target.Position - Player.Position).normalized : Vector2.up;
                return new ShipCommand { Aim = aim, FirePrimary = shoot, FireSecondary = shoot && tick % 20 == 0 };
            });

            float timeScale = Time.timeScale;
            Time.timeScale = Mathf.Max(20f, timeScale); // a probe only counts ticks
            int start = World.Tick;
            yield return WaitUntilTicks(() => IsDead(_alpha), 60 * 300);
            Time.timeScale = timeScale;

            float seconds = (World.Tick - start) / 60f;
            Debug.Log($"[Balance] Binary Wardens time to kill (balancing bot, default loadout): {seconds:F0} s");
            Assert.IsTrue(IsDead(_alpha) && IsDead(_beta), "A pilot who wears both down evenly can win.");
            Assert.That(seconds, Is.InRange(40f, 200f), "A boss fight should last minutes, not seconds or forever.");
        }
    }
}
