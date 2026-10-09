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

        public float Radius => radius;
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

        protected virtual void Awake()
        {
            Position = _previousPosition = PlaneMath.ToPlane(transform.position);
            Heading = _previousHeading = transform.eulerAngles.y;
        }

        protected virtual void OnEnable() => EntityId = CombatRegistry.Register(this);

        protected virtual void OnDisable() => CombatRegistry.Unregister(this);

        public abstract void Simulate(float deltaTime, CombatWorld world);

        /// <summary>Applies damage. Authority only. On clients this returns an empty result.</summary>
        public DamageResult ApplyDamage(in DamageInfo info)
        {
            if (!Authority.IsAuthority || !IsAlive)
                return default;

            var result = Vitals.ApplyDamage(info.Amount);
            if (result.Blocked || result.TotalDamage > 0f)
                Damaged?.Invoke(this, info, result);

            if (result.Killed)
            {
                OnKilled(info);
                Died?.Invoke(this, info);
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

        protected virtual void Update() => SyncTransform(SimInterpolation.Alpha);

        void SyncTransform(float alpha)
        {
            transform.SetPositionAndRotation(
                PlaneMath.ToWorld(Vector2.Lerp(_previousPosition, Position, alpha)),
                Quaternion.Euler(0f, Mathf.LerpAngle(_previousHeading, Heading, alpha), 0f));
        }
    }

    public readonly struct DamageInfo
    {
        public float Amount { get; }
        public Team SourceTeam { get; }
        public int SourceId { get; }
        public Vector2 Point { get; }
        public Vector2 Direction { get; }

        public DamageInfo(float amount, Team sourceTeam, int sourceId, Vector2 point, Vector2 direction)
        {
            Amount = amount;
            SourceTeam = sourceTeam;
            SourceId = sourceId;
            Point = point;
            Direction = direction;
        }
    }
}
