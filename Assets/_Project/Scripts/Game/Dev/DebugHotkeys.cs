using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
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
        [SerializeField] PrimaryWeaponDefinition[] primaryWeapons = new PrimaryWeaponDefinition[0];
        [SerializeField] SecondaryWeaponDefinition[] secondaryWeapons = new SecondaryWeaponDefinition[0];

        public string HintText => "DEV   F1 primary   F2 secondary   F3 god mode   F4 kill all   F5 next wave";
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
