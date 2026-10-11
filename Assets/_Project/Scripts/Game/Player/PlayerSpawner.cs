using System.Collections.Generic;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Spawns the pilots for a level: one for each local player (M8, local co-op), side by side, each listening to their
    /// own devices through a <see cref="LocalInputCommandSource"/>. Co-op pilots share one Scraps wallet and wear a ring in
    /// their colour. A remote co-op pilot would get a network command source instead.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField] PlayerShip shipPrefab;
        [SerializeField] ShipDefinition ship;
        [SerializeField] PrimaryWeaponDefinition primaryWeapon;
        [SerializeField] SecondaryWeaponDefinition secondaryWeapon;
        [SerializeField] Vector2 spawnPosition = new Vector2(0f, -6f);
        [SerializeField, Tooltip("A ring in the pilot's colour under each ship, shown in co-op.")] GameObject pilotRingPrefab;

        [Header("Run")]
        [SerializeField, Tooltip("Use the loadout chosen in the Hub, when a run session has one.")] bool useRunLoadout;
        [SerializeField] ContentDatabase content;

        readonly List<PlayerShip> _spawned = new List<PlayerShip>();

        public IReadOnlyList<PlayerShip> Spawned => _spawned;

        void Start()
        {
            for (int i = 0; i < LocalPlayers.Count; i++)
                SpawnLocalPlayer();
        }

        public PlayerShip SpawnLocalPlayer()
        {
            int index = _spawned.Count;
            int count = Mathf.Max(LocalPlayers.Count, index + 1);
            var player = Instantiate(shipPrefab, PlaneMath.ToWorld(spawnPosition + CoopRules.EntryOffset(index, count)), Quaternion.identity);
            player.name = $"Player {index + 1}";
            // A continued run puts each pilot back as saved (M7).
            var saved = useRunLoadout && content != null ? RunSession.TakePendingPilot(index) : null;
            ResolveLoadout(index, saved, out var shipToUse, out var primaryToUse, out var secondaryToUse);
            player.Initialize(shipToUse, primaryToUse, secondaryToUse);
            player.Bind(world, index);
            if (useRunLoadout && content != null)
                MetaProgression.ApplyRunBonuses(player, content); // Hub upgrades only apply to real runs
            if (saved != null)
                RunSaveFile.ApplyPilot(saved, player, content);
            // Co-op pilots share player 1's Scraps; Lab starting Scraps count once (M8).
            if (index > 0)
                player.ShareWallet(_spawned[0].Wallet);
            player.IsLocal = true;
            var input = player.gameObject.AddComponent<LocalInputCommandSource>();
            input.Bind(index);
            player.CommandSource = input;
            if (pilotRingPrefab != null)
                Instantiate(pilotRingPrefab, player.transform, false);

            _spawned.Add(player);
            return player;
        }

        /// <summary>
        /// Player 1 flies the run's loadout; the others what they picked in the Hub (both resolved by content ID), or what
        /// they flew when the run was saved. Otherwise, the scene defaults.
        /// </summary>
        void ResolveLoadout(int index, RunSaveFile.Pilot saved, out ShipDefinition shipToUse, out PrimaryWeaponDefinition primaryToUse,
            out SecondaryWeaponDefinition secondaryToUse)
        {
            shipToUse = ship;
            primaryToUse = primaryWeapon;
            secondaryToUse = secondaryWeapon;
            if (!useRunLoadout || content == null)
                return;

            var loadout = saved != null ? new PilotLoadout(saved.ship, saved.primary, saved.secondary)
                : index == 0 ? RunSession.Current?.Loadout ?? default
                : LocalPlayers.LoadoutFor(index);
            if (loadout.IsEmpty)
                loadout = RunSession.Current?.Loadout ?? default;
            if (loadout.IsEmpty)
                return;

            shipToUse = content.FindShip(loadout.ShipId) ?? ship;
            primaryToUse = content.FindPrimary(loadout.PrimaryId) ?? primaryWeapon;
            secondaryToUse = content.FindSecondary(loadout.SecondaryId) ?? secondaryWeapon;
        }
    }
}
