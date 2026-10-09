using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Seeded replay (GDD §14.3) and screenshot capture for visual review.</summary>
    public class DeterminismAndScreenshotTests : SandboxTestBase
    {
        [UnityTest]
        public IEnumerator SameSeedAndInputs_ReplayIdentically()
        {
            const int snapshotTick = 600;
            string first = null, second = null;

            yield return RecordSnapshot(snapshotTick, s => first = s);
            yield return RecordSnapshot(snapshotTick, s => second = s);

            Assert.IsNotNull(first);
            Assert.That(first, Does.Contain("enemy"), "The snapshot should include enemies to be meaningful.");
            Assert.AreEqual(first, second);
        }

        IEnumerator RecordSnapshot(int snapshotTick, Action<string> record)
        {
            yield return LoadRun(keepDirector: true, seed: 0xC0FFEEUL);
            Script(DemoPilot);

            string snapshot = null;
            void OnTick(CombatWorld world)
            {
                if (world.Tick == snapshotTick)
                    snapshot = Snapshot();
            }

            World.Ticked += OnTick;
            yield return WaitUntilTicks(() => snapshot != null, maxTicks: snapshotTick + 60);
            World.Ticked -= OnTick;
            record(snapshot);
        }

        /// <summary>
        /// A deterministic pilot keyed only by tick. It circles, fires at the nearest enemy, dashes, and uses its secondary.
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
                FireSecondary = tick % 240 == 0,
            };

            Enemy nearest = null;
            float best = float.MaxValue;
            foreach (var enemy in CombatRegistry.Enemies)
            {
                float distance = (enemy.Position - Player.Position).sqrMagnitude;
                if (enemy.IsAlive && distance < best)
                {
                    best = distance;
                    nearest = enemy;
                }
            }
            command.Aim = nearest != null ? nearest.Position - Player.Position : Vector2.up;
            return command;
        }

        string Snapshot()
        {
            var text = new StringBuilder();
            text.Append("player ").Append(F(Player.Position.x)).Append(' ').Append(F(Player.Position.y))
                .Append(" hull ").Append(F(Player.Vitals.Hull)).Append(" shield ").Append(F(Player.Vitals.Shield))
                .Append(" weapon ").Append(Player.Primary.Status.Label).Append(" scraps ").Append(Player.Scraps)
                .Append(" wave ").Append(Director.Wave).Append(" projectiles ").Append(World.Projectiles.Count)
                .Append(" pickups ").Append(World.Pickups.Count).AppendLine();

            foreach (var enemy in CombatRegistry.Enemies)
            {
                text.Append("enemy ").Append(enemy.EntityId).Append(' ').Append(enemy.Definition.Id).Append(enemy.IsElite ? "*" : "").Append(' ')
                    .Append(F(enemy.Position.x)).Append(' ').Append(F(enemy.Position.y)).Append(" hp ").Append(F(enemy.Vitals.Hull)).AppendLine();
            }
            return text.ToString();
        }

        static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        static string ScreenshotDirectory()
        {
            string directory = Environment.GetEnvironmentVariable("PATHFINDER_SCREENSHOT_DIR");
            if (string.IsNullOrEmpty(directory))
                Assert.Ignore("Set PATHFINDER_SCREENSHOT_DIR to capture screenshots.");
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        /// Saves screenshots of live combat for visual review. Runs only when PATHFINDER_SCREENSHOT_DIR is set,
        /// and needs a graphics device, so it doesn't work with -nographics.
        /// </summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureCombatScreenshots()
        {
            string directory = ScreenshotDirectory();
            yield return LoadRun(keepDirector: true, seed: 0xC0FFEEUL);
            Script(DemoPilot);

            foreach (int tick in new[] { 300, 900, 1500 })
            {
                yield return WaitUntilTicks(() => World.Tick >= tick, maxTicks: 1600);
                yield return null; // let visuals interpolate to the latest tick
                Capture(Path.Combine(directory, $"combat_{tick}.png"));
            }
        }

        /// <summary>
        /// Stages every enemy type, an elite, enemy bullets and Scraps side by side, to check the visual language
        /// (GDD §12.4).
        /// </summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureReadabilityShowcase()
        {
            string directory = ScreenshotDirectory();
            yield return LoadRun();
            Vector2 center = Player.Position + new Vector2(0f, 4f);

            Spawn("enemy.drone", new Vector2(-6f, 2f));
            Spawn("enemy.gunner", new Vector2(6f, 3f));
            Spawn("enemy.spinner", new Vector2(-4f, 7f));
            Spawn("enemy.lancer", new Vector2(3f, 8f));
            Spawn("enemy.carrier", new Vector2(9f, 9f));
            Spawn("enemy.drone", new Vector2(-9f, 6f), eliteId: "elite.armored");

            var bullet = EnemyDef("enemy.gunner").Prefab.GetComponent<GunnerBrain>().BulletPrefab;
            for (int i = 0; i < 8; i++)
            {
                Vector2 direction = PlaneMath.FromHeading(i * 45f);
                SpawnEnemyBullet(center + direction * 1.5f, direction, speed: 1f, damage: 0f, visual: bullet);
            }
            World.Pickups.SpawnScraps(Player.Position + new Vector2(-3f, -1f), 6, new Pathfinder.Core.Random.Pcg32(7, 7));

            yield return WaitTicks(25);
            yield return null;
            Capture(Path.Combine(directory, "readability.png"));
        }

        /// <summary>Captures each weapon in action: Plasma Cutter, a charged shot, a Seeker Swarm, and a Nova Pulse.</summary>
        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureWeaponShowcase()
        {
            string directory = ScreenshotDirectory();
            yield return LoadRun();
            for (int i = 0; i < 5; i++)
                Spawn("enemy.drone", new Vector2(-8f + i * 4f, 9f));

            Player.EquipPrimary(PrimaryWeapon("primary.plasma-cutter"));
            Script(_ => new ShipCommand { Aim = new Vector2(-0.3f, 1f), FirePrimary = true });
            yield return WaitTicks(8);
            yield return null;
            Capture(Path.Combine(directory, "weapon_plasma.png"));

            Player.EquipPrimary(PrimaryWeapon("primary.quantum-charger"));
            bool release = false;
            Script(_ => new ShipCommand { Aim = Vector2.up, FirePrimary = !release });
            yield return WaitTicks(90);
            release = true;
            yield return WaitTicks(6);
            yield return null;
            Capture(Path.Combine(directory, "weapon_charger.png"));

            Player.EquipSecondary(SecondaryWeapon("secondary.seeker-swarm"));
            Script(Once(new ShipCommand { Aim = Vector2.up, FireSecondary = true }, new ShipCommand { Aim = Vector2.up }));
            yield return WaitTicks(14);
            yield return null;
            Capture(Path.Combine(directory, "weapon_seekers.png"));

            Player.EquipSecondary(SecondaryWeapon("secondary.nova-pulse"));
            Script(Once(new ShipCommand { FireSecondary = true }));
            yield return WaitTicks(6);
            yield return null;
            Capture(Path.Combine(directory, "weapon_nova.png"));
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
