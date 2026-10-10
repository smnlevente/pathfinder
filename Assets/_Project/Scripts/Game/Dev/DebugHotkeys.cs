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
        [SerializeField, Tooltip("F7 offers a choice of 3 random items from here.")] ContentDatabase content;
        [SerializeField] EquipmentChoiceScreen equipmentChoice;
        [SerializeField] PrimaryWeaponDefinition[] primaryWeapons = new PrimaryWeaponDefinition[0];
        [SerializeField] SecondaryWeaponDefinition[] secondaryWeapons = new SecondaryWeaponDefinition[0];

        public string HintText => level != null
            ? "DEV   F1 primary   F2 secondary   F3 god   F4 kill all   F5 next wave   F6 skip level   F7 equipment"
            : "DEV   F1 primary   F2 secondary   F3 god   F4 kill all   F5 next wave   F7 equipment";

        Pathfinder.Core.Random.IRandom _random = new Pathfinder.Core.Random.Pcg32(7UL, 7UL);
        public ContentDatabase Content => content;
        public IReadOnlyList<PrimaryWeaponDefinition> PrimaryWeapons => primaryWeapons;
        public IReadOnlyList<SecondaryWeaponDefinition> SecondaryWeapons => secondaryWeapons;

        readonly List<Enemy> _scratch = new List<Enemy>();

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
            if (keyboard.f7Key.wasPressedThisFrame)
                OfferRandomEquipment();
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
            equipmentChoice.Show("DEV: EQUIPMENT", offer, pilot, 0, null);
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
