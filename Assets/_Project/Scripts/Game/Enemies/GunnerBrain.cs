using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Gunner: keeps 8–12 u from the nearest player, strafes, and fires telegraphed 3-shot bursts aimed at
    /// the player's current position (GDD §7.3).
    /// </summary>
    public sealed class GunnerBrain : EnemyBrain
    {
        [Header("Positioning")]
        [SerializeField, Min(0f)] float preferredMinDistance = 8f;
        [SerializeField, Min(0f)] float preferredMaxDistance = 12f;
        [SerializeField, Range(0f, 1f)] float strafeSpeedFactor = 0.6f;
        [SerializeField, Min(0.1f)] float strafeSwitchMin = 1.5f;
        [SerializeField, Min(0.1f)] float strafeSwitchMax = 3.5f;

        [Header("Attack")]
        [SerializeField, Min(0.1f)] float fireCooldown = 2f;
        [SerializeField, Min(0f)] float telegraphTime = 0.45f;
        [SerializeField, Min(1)] int burstCount = 3;
        [SerializeField, Min(0.01f)] float burstInterval = 0.15f;
        [SerializeField, Min(0f), Tooltip("Only start an attack when the target is this close.")] float attackRange = 18f;
        [SerializeField, Range(0f, 1f), Tooltip("Move speed multiplier while winding up and firing.")] float attackMoveFactor = 0.3f;

        [Header("Bullet")]
        [SerializeField, Min(0f)] float bulletDamage = 8f;
        [SerializeField, Min(0.1f)] float bulletSpeed = 11f;
        [SerializeField, Min(0.01f)] float bulletRadius = 0.25f;
        [SerializeField, Min(0.1f)] float bulletRange = 24f;
        [SerializeField] GameObject bulletPrefab;
        [SerializeField] GameObject bulletImpactPrefab;
        [SerializeField, Min(0f)] float muzzleDistance = 0.8f;

        float _cooldown;
        float _telegraphRemaining;
        int _shotsRemaining;
        float _burstTimer;
        float _strafeSign;
        float _strafeTimer;

        bool IsAttacking => _telegraphRemaining > 0f || _shotsRemaining > 0;

        public GameObject BulletPrefab => bulletPrefab;

        public override void ResetState(Enemy self)
        {
            _cooldown = self.Random.Range(0.6f, fireCooldown);
            _telegraphRemaining = 0f;
            _shotsRemaining = 0;
            _strafeSign = self.Random.Chance(0.5f) ? 1f : -1f;
            _strafeTimer = self.Random.Range(strafeSwitchMin, strafeSwitchMax);
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
            self.FaceTowards(forward);

            UpdateAttack(self, deltaTime, world, target.Position, distance);
            return Move(self, deltaTime, forward, distance);
        }

        void UpdateAttack(Enemy self, float deltaTime, CombatWorld world, Vector2 targetPosition, float distance)
        {
            if (_shotsRemaining > 0)
            {
                _burstTimer -= deltaTime;
                if (_burstTimer <= 0f)
                {
                    Fire(self, world, targetPosition);
                    _shotsRemaining--;
                    _burstTimer = burstInterval;
                    if (_shotsRemaining == 0)
                        _cooldown = fireCooldown + self.Random.Range(-0.3f, 0.3f);
                }
                return;
            }

            if (_telegraphRemaining > 0f)
            {
                _telegraphRemaining -= deltaTime;
                if (_telegraphRemaining <= 0f)
                {
                    self.IsTelegraphing = false;
                    _shotsRemaining = burstCount;
                    _burstTimer = 0f;
                }
                return;
            }

            _cooldown -= deltaTime;
            if (_cooldown <= 0f && distance <= attackRange)
            {
                _telegraphRemaining = Mathf.Max(telegraphTime, 1e-3f);
                self.IsTelegraphing = true;
            }
        }

        Vector2 Move(Enemy self, float deltaTime, Vector2 forward, float distance)
        {
            _strafeTimer -= deltaTime;
            if (_strafeTimer <= 0f)
            {
                _strafeSign = -_strafeSign;
                _strafeTimer = self.Random.Range(strafeSwitchMin, strafeSwitchMax);
            }

            float radial = distance > preferredMaxDistance ? 1f : distance < preferredMinDistance ? -1f : 0f;
            Vector2 side = new Vector2(-forward.y, forward.x) * (_strafeSign * strafeSpeedFactor);
            Vector2 direction = forward * radial + side;

            float speed = self.Definition.MoveSpeed * (IsAttacking ? attackMoveFactor : 1f);
            return Vector2.ClampMagnitude(direction, 1f) * speed;
        }

        void Fire(Enemy self, CombatWorld world, Vector2 targetPosition)
        {
            Vector2 toTarget = targetPosition - self.Position;
            Vector2 direction = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector2.up;

            world.Projectiles.Spawn(new ProjectileSpawn(
                self.Position + direction * muzzleDistance, direction, bulletSpeed, bulletDamage, bulletRadius, bulletRange,
                self.Team, self.EntityId, bulletPrefab, bulletImpactPrefab));
        }
    }
}
