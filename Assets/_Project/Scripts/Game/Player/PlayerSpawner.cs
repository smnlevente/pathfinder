using System.Collections.Generic;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
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
        [SerializeField] Vector2 spawnPosition = new Vector2(0f, -6f);

        readonly List<PlayerShip> _spawned = new List<PlayerShip>();

        public IReadOnlyList<PlayerShip> Spawned => _spawned;

        void Start() => SpawnLocalPlayer();

        public PlayerShip SpawnLocalPlayer()
        {
            int index = _spawned.Count;
            var player = Instantiate(shipPrefab, PlaneMath.ToWorld(spawnPosition), Quaternion.identity);
            player.name = $"Player {index + 1}";
            player.Initialize(ship, primaryWeapon);
            player.Bind(world, index);
            player.IsLocal = true;
            player.CommandSource = player.gameObject.AddComponent<LocalInputCommandSource>();

            _spawned.Add(player);
            return player;
        }
    }
}
