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
    /// <summary>The Hive Matriarch (GDD §7.5), in the combat sandbox.</summary>
    public class MatriarchTests : SandboxTestBase
    {
        Enemy _boss;
        MatriarchBrain _brain;

        IEnumerator SpawnMatriarch(bool godMode = true)
        {
            yield return LoadRun();
            Player.GodMode = godMode;
            Player.PlaceForLevel(new Vector2(0f, -8f));
            _boss = World.SpawnEnemy(EnemyDef("boss.hive-matriarch"), new Vector2(0f, 8f));
            _brain = _boss.GetComponent<MatriarchBrain>();
            yield return WaitTicks(2);
        }

        IEnumerator WaitOnlySpores(int ticks, System.Func<bool> until = null)
        {
            for (int i = 0; i < ticks && (until == null || !until()); i++)
            {
                World.Projectiles.Clear();
                foreach (var enemy in CombatRegistry.Enemies.ToList())
                {
                    if (enemy != _boss && enemy.IsAlive)
                        Kill(enemy);
                }
                yield return new WaitForFixedUpdate();
            }
        }

        void Hit(float amount) => _boss.ApplyDamage(new DamageInfo(amount, Team.Player, 0, _boss.Position, Vector2.up));

        [UnityTest]
        public IEnumerator Brood_BirthsBroodlings_AndSporesBurstBurnAndSlow()
        {
            yield return SpawnMatriarch(godMode: false);
            Player.Reinforce(5000f); // takes her stream and brood without dying; only the spore hits are counted below
            Player.Heal(5000f);
            int sporeHits = 0;
            Player.Damaged += (entity, info, result) => sporeHits += info.SourceId == _boss.EntityId && info.Kind == DamageKind.Contact ? 1 : 0;
            Assert.AreEqual(MatriarchBrain.Phase.Brood, _brain.CurrentPhase);
            Assert.IsTrue(_brain.HeartOpen && !_boss.Armoured, "Drifting, her heart is open.");

            yield return WaitUntilTicks(() => _brain.LivingBrood > 0, 300);
            Assert.Greater(_brain.LivingBrood, 0, "She births Broodlings.");

            // From here on only the spores may hurt the ship: her bullets and brood are cleared away each tick.
            yield return WaitOnlySpores(400, () => _brain.LandedSpores.Any());
            Assert.IsTrue(_brain.LandedSpores.Any(), "Her spore pods land and become clouds.");
            Assert.Less(Vector2.Distance(_brain.LandedSpores.First(), Player.Position), 1f, "A pod lands where the ship was.");
            Assert.GreaterOrEqual(sporeHits, 1, "A pod that bursts on the ship hurts it (playtest 7: spores seemed to do nothing).");

            int afterBurst = sporeHits;
            yield return WaitOnlySpores(70);
            Assert.IsTrue(Player.IsSlowed, "A spore cloud slows the ship.");
            Assert.Greater(sporeHits, afterBurst, "...and burns it while it stays inside.");
        }

        [UnityTest]
        public IEnumerator Frenzy_ChargesWithTheHeartShut_ThenRests()
        {
            yield return SpawnMatriarch();
            Hit(_boss.Vitals.MaxHull * 0.4f);
            yield return WaitUntilTicks(() => _brain.CurrentPhase == MatriarchBrain.Phase.Frenzy && !_brain.IsRoaring, 300);
            var telegraph = (IChargeTelegraph)_brain;
            yield return WaitUntilTicks(() => telegraph.ShowsCharge, 120);
            Assert.IsTrue(telegraph.ShowsCharge, "Each charge shows its lane first.");
            Assert.IsTrue(_brain.HeartOpen && !_boss.Armoured, "Her heart stays open while she lines the charge up (playtest 7).");

            yield return WaitUntilTicks(() => !telegraph.ShowsCharge, 120);
            Assert.IsTrue(_boss.Armoured, "Her heart shuts while she charges.");

            yield return WaitUntilTicks(() => _brain.HeartOpen && !telegraph.ShowsCharge, 600);
            Assert.IsTrue(_brain.HeartOpen && !_boss.Armoured, "After a volley she rests, heart open.");
        }

        [UnityTest]
        public IEnumerator BloomPulse_HitsAShipThatStaysPut()
        {
            yield return SpawnMatriarch(godMode: false);
            bool hit = false;
            Player.Damaged += (entity, info, result) => hit |= info.Kind == DamageKind.Contact && info.SourceId == _boss.EntityId;
            Hit(_boss.Vitals.MaxHull * 0.7f);
            yield return WaitUntilTicks(() => _brain.TryGetPulse(out _, out _), 600);
            Assert.AreEqual(MatriarchBrain.Phase.Bloom, _brain.CurrentPhase);
            yield return WaitUntilTicks(() => hit, 240);
            Assert.IsTrue(hit, "The ring has no gap: standing still, it hits.");
        }

        [UnityTest]
        public IEnumerator BloomPulse_CanBeDashedThrough()
        {
            yield return SpawnMatriarch(godMode: false);
            bool hit = false, dashed = false;
            Player.Damaged += (entity, info, result) => hit |= info.Kind == DamageKind.Contact && info.SourceId == _boss.EntityId;
            Hit(_boss.Vitals.MaxHull * 0.7f);
            yield return WaitUntilTicks(() => _brain.TryGetPulse(out _, out _), 600);
            _brain.TryGetPulse(out var centre, out _);

            // Dash toward her as the ring arrives.
            Script(_ =>
            {
                var command = new ShipCommand();
                if (!dashed && _brain.TryGetPulse(out var c, out float r))
                {
                    float gap = Vector2.Distance(Player.Position, c) - r;
                    if (gap < 1.2f)
                    {
                        dashed = true;
                        command.Dash = true;
                        command.Move = (c - Player.Position).normalized;
                    }
                }
                return command;
            });
            yield return WaitUntilTicks(() => dashed, 300);
            yield return WaitTicks(40);
            Assert.IsTrue(dashed);
            Assert.IsFalse(hit, "A dash carries the ship through the ring.");
        }

        [UnityTest]
        public IEnumerator Death_TakesHerBroodWithHer()
        {
            yield return SpawnMatriarch();
            yield return WaitUntilTicks(() => _brain.LivingBrood > 0, 300);
            yield return WaitUntilTicks(() => _brain.HeartOpen, 300);
            Hit(1_000_000f);
            yield return null;
            yield return null;

            Assert.IsTrue(_boss == null || !_boss.IsAlive);
            Assert.AreEqual(0, CombatRegistry.Enemies.Count, "Her Broodlings die with her.");
            Assert.AreEqual(40, World.Pickups.TotalAmount, "She drops 40 Scraps; the brood drops nothing.");
        }

        /// <summary>Each phase, for visual review: her brood and a spore cloud; a charge lane; a bloom pulse.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CapturePhases()
        {
            string directory = Screenshots.Directory();
            yield return SpawnMatriarch();
            Player.PlaceForLevel(new Vector2(-3f, -3f));
            yield return WaitUntilTicks(() => _brain.LandedSpores.Any() && _brain.LivingBrood > 0, 500);
            yield return WaitTicks(20);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "matriarch_brood.png"));

            // A pod in flight, its landing circle filling in.
            yield return WaitUntilTicks(() => _brain.HasIncomingSpore(0.6f), 400);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "matriarch_spore_incoming.png"));

            Hit(_boss.Vitals.MaxHull * 0.4f);
            var telegraph = (IChargeTelegraph)_brain;
            yield return WaitUntilTicks(() => telegraph.ShowsCharge && telegraph.ChargeProgress > 0.6f, 600);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "matriarch_charge.png"));

            yield return WaitUntilTicks(() => _brain.HeartOpen, 600); // a shut heart shrugs off the hit
            Hit(_boss.Vitals.Hull - _boss.Vitals.MaxHull * 0.3f);
            yield return WaitUntilTicks(() => _brain.CurrentPhase == MatriarchBrain.Phase.Bloom && !_brain.IsRoaring && _boss.Position.sqrMagnitude < 4f, 900);
            Player.PlaceForLevel(_boss.Position + new Vector2(2f, -7f));
            yield return WaitUntilTicks(() => _brain.TryGetPulse(out _, out float r) && r > 4f, 400);
            yield return null;
            Screenshots.CaptureCamera(System.IO.Path.Combine(directory, "matriarch_bloom.png"));
        }

        /// <summary>Balance check: a god-mode bot with perfect aim at whatever can be hurt, the default loadout.</summary>
        [UnityTest]
        public IEnumerator Balance_TimeToKill()
        {
            yield return SpawnMatriarch();
            Script(tick =>
            {
                CombatEntity best = null;
                float bestDistance = float.MaxValue;
                foreach (var enemy in CombatRegistry.OnTeam(Team.Enemy))
                {
                    float distance = (enemy.Position - Player.Position).sqrMagnitude;
                    if (enemy.IsAlive && !enemy.Armoured && !enemy.IsIntangible && distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = enemy;
                    }
                }
                Vector2 aim = best != null ? (best.Position - Player.Position).normalized : Vector2.up;
                // Stay within range of her, as a player would: 7 to 10 units away.
                Vector2 move = Vector2.zero;
                if (_boss != null)
                {
                    Vector2 toBoss = _boss.Position - Player.Position;
                    move = toBoss.magnitude > 10f ? toBoss.normalized : toBoss.magnitude < 7f ? -toBoss.normalized : Vector2.zero;
                }
                return new ShipCommand { Aim = aim, Move = move, FirePrimary = best != null, FireSecondary = best != null && tick % 20 == 0 };
            });

            float timeScale = Time.timeScale;
            Time.timeScale = 5f;
            int start = World.Tick;
            yield return WaitUntilTicks(() => _boss == null || !_boss.IsAlive, 60 * 400);
            Time.timeScale = timeScale;

            float seconds = (World.Tick - start) / 60f;
            Debug.Log($"[Balance] Hive Matriarch time to kill (perfect-aim bot, default loadout): {seconds:F0} s" +
                (_boss != null && _boss.IsAlive ? $", still at {_boss.Vitals.Hull / _boss.Vitals.MaxHull:P0} in {_brain.CurrentPhase}" : string.Empty));
            Assert.IsTrue(_boss == null || !_boss.IsAlive, "The bot must be able to kill her.");
            Assert.That(seconds, Is.InRange(50f, 240f), "A boss fight should last minutes, not seconds or forever.");
        }
    }
}
