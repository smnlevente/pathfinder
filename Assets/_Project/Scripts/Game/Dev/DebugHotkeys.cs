using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Dev
{
    /// <summary>
    /// Development shortcuts (GDD §14.5) for trying loadouts and testing waves. Active in the Editor and in
    /// development builds only. They act on the local pilots.
    /// </summary>
    public sealed class DebugHotkeys : MonoBehaviour
    {
        [SerializeField] WaveDirector waveDirector;
        [SerializeField, Tooltip("Optional. In the Run scene, F6 skips the current level.")] LevelController level;
        [SerializeField, Tooltip("Optional. In Survival, F6 moves the clock on a minute.")] SurvivalController survival;
        [SerializeField, Tooltip("F7 offers a choice of 3 random items from here.")] ContentDatabase content;
        [SerializeField] EquipmentChoiceScreen equipmentChoice;
        [SerializeField, Tooltip("Optional. Refreshed after F8 so newly affordable Shop items light up.")] ChoiceDialog dialog;
        [SerializeField] PrimaryWeaponDefinition[] primaryWeapons = new PrimaryWeaponDefinition[0];
        [SerializeField] SecondaryWeaponDefinition[] secondaryWeapons = new SecondaryWeaponDefinition[0];

        public string HintText => Localization.Loc.T(level != null ? "dev.hint" : survival != null ? "dev.hint.survival" : "dev.hint.sandbox");

        Pathfinder.Core.Random.IRandom _random = new Pathfinder.Core.Random.Pcg32(7UL, 7UL);
        public ContentDatabase Content => content;
        public IReadOnlyList<PrimaryWeaponDefinition> PrimaryWeapons => primaryWeapons;
        public IReadOnlyList<SecondaryWeaponDefinition> SecondaryWeapons => secondaryWeapons;

        readonly List<Enemy> _scratch = new List<Enemy>();
        int _nextSpawn;

        void Awake()
        {
            if (!Debug.isDebugBuild)
                enabled = false;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame)
                ForEachLocalPlayer(p => p.EquipPrimary(Next(primaryWeapons, p.PrimaryWeapon)));
            if (keyboard.f2Key.wasPressedThisFrame)
                ForEachLocalPlayer(p => p.EquipSecondary(Next(secondaryWeapons, p.SecondaryWeapon)));
            if (keyboard.f3Key.wasPressedThisFrame)
                ForEachLocalPlayer(p => p.GodMode = !p.GodMode);
            if (keyboard.f4Key.wasPressedThisFrame)
                KillAllEnemies();
            if (keyboard.f5Key.wasPressedThisFrame && waveDirector != null)
                waveDirector.SkipToNextWave();
            if (keyboard.f6Key.wasPressedThisFrame && level != null && level.IsActive)
                level.DebugTakeExit();
            if (keyboard.f6Key.wasPressedThisFrame && survival != null)
                survival.DebugAdvance(60f);
            if (keyboard.f7Key.wasPressedThisFrame)
                OfferRandomEquipment();
            if (keyboard.f8Key.wasPressedThisFrame)
                GrantScraps(100);
            if (keyboard.f9Key.wasPressedThisFrame)
                Localization.Loc.Reload(); // pick up edits to the .loc files without restarting
            if (keyboard.f10Key.wasPressedThisFrame)
                SpawnNextEnemyType();
        }

        /// <summary>Gives every local pilot Scraps, e.g. to try the Shop. Works while menus are open.</summary>
        public void GrantScraps(int amount)
        {
            // A grant isn't collected, so it doesn't bank 10% back as Dark Matter.
            ForEachLocalPlayer(p => p.AddStartingScraps(amount));
            if (dialog != null && dialog.IsOpen)
                dialog.RefreshOptions();
        }

        /// <summary>
        /// Spawns the next enemy type from the content database (bosses included) a few units ahead of the pilot, cycling
        /// through all of them, to try any enemy without waiting for it to come up in a wave. Returns what it spawned.
        /// </summary>
        public Enemy SpawnNextEnemyType()
        {
            var world = waveDirector != null ? waveDirector.World : null;
            if (content == null || world == null || content.Enemies.Length == 0)
                return null;

            PlayerShip pilot = null;
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal && player.IsAlive)
                    pilot = player;
            }
            if (pilot == null)
                return null;

            var definition = content.Enemies[_nextSpawn % content.Enemies.Length];
            _nextSpawn++;
            Vector2 position = world.ClampToArena(pilot.Position + pilot.AimDirection * 7f, 2f);
            Debug.Log($"[Dev] Spawned {definition.Id}");
            return world.SpawnEnemy(definition, position);
        }

        void OfferRandomEquipment()
        {
            if (content == null || equipmentChoice == null || equipmentChoice.IsOpen || content.Equipment.Length == 0)
                return;

            PlayerShip pilot = null;
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal && player.IsAlive)
                    pilot = player;
            }
            if (pilot == null)
                return;

            var offer = new System.Collections.Generic.List<Pathfinder.Game.Equipment.EquipmentDefinition>();
            var pool = new System.Collections.Generic.List<Pathfinder.Game.Equipment.EquipmentDefinition>(content.Equipment);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int index = Pathfinder.Core.Random.RandomExtensions.Range(_random, 0, pool.Count);
                offer.Add(pool[index]);
                pool.RemoveAt(index);
            }
            equipmentChoice.Show(Localization.Loc.T("choice.debug"), offer, pilot, 0, null);
        }

        static T Next<T>(T[] options, T current) where T : Object
        {
            if (options.Length == 0)
                return current;
            int index = System.Array.IndexOf(options, current);
            return options[(index + 1) % options.Length];
        }

        static void ForEachLocalPlayer(System.Action<PlayerShip> action)
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal && player.IsAlive)
                    action(player);
            }
        }

        void KillAllEnemies()
        {
            _scratch.Clear();
            _scratch.AddRange(CombatRegistry.Enemies);
            foreach (var enemy in _scratch)
                enemy.ApplyDamage(new DamageInfo(1_000_000f, Team.Player, 0, enemy.Position, Vector2.up));
        }
    }
}
