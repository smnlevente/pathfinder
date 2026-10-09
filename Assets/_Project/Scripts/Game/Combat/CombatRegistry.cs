using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// Every live combat entity, grouped by team. There is no "the player": targeting, the camera and
    /// spawning all work with 1–N players (GDD §14.4).
    /// </summary>
    public static class CombatRegistry
    {
        static readonly List<PlayerShip> s_players = new List<PlayerShip>();
        static readonly List<Enemy> s_enemies = new List<Enemy>();
        static readonly List<CombatEntity>[] s_byTeam = { new List<CombatEntity>(), new List<CombatEntity>() };
        static int s_lastEntityId;

        /// <summary>In registration order, which is deterministic for a given seed and inputs.</summary>
        public static IReadOnlyList<PlayerShip> Players => s_players;
        public static IReadOnlyList<Enemy> Enemies => s_enemies;

        public static IReadOnlyList<CombatEntity> OnTeam(Team team) => s_byTeam[(int)team];

        public static int Register(CombatEntity entity)
        {
            s_byTeam[(int)entity.Team].Add(entity);
            if (entity is PlayerShip player)
                s_players.Add(player);
            else if (entity is Enemy enemy)
                s_enemies.Add(enemy);

            return ++s_lastEntityId;
        }

        public static void Unregister(CombatEntity entity)
        {
            s_byTeam[(int)entity.Team].Remove(entity);
            if (entity is PlayerShip player)
                s_players.Remove(player);
            else if (entity is Enemy enemy)
                s_enemies.Remove(enemy);
        }

        /// <summary>Nearest living player, or null if none. Ties go to the earliest registered player.</summary>
        public static PlayerShip NearestLivingPlayer(Vector2 from)
        {
            PlayerShip nearest = null;
            float bestDistance = float.MaxValue;
            foreach (var player in s_players)
            {
                if (!player.IsAlive)
                    continue;

                float distance = (player.Position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = player;
                }
            }
            return nearest;
        }

        public static bool AnyPlayerAlive()
        {
            foreach (var player in s_players)
            {
                if (player.IsAlive)
                    return true;
            }
            return false;
        }

        /// <summary>Restarts entity numbering. Called when a new combat world starts.</summary>
        internal static void ResetEntityIds() => s_lastEntityId = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_players.Clear();
            s_enemies.Clear();
            foreach (var list in s_byTeam)
                list.Clear();
            s_lastEntityId = 0;
        }
    }
}
