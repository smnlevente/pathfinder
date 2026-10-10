using Pathfinder.Core.Combat;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>
    /// The runtime behaviour of one equipped item or ship trait. The ship calls these hooks in equipment order on the
    /// authority's simulation tick (GDD §14.2: equipment registers modifiers and subscribes to OnKill, OnHit,
    /// OnDashStart and the other hooks). Effects are rebuilt from scratch whenever the loadout changes.
    /// </summary>
    public abstract class EquipmentEffect
    {
        public PlayerShip Ship { get; private set; }
        public int Level { get; private set; }

        protected CombatWorld World => Ship.World;
        protected IRandom Random => Ship.EffectRandom;

        internal void Bind(PlayerShip ship, int level)
        {
            Ship = ship;
            Level = level;
        }

        public virtual void ApplyStats(StatSheet stats) { }

        /// <summary>A new level began. Use it to reset per-level state, e.g. Last Stand.</summary>
        public virtual void OnLevelStart() { }

        /// <summary>Called once per trigger pull, before its projectiles spawn. Can add pierce, extra projectiles and so on.</summary>
        public virtual void OnShotFired(ref ShotParams shot) { }

        /// <summary>A primary-weapon projectile from this ship hit an enemy.</summary>
        public virtual void OnHit(CombatEntity target, in DamageInfo info, DamageResult result) { }

        /// <summary>This ship killed an enemy, by any means.</summary>
        public virtual void OnKill(Enemy enemy, in DamageInfo info) { }

        /// <summary>Return true to take over dash movement, e.g. Blink teleports instead of travelling.</summary>
        public virtual bool OverrideDashMovement(Vector2 direction) => false;

        public virtual void OnDashStart(Vector2 direction) { }
        public virtual void OnDashEnd() { }

        /// <summary>Intangibility that continues after the dash ends, e.g. Phase Dash.</summary>
        public virtual float ExtraDashIntangibility => 0f;

        /// <summary>An enemy shot passed through the ship while it was intangible.</summary>
        public virtual void OnPhasedThrough() { }

        /// <summary>Return true to survive a lethal hit at 1 hull, e.g. Last Stand.</summary>
        public virtual bool TryPreventLethal() => false;
    }

    /// <summary>One trigger pull of a primary weapon, which equipment can modify before its projectiles spawn.</summary>
    public struct ShotParams
    {
        /// <summary>Counts trigger pulls since the weapon was equipped, starting at 1.</summary>
        public int ShotIndex;
        public float DamageMultiplier;
        public int ExtraPierce;

        /// <summary>Extra projectiles fanned around each normal one, e.g. Split Shot.</summary>
        public int ExtraProjectiles;
        public float ExtraSpreadDegrees;
    }
}
