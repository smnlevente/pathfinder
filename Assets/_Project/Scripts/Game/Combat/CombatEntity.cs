using System;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// Anything that can be hit: player ships and enemies. Simulation state (position, heading, vitals)
    /// is advanced only by <see cref="CombatWorld"/> on the fixed tick. The transform is presentation
    /// and is interpolated between ticks.
    /// </summary>
    public abstract class CombatEntity : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] float radius = 0.5f;

        /// <summary>Runtime ID assigned by the authority. Stable for the entity's lifetime, network-ready.</summary>
        public int EntityId { get; private set; }

        public abstract Team Team { get; }
        public abstract Vitals Vitals { get; }

        public float Radius => radius * _radiusScale;
        public Vector2 Position { get; protected set; }

        /// <summary>Yaw in degrees, 0 = +Z. See <see cref="PlaneMath.HeadingDegrees"/>.</summary>
        public float Heading { get; protected set; }

        public bool IsAlive => Vitals != null && !Vitals.IsDead;

        /// <summary>Projectiles and contact damage pass straight through, e.g. while dashing.</summary>
        public virtual bool IsIntangible => false;

        /// <summary>Raised for every hit, including blocked ones (check <see cref="DamageResult.Blocked"/>).</summary>
        public event Action<CombatEntity, DamageInfo, DamageResult> Damaged;
        public event Action<CombatEntity, DamageInfo> Died;

        Vector2 _previousPosition;
        float _previousHeading;
        float _radiusScale = 1f;

        protected virtual void Awake()
        {
            Position = _previousPosition = PlaneMath.ToPlane(transform.position);
            Heading = _previousHeading = transform.eulerAngles.y;
        }

        protected virtual void OnEnable() => EntityId = CombatRegistry.Register(this);

        protected virtual void OnDisable() => CombatRegistry.Unregister(this);

        public abstract void Simulate(float deltaTime, CombatWorld world);

        /// <summary>
        /// For parts of a larger body, such as boss segments: hits pass to the owner, scaled by the part's own multiplier
        /// (e.g. weak points). Null for standalone entities.
        /// </summary>
        public virtual CombatEntity DamageOwner => null;

        /// <summary>Applies damage. Authority only. On clients this returns an empty result.</summary>
        public DamageResult ApplyDamage(in DamageInfo info)
        {
            if (!Authority.IsAuthority || !IsAlive)
                return default;

            var owner = DamageOwner;
            if (owner != null)
                return owner.ApplyDamage(new DamageInfo(info.Amount * IncomingDamageMultiplier(info), info.SourceTeam, info.SourceId, info.Point, info.Direction, info.Kind));

            var result = Vitals.ApplyDamage(info.Amount * IncomingDamageMultiplier(info));
            if (result.Killed && TryPreventDeath(info))
            {
                Vitals.Revive(1f);
                result = new DamageResult(result.ShieldDamage, result.HullDamage, killed: false, blocked: false);
            }

            if (result.Blocked || result.TotalDamage > 0f)
            {
                Damaged?.Invoke(this, info, result);
                CombatEvents.RaiseDamaged(this, info, result);
            }

            if (result.Killed)
            {
                OnKilled(info);
                Died?.Invoke(this, info);
                CombatEvents.RaiseKilled(this, info);
            }
            return result;
        }

        /// <summary>Moves instantly without interpolating from the old position.</summary>
        public void Teleport(Vector2 position)
        {
            Position = _previousPosition = position;
            SyncTransform(1f);
        }

        internal void CapturePreviousPose()
        {
            _previousPosition = Position;
            _previousHeading = Heading;
        }

        protected virtual void OnKilled(in DamageInfo info) { }

        /// <summary>Last chance to cancel a lethal hit, e.g. Last Stand. Returning true leaves the entity at 1 hull.</summary>
        protected virtual bool TryPreventDeath(in DamageInfo info) => false;

        /// <summary>Directional armour and weak points, e.g. a Carrier's exposed rear.</summary>
        protected virtual float IncomingDamageMultiplier(in DamageInfo info) => 1f;

        /// <summary>Scales the hit radius, e.g. for larger elites.</summary>
        protected void SetRadiusScale(float scale) => _radiusScale = scale;

        protected virtual void Update() => SyncTransform(SimInterpolation.Alpha);

        void SyncTransform(float alpha)
        {
            transform.SetPositionAndRotation(
                PlaneMath.ToWorld(Vector2.Lerp(_previousPosition, Position, alpha)),
                Quaternion.Euler(0f, Mathf.LerpAngle(_previousHeading, Heading, alpha), 0f));
        }
    }

    /// <summary>
    /// What kind of attack dealt the damage. On-hit effects trigger only on primary projectile hits, so procs such as
    /// lightning or ricochets can never chain off each other.
    /// </summary>
    public enum DamageKind
    {
        Projectile,
        Secondary,
        Contact,
        Proc,
        Burn,
    }

    public readonly struct DamageInfo
    {
        public float Amount { get; }
        public Team SourceTeam { get; }
        public int SourceId { get; }
        public Vector2 Point { get; }
        public Vector2 Direction { get; }
        public DamageKind Kind { get; }

        public DamageInfo(float amount, Team sourceTeam, int sourceId, Vector2 point, Vector2 direction, DamageKind kind = DamageKind.Projectile)
        {
            Amount = amount;
            SourceTeam = sourceTeam;
            SourceId = sourceId;
            Point = point;
            Direction = direction;
            Kind = kind;
        }
    }

    /// <summary>
    /// Damage and kill notifications for all entities, for effects that react to what a pilot does, such as on-hit
    /// and on-kill equipment. Raised on the authority only.
    /// </summary>
    public static class CombatEvents
    {
        public static event Action<CombatEntity, DamageInfo, DamageResult> Damaged;
        public static event Action<CombatEntity, DamageInfo> Killed;

        internal static void RaiseDamaged(CombatEntity target, in DamageInfo info, DamageResult result) => Damaged?.Invoke(target, info, result);

        internal static void RaiseKilled(CombatEntity target, in DamageInfo info) => Killed?.Invoke(target, info);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Damaged = null;
            Killed = null;
        }
    }
}
