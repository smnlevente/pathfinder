using System.Collections;
using NUnit.Framework;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Equipment;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The ships' traits (GDD §5.3).</summary>
    public class ShipTests : SandboxTestBase
    {
        /// <summary>A kill the pilot gets the credit for, so its traits react.</summary>
        void KillAsPilot(Enemy enemy) => enemy.ApplyDamage(new DamageInfo(1_000_000f, Team.Player, Player.EntityId, enemy.Position, Vector2.up));

        /// <summary>M7: the Bot-Commander Frigate's Swarm Protocol launches a homing bot from every kill, a few at a time.</summary>
        [UnityTest]
        public IEnumerator BotCommander_LaunchesAHomingBotFromEveryKill()
        {
            yield return LoadRun();
            var frigate = Ship("ship.bot-commander");
            Player.Initialize(frigate, Player.PrimaryWeapon, Player.SecondaryWeapon);
            var protocol = (SwarmProtocolDefinition)frigate.Trait;
            Assert.IsNotNull(protocol, "The frigate's trait is Swarm Protocol.");

            var target = Spawn("enemy.carrier", new Vector2(6f, 6f));
            float health = target.Vitals.Hull;
            KillAsPilot(Spawn("enemy.drone", new Vector2(0f, 4f)));
            Assert.AreEqual(1, World.Projectiles.CountFrom(Player.EntityId, protocol.BotPrefab), "A kill launches a bot.");

            yield return WaitUntilTicks(() => target.Vitals.Hull < health, 300);
            Assert.Less(target.Vitals.Hull, health, "The bot flies at an enemy and bursts on it.");

            for (int i = 0; i < 10; i++)
                KillAsPilot(Spawn("enemy.drone", new Vector2(-6f + i, -4f)));
            Assert.AreEqual(protocol.MaxBots, World.Projectiles.CountFrom(Player.EntityId, protocol.BotPrefab), "No more than six bots at once.");
        }
    }
}
