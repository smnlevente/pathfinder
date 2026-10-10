using System.Collections.Generic;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Spawns the pilots for a level. Only local pilots get a <see cref="LocalInputCommandSource"/>. A remote
    /// co-op pilot would get a network command source instead.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField] PlayerShip shipPrefab;
        [SerializeField] ShipDefinition ship;
        [SerializeField] PrimaryWeaponDefinition primaryWeapon;
        [SerializeField] SecondaryWeaponDefinition secondaryWeapon;
        [SerializeField] Vector2 spawnPosition = new Vector2(0f, -6f);

        [Header("Run")]
        [SerializeField, Tooltip("Use the loadout chosen in the Hub, when a run session has one.")] bool useRunLoadout;
        [SerializeField] ContentDatabase content;

        readonly List<PlayerShip> _spawned = new List<PlayerShip>();

        public IReadOnlyList<PlayerShip> Spawned => _spawned;

        void Start() => SpawnLocalPlayer();

        public PlayerShip SpawnLocalPlayer()
        {
            int index = _spawned.Count;
            var player = Instantiate(shipPrefab, PlaneMath.ToWorld(spawnPosition), Quaternion.identity);
            player.name = $"Player {index + 1}";
            ResolveLoadout(out var shipToUse, out var primaryToUse, out var secondaryToUse);
            player.Initialize(shipToUse, primaryToUse, secondaryToUse);
            player.Bind(world, index);
            player.IsLocal = true;
            player.CommandSource = player.gameObject.AddComponent<LocalInputCommandSource>();

            _spawned.Add(player);
            return player;
        }

        /// <summary>The Hub's loadout (resolved by content ID) when there is one, otherwise the scene defaults.</summary>
        void ResolveLoadout(out ShipDefinition shipToUse, out PrimaryWeaponDefinition primaryToUse, out SecondaryWeaponDefinition secondaryToUse)
        {
            shipToUse = ship;
            primaryToUse = primaryWeapon;
            secondaryToUse = secondaryWeapon;

            var loadout = RunSession.Current?.Loadout ?? default;
            if (!useRunLoadout || content == null || loadout.IsEmpty)
                return;

            shipToUse = content.FindShip(loadout.ShipId) ?? ship;
            primaryToUse = content.FindPrimary(loadout.PrimaryId) ?? primaryWeapon;
            secondaryToUse = content.FindSecondary(loadout.SecondaryId) ?? secondaryWeapon;
        }
    }
}
