using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Spinner: slow, holds mid range and fires rings of bullets whose angle rotates each volley, so the gaps
    /// move (GDD §7.3).
    /// </summary>
    public sealed class SpinnerBrain : EnemyBrain
    {
        [Header("Positioning")]
        [SerializeField, Min(0f)] float preferredMinDistance = 6f;
        [SerializeField, Min(0f)] float preferredMaxDistance = 13f;
        [SerializeField, Range(0f, 1f)] float driftFactor = 0.4f;

        [Header("Attack")]
        [SerializeField, Min(0.1f)] float ringInterval = 1.8f;
        [SerializeField, Min(0f)] float telegraphTime = 0.35f;
        [SerializeField, Min(3)] int bulletsPerRing = 10;
        [SerializeField, Tooltip("Degrees the ring rotates between volleys.")] float ringRotationStep = 11f;
        [SerializeField, Min(0f)] float attackRange = 18f;

        [Header("Bullet")]
        [SerializeField, Min(0f)] float bulletDamage = 8f;
        [SerializeField, Min(0.1f)] float bulletSpeed = 6f;
        [SerializeField, Min(0.01f)] float bulletRadius = 0.25f;
        [SerializeField, Min(0.1f)] float bulletRange = 20f;
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] GameObject bulletImpactPrefab;

        float _timer;
        float _ringAngle;
        float _driftSign;

        public override void ResetState(Enemy self)
        {
            _timer = self.Random.Range(0.8f, ringInterval + 0.8f);
            _ringAngle = self.Random.Range(0f, 360f);
            _driftSign = self.Random.Chance(0.5f) ? 1f : -1f;
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            var target = Target(self);
            if (target == null)
            {
                self.IsTelegraphing = false;
                return Vector2.zero;
            }

            Vector2 toTarget = target.Position - self.Position;
            float distance = toTarget.magnitude;
            Vector2 forward = distance > 1e-4f ? toTarget / distance : Vector2.up;

            _timer -= deltaTime * self.AttackRate;
            self.IsTelegraphing = _timer <= telegraphTime && distance <= attackRange;
            if (_timer <= 0f)
            {
                if (distance <= attackRange)
                    FireRing(self, world);
                _timer = ringInterval + self.Random.Range(-0.2f, 0.2f);
            }

            float radial = distance > preferredMaxDistance ? 1f : distance < preferredMinDistance ? -1f : 0f;
            Vector2 drift = new Vector2(-forward.y, forward.x) * (_driftSign * driftFactor);
            return Vector2.ClampMagnitude(forward * radial + drift, 1f) * self.MoveSpeed;
        }

        void FireRing(Enemy self, CombatWorld world)
        {
            float step = 360f / bulletsPerRing;
            for (int i = 0; i < bulletsPerRing; i++)
            {
                Vector2 direction = PlaneMath.FromHeading(_ringAngle + i * step);
                world.Projectiles.Spawn(new ProjectileSpawn(
                    self.Position + direction * (self.Radius + 0.2f), direction, bulletSpeed, bulletDamage, bulletRadius, bulletRange,
                    self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));
            }
            _ringAngle += ringRotationStep;
        }
    }
}
