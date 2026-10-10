using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// One body segment of a multi-part boss. It is hittable and deals contact damage, but shares its owner's health:
    /// hits pass to the owner, scaled by whether the segment is armoured or a weak point. Its owner's brain positions it.
    /// </summary>
    public sealed class BossSegment : CombatEntity
    {
        const float ContactCooldown = 0.75f;

        [SerializeField, Min(0f)] float armouredMultiplier = 0.5f;
        [SerializeField, Min(0f)] float weakPointMultiplier = 2f;
        [SerializeField] GameObject shatterEffect;
        [SerializeField, Tooltip("Shown only on weak-point segments.")] GameObject weakPointMarker;

        Enemy _owner;
        float _contactCooldown;

        public override Team Team => Team.Enemy;
        public override Vitals Vitals => _owner != null ? _owner.Vitals : null;
        public override CombatEntity DamageOwner => _owner;
        public override bool IsIntangible => _owner == null || _owner.IsIntangible;

        public Enemy Owner => _owner;
        public bool IsWeakPoint { get; private set; }

        public void Attach(Enemy owner, bool weakPoint)
        {
            _owner = owner;
            IsWeakPoint = weakPoint;
            if (weakPointMarker != null)
                weakPointMarker.SetActive(weakPoint);
        }

        /// <summary>Moves the segment along its owner's body. Called by the owner's brain each tick.</summary>
        public void Follow(Vector2 position, Vector2 facing)
        {
            Position = position;
            if (facing.sqrMagnitude > 1e-6f)
                Heading = PlaneMath.HeadingDegrees(facing);
        }

        /// <summary>Breaks apart when the boss dies. Presentation only.</summary>
        public void Shatter()
        {
            if (shatterEffect != null)
                Instantiate(shatterEffect, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            Destroy(gameObject);
        }

        protected override float IncomingDamageMultiplier(in DamageInfo info) => IsWeakPoint ? weakPointMultiplier : armouredMultiplier;

        public override void Simulate(float deltaTime, CombatWorld world)
        {
            _contactCooldown -= deltaTime;
            if (_contactCooldown > 0f || IsIntangible || _owner == null)
                return;

            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive || player.IsIntangible)
                    continue;

                Vector2 toPlayer = player.Position - Position;
                float reach = Radius + player.Radius;
                if (toPlayer.sqrMagnitude > reach * reach)
                    continue;

                Vector2 direction = toPlayer.sqrMagnitude > 1e-6f ? toPlayer.normalized : Vector2.up;
                player.ApplyDamage(new DamageInfo(_owner.Definition.ContactDamage, Team, _owner.EntityId, Position, direction, DamageKind.Contact));
                _contactCooldown = ContactCooldown;
                return;
            }
        }
    }
}
