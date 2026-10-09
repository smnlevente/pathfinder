using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Dev;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>
    /// Loads the real Run scene and drives it with scripted pilot commands. The same <see cref="IShipCommandSource"/>
    /// seam is what a network peer or bot would use.
    /// </summary>
    public abstract class SandboxTestBase
    {
        const string RunScene = "Run";

        protected CombatWorld World { get; private set; }
        protected WaveDirector Director { get; private set; }
        protected PlayerShip Player { get; private set; }
        DebugHotkeys _loadouts;

        protected sealed class ScriptedCommands : IShipCommandSource
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

        protected IEnumerator LoadRun(bool keepDirector = false, ulong seed = 1234UL)
        {
            GameClock.Paused = false;
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync(RunScene, LoadSceneMode.Single);
            yield return null; // let Start run so the player spawns

            World = Object.FindAnyObjectByType<CombatWorld>();
            Director = Object.FindAnyObjectByType<WaveDirector>();
            _loadouts = Object.FindAnyObjectByType<DebugHotkeys>();
            Assert.IsNotNull(World, "Run scene has no CombatWorld.");
            Assert.AreEqual(1, CombatRegistry.Players.Count, "Expected exactly one spawned player.");

            Director.enabled = keepDirector;
            Player = CombatRegistry.Players[0];
            Player.CommandSource = null;
        }

        protected void Script(Func<int, ShipCommand> script) => Player.CommandSource = new ScriptedCommands(World, script);

        /// <summary>A command that is true only on the first tick it is read, e.g. a dash or secondary press.</summary>
        protected static Func<int, ShipCommand> Once(ShipCommand first, ShipCommand after = default)
        {
            bool used = false;
            return _ =>
            {
                var command = used ? after : first;
                used = true;
                return command;
            };
        }

        protected IEnumerator WaitTicks(int ticks)
        {
            int target = World.Tick + ticks;
            while (World.Tick < target)
                yield return new WaitForFixedUpdate();
        }

        protected IEnumerator WaitUntilTicks(Func<bool> condition, int maxTicks)
        {
            int deadline = World.Tick + maxTicks;
            while (!condition() && World.Tick < deadline)
                yield return new WaitForFixedUpdate();
        }

        protected EnemyDefinition EnemyDef(string id) =>
            Director.Config.FindEnemy(id) ?? throw new ArgumentException($"No enemy '{id}' in the wave config.");

        protected EliteModifierDefinition EliteDef(string id) =>
            Director.Config.FindElite(id) ?? throw new ArgumentException($"No elite '{id}' in the wave config.");

        protected PrimaryWeaponDefinition PrimaryWeapon(string id) =>
            _loadouts.PrimaryWeapons.FirstOrDefault(w => w.Id == id) ?? throw new ArgumentException($"No primary weapon '{id}'.");

        protected SecondaryWeaponDefinition SecondaryWeapon(string id) =>
            _loadouts.SecondaryWeapons.FirstOrDefault(w => w.Id == id) ?? throw new ArgumentException($"No secondary weapon '{id}'.");

        protected Enemy Spawn(string id, Vector2 offsetFromPlayer, string eliteId = null) =>
            World.SpawnEnemy(EnemyDef(id), Player.Position + offsetFromPlayer, eliteId != null ? EliteDef(eliteId) : null);

        protected static bool IsDead(Enemy enemy) => enemy == null || !enemy.IsAlive;

        protected float DamageTaken => (Player.Vitals.MaxShield - Player.Vitals.Shield) + (Player.Vitals.MaxHull - Player.Vitals.Hull);

        protected void SpawnEnemyBullet(Vector2 position, Vector2 direction, float speed = 0.5f, float damage = 8f, GameObject visual = null)
        {
            World.Projectiles.Spawn(new ProjectileSpawn(position, direction, speed, damage, radius: 0.25f, range: 10f,
                team: Team.Enemy, ownerId: 0, visualPrefab: visual));
        }

        protected static void Kill(CombatEntity entity) =>
            entity.ApplyDamage(new DamageInfo(1_000_000f, entity.Team.Opponent(), 0, entity.Position, Vector2.up));
    }
}
