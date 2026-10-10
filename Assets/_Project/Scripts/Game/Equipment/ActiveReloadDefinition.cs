using Pathfinder.Core.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>
    /// Precision Loader: an active reload (playtest 6, after 20 Minutes Till Dawn). While a magazine weapon reloads, a gold
    /// window shows on the ship's ammo ring; press Reload as the reload passes through it and the reload finishes at once.
    /// From level II the next magazine also hits harder. Press too early or too late and it jams for a moment; there's
    /// one try per reload. Only offered with magazine weapons.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Equipment/Precision Loader", fileName = "Equipment_PrecisionLoader")]
    public sealed class ActiveReloadDefinition : EquipmentDefinition
    {
        [field: SerializeField, Range(0f, 1f), Tooltip("Centre of the window, as a fraction of the reload.")] public float WindowCentre { get; private set; } = 0.6f;
        [field: SerializeField, Tooltip("Width of the window at levels I, II, III, as a fraction of the reload.")] public float[] WindowWidth { get; private set; } = { 0.14f, 0.18f, 0.22f };
        [field: SerializeField, Tooltip("Extra damage for the magazine a perfect reload loads, at levels I, II, III.")] public float[] BonusDamage { get; private set; } = { 0f, 0.15f, 0.3f };
        [field: SerializeField, Min(0f), Tooltip("Seconds a missed press adds to the reload.")] public float JamSeconds { get; private set; } = 0.4f;

        public override EquipmentEffect CreateEffect() => new Effect(this);

        /// <summary>Useless without a magazine (the Quantum Charger), so it isn't offered then.</summary>
        public override bool SuitsPilot(PlayerShip pilot) => pilot.PrimaryWeapon == null || pilot.PrimaryWeapon.FireMode != PrimaryFireMode.Charge;

        public override string Describe(int level)
        {
            float bonus = AtLevel(BonusDamage, level);
            string bonusText = bonus > 0f ? Localization.Loc.T($"{Id}.desc.bonus", ("percent", bonus * 100f)) : string.Empty;
            return Localization.Loc.T($"{Id}.desc", ("bonus", bonusText), ("jam", JamSeconds));
        }

        /// <summary>The window at a level, as (start, end) fractions of the reload.</summary>
        public Vector2 Window(int level)
        {
            float half = AtLevel(WindowWidth, level) * 0.5f;
            return new Vector2(Mathf.Clamp01(WindowCentre - half), Mathf.Clamp01(WindowCentre + half));
        }

        sealed class Effect : EquipmentEffect
        {
            readonly ActiveReloadDefinition _definition;
            bool _tried;
            int _bonusShots;

            public Effect(ActiveReloadDefinition definition) => _definition = definition;

            /// <summary>Shown while a reload runs and the pilot hasn't tried yet.</summary>
            public override Vector2? ReloadWindow =>
                !_tried && Ship.Primary is AutomaticPrimary weapon && weapon.Magazine.IsReloading ? _definition.Window(Level) : (Vector2?)null;

            public override void OnReloadPressedWhileReloading(MagazineWeapon magazine)
            {
                if (_tried)
                    return;
                _tried = true;

                var window = _definition.Window(Level);
                bool perfect = magazine.ReloadProgress >= window.x && magazine.ReloadProgress <= window.y;
                if (perfect)
                {
                    magazine.FinishReload();
                    _bonusShots = AtLevel(_definition.BonusDamage, Level) > 0f ? magazine.MagazineSize : 0;
                }
                else
                {
                    magazine.DelayReload(_definition.JamSeconds);
                }
                Ship.NotifyActiveReload(perfect);
            }

            public override void OnShotFired(ref ShotParams shot)
            {
                // Firing means the reload is over: the next one gets a fresh try.
                _tried = false;
                if (_bonusShots <= 0)
                    return;
                _bonusShots--;
                shot.DamageMultiplier *= 1f + AtLevel(_definition.BonusDamage, Level);
            }
        }
    }
}
