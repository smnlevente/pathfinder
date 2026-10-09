using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>
    /// Drives the real Run scene with scripted pilot commands. The same <see cref="IShipCommandSource"/> seam
    /// is what a network peer or bot would use.
    /// </summary>
    public class CombatSandboxTests
    {
        const string RunScene = "Run";

        CombatWorld _world;
        WaveSpawner _waves;
        PlayerShip _player;

        sealed class ScriptedCommands : IShipCommandSource
        {
            readonly CombatWorld _world;
            readonly Func<int, ShipCommand> _script;

            public ScriptedCommands(CombatWorld world, Func<int, ShipCommand> script)
            {
                _world = world;
                _script = script;
            }

            public ShipCommand ReadCommand(PlayerShip ship) => _script(_world.Tick);
        }

        IEnumerator LoadRun(bool keepSpawner = false, ulong seed = 1234UL)
        {
            GameClock.Paused = false;
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync(RunScene, LoadSceneMode.Single);
            yield return null; // let Start run so the player spawns

            _world = Object.FindAnyObjectByType<CombatWorld>();
            _waves = Object.FindAnyObjectByType<WaveSpawner>();
            Assert.IsNotNull(_world, "Run scene has no CombatWorld.");
            Assert.AreEqual(1, CombatRegistry.Players.Count, "Expected exactly one spawned player.");

            _waves.enabled = keepSpawner;
            _player = CombatRegistry.Players[0];
            _player.CommandSource = null;
        }

        void Script(Func<int, ShipCommand> script) => _player.CommandSource = new ScriptedCommands(_world, script);

        IEnumerator WaitTicks(int ticks)
        {
            int target = _world.Tick + ticks;
            while (_world.Tick < target)
                yield return new WaitForFixedUpdate();
        }

        IEnumerator WaitUntilTicks(Func<bool> condition, int maxTicks)
        {
            int deadline = _world.Tick + maxTicks;
            while (!condition() && _world.Tick < deadline)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Ship_ReachesTopSpeedAlmostInstantly()
        {
            yield return LoadRun();
            float startX = _player.Position.x;
            Script(_ => new ShipCommand { Move = Vector2.right });

            yield return WaitTicks(60);

            // 8 u/s for 1 s, minus ~0.3 u lost to the 0.08 s acceleration ramp.
            Assert.That(_player.Position.x - startX, Is.InRange(7.4f, 8.0f));
        }

        [UnityTest]
        public IEnumerator Ship_StaysInsideTheArena()
        {
            yield return LoadRun();
            Script(_ => new ShipCommand { Move = new Vector2(-1f, -1f) });

            yield return WaitTicks(400);

            Vector2 limit = _world.ArenaSize * 0.5f - Vector2.one * _player.Radius;
            Assert.AreEqual(-limit.x, _player.Position.x, 1e-3f);
            Assert.AreEqual(-limit.y, _player.Position.y, 1e-3f);
        }

        [UnityTest]
        public IEnumerator PhotonRepeater_DestroysAnApproachingDrone()
        {
            yield return LoadRun();
            var drone = _waves.Spawn(_waves.DroneDefinition, _player.Position + new Vector2(0f, 8f));
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = true });

            yield return WaitUntilTicks(() => drone == null || !drone.IsAlive, maxTicks: 180);

            Assert.IsTrue(drone == null || !drone.IsAlive, "Drone survived 3 s of fire.");
            Assert.AreEqual(_player.Vitals.MaxShield, _player.Vitals.Shield, "The drone should die before it reaches the ship.");
            Assert.Less(_player.Weapon.Ammo, _player.Weapon.MagazineSize);
        }

        [UnityTest]
        public IEnumerator Dash_GrantsIFrames_ThenDamageResumes()
        {
            yield return LoadRun();
            bool dashRequested = false;
            Script(_ =>
            {
                var command = new ShipCommand { Dash = !dashRequested };
                dashRequested = true;
                return command;
            });

            yield return WaitTicks(1);
            Assert.IsTrue(_player.IsIntangible, "Ship should be dashing.");

            SpawnEnemyBulletOn(_player.Position);
            yield return WaitTicks(2);
            Assert.AreEqual(_player.Vitals.MaxShield, _player.Vitals.Shield, "Bullets must pass through a dashing ship.");

            yield return WaitUntilTicks(() => !_player.IsIntangible, maxTicks: 30);
            SpawnEnemyBulletOn(_player.Position);
            yield return WaitTicks(2);
            Assert.Less(_player.Vitals.Shield, _player.Vitals.MaxShield, "After the dash the ship takes damage again.");
        }

        [UnityTest]
        public IEnumerator Dash_CoversItsFullDistance()
        {
            yield return LoadRun();
            Vector2 start = _player.Position;
            bool dashRequested = false;
            Script(_ =>
            {
                var command = new ShipCommand { Dash = !dashRequested, Move = Vector2.right };
                dashRequested = true;
                return command;
            });

            yield return WaitUntilTicks(() => dashRequested && !_player.IsIntangible, maxTicks: 30);
            float dashOnly = _player.Position.x - start.x;

            // 5 u dash. A tick of normal movement after it may add up to ~0.15 u.
            Assert.That(dashOnly, Is.InRange(4.9f, 5.3f));
        }

        [UnityTest]
        public IEnumerator LethalDamage_EndsTheRun()
        {
            yield return LoadRun();
            bool raised = false;
            _world.AllPlayersDown += () => raised = true;

            _player.ApplyDamage(new DamageInfo(10_000f, Team.Enemy, 0, _player.Position, Vector2.up));
            yield return WaitTicks(1);

            Assert.IsFalse(_player.IsAlive);
            Assert.IsTrue(raised);
            Assert.IsTrue(Object.FindAnyObjectByType<RunFlow>().IsGameOver);
        }

        [UnityTest]
        public IEnumerator Gunner_ShootsAtTheShip()
        {
            yield return LoadRun();
            _waves.Spawn(_waves.GunnerDefinition, _player.Position + new Vector2(0f, 10f));

            yield return WaitUntilTicks(() => _player.Vitals.Shield < _player.Vitals.MaxShield, maxTicks: 360);

            Assert.Less(_player.Vitals.Shield, _player.Vitals.MaxShield, "A stationary ship should be hit within 6 s.");
        }

        [UnityTest]
        public IEnumerator Waves_SpawnAwayFromTheShip()
        {
            yield return LoadRun(keepSpawner: true);

            yield return WaitUntilTicks(() => CombatRegistry.Enemies.Count > 0, maxTicks: 300);

            Assert.That(CombatRegistry.Enemies.Count, Is.GreaterThan(0), "First wave never arrived.");
            foreach (var enemy in CombatRegistry.Enemies)
                Assert.That(Vector2.Distance(enemy.Position, _player.Position), Is.GreaterThan(5.5f));
        }

        [UnityTest]
        public IEnumerator SameSeedAndInputs_ReplayIdentically()
        {
            const int snapshotTick = 480;
            string first = null, second = null;

            yield return RecordSnapshot(snapshotTick, s => first = s);
            yield return RecordSnapshot(snapshotTick, s => second = s);

            Assert.IsNotNull(first);
            Assert.That(first, Does.Contain("enemy"), "The snapshot should include enemies to be meaningful.");
            Assert.AreEqual(first, second);
        }

        IEnumerator RecordSnapshot(int snapshotTick, Action<string> record)
        {
            yield return LoadRun(keepSpawner: true, seed: 0xC0FFEEUL);
            Script(DemoPilot);

            string snapshot = null;
            void OnTick(CombatWorld world)
            {
                if (world.Tick == snapshotTick)
                    snapshot = Snapshot();
            }

            _world.Ticked += OnTick;
            yield return WaitUntilTicks(() => snapshot != null, maxTicks: snapshotTick + 60);
            _world.Ticked -= OnTick;
            record(snapshot);
        }

        /// <summary>
        /// A deterministic pilot keyed only by tick: it circles, fires at the nearest enemy and dashes periodically.
        /// The first 60 ticks are idle so the script doesn't depend on when the test attaches it.
        /// </summary>
        ShipCommand DemoPilot(int tick)
        {
            if (tick < 60)
                return default;

            float angle = tick * 0.02f;
            var command = new ShipCommand
            {
                Move = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                FirePrimary = true,
                Dash = tick % 90 == 0,
            };

            Enemy nearest = null;
            float best = float.MaxValue;
            foreach (var enemy in CombatRegistry.Enemies)
            {
                float distance = (enemy.Position - _player.Position).sqrMagnitude;
                if (enemy.IsAlive && distance < best)
                {
                    best = distance;
                    nearest = enemy;
                }
            }
            command.Aim = nearest != null ? nearest.Position - _player.Position : Vector2.up;
            return command;
        }

        string Snapshot()
        {
            var text = new StringBuilder();
            text.Append("player ").Append(F(_player.Position.x)).Append(' ').Append(F(_player.Position.y))
                .Append(" hull ").Append(F(_player.Vitals.Hull)).Append(" shield ").Append(F(_player.Vitals.Shield))
                .Append(" ammo ").Append(_player.Weapon.Ammo).Append(" wave ").Append(_waves.Wave)
                .Append(" projectiles ").Append(_world.Projectiles.Count).AppendLine();

            foreach (var enemy in CombatRegistry.Enemies)
            {
                text.Append("enemy ").Append(enemy.EntityId).Append(' ').Append(enemy.Definition.Id).Append(' ')
                    .Append(F(enemy.Position.x)).Append(' ').Append(F(enemy.Position.y)).Append(" hp ").Append(F(enemy.Vitals.Hull)).AppendLine();
            }
            return text.ToString();
        }

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        void SpawnEnemyBulletOn(Vector2 position)
        {
            _world.Projectiles.Spawn(new ProjectileSpawn(position, Vector2.right, speed: 0.5f, damage: 8f, radius: 0.25f, range: 5f,
                team: Team.Enemy, ownerId: 0, visualPrefab: null));
        }

        /// <summary>
        /// Saves screenshots of live combat for visual review. Runs only when PATHFINDER_SCREENSHOT_DIR is set,
        /// and needs a graphics device, so it doesn't work with -nographics.
        /// </summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureCombatScreenshots()
        {
            string directory = Environment.GetEnvironmentVariable("PATHFINDER_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(directory))
                Assert.Ignore("Set PATHFINDER_SCREENSHOT_DIR to capture screenshots.");
            Directory.CreateDirectory(directory);

            yield return LoadRun(keepSpawner: true, seed: 0xC0FFEEUL);
            Script(DemoPilot);

            foreach (int tick in new[] { 300, 600, 900 })
            {
                yield return WaitUntilTicks(() => _world.Tick >= tick, maxTicks: 1200);
                yield return null; // let visuals interpolate to the latest tick
                Capture(Path.Combine(directory, $"combat_{tick}.png"));
            }
        }

        static void Capture(string path)
        {
            var camera = Camera.main;
            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(camera, request))
            {
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = target;
                camera.Render();
                camera.targetTexture = null;
            }

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
    }
}
