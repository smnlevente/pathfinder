using System.Collections.Generic;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Carrier: a slow tank that keeps its armoured front toward the player and launches Drones from its flanks.
    /// Its rear is a weak point, so flanking pays off (GDD §7.3).
    /// </summary>
    public sealed class CarrierBrain : EnemyBrain, IDamageModifier
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] float keepDistance = 10f;
        [SerializeField, Min(1f), Tooltip("Degrees per second. Slow turning is what makes flanking possible.")] float turnRate = 60f;

        [Header("Drones")]
        [SerializeField] EnemyDefinition launchedEnemy;
        [SerializeField, Min(0.5f)] float launchInterval = 4f;
        [SerializeField, Min(0f)] float launchTelegraph = 0.6f;
        [SerializeField, Min(1)] int launchCount = 2;
        [SerializeField, Min(1)] int maxLaunched = 6;

        [Header("Armour")]
        [SerializeField, Min(0f)] float frontDamageMultiplier = 0.6f;
        [SerializeField, Min(0f)] float rearDamageMultiplier = 2f;
        [SerializeField, Range(-1f, 1f), Tooltip("Hits whose direction from the centre has a dot with the facing below this count as rear hits.")]
        float rearThreshold = -0.3f;

        readonly List<Enemy> _launched = new List<Enemy>();
        float _timer;

        public override void ResetState(Enemy self)
        {
            _launched.Clear();
            _timer = self.Random.Range(1.5f, launchInterval);
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            _launched.RemoveAll(enemy => enemy == null || !enemy.IsAlive);

            var target = Target(self);
            if (target == null)
            {
                self.IsTelegraphing = false;
                return Vector2.zero;
            }

            Vector2 toTarget = target.Position - self.Position;
            self.TurnTowards(toTarget, turnRate * deltaTime);

            bool canLaunch = launchedEnemy != null && _launched.Count < maxLaunched;
            _timer -= deltaTime * self.AttackRate;
            self.IsTelegraphing = canLaunch && _timer <= launchTelegraph;
            if (_timer <= 0f)
            {
                if (canLaunch)
                    Launch(self, world);
                _timer = launchInterval;
            }

            // Drift toward a comfortable distance. The armoured front leads the way.
            float distance = toTarget.magnitude;
            float radial = distance > keepDistance + 1f ? 1f : distance < keepDistance - 1f ? -0.5f : 0f;
            return toTarget.normalized * (radial * self.MoveSpeed);
        }

        void Launch(Enemy self, CombatWorld world)
        {
            Vector2 back = -PlaneMath.FromHeading(self.Heading);
            for (int i = 0; i < launchCount && _launched.Count < maxLaunched; i++)
            {
                float side = launchCount > 1 ? Mathf.Lerp(-50f, 50f, i / (launchCount - 1f)) : 0f;
                Vector2 position = self.Position + PlaneMath.Rotate(back, side) * (self.Radius + 0.8f);
                // Launched drones drop nothing, so a Carrier can't be farmed for Scraps.
                _launched.Add(world.SpawnEnemy(launchedEnemy, world.ClampToArena(position, 0.5f), dropsLoot: false));
            }
        }

        public float IncomingDamageMultiplier(Enemy self, in DamageInfo info)
        {
            Vector2 fromCentre = info.Point - self.Position;
            if (fromCentre.sqrMagnitude < 1e-6f)
                return 1f;

            float facing = Vector2.Dot(fromCentre.normalized, PlaneMath.FromHeading(self.Heading));
            return facing < rearThreshold ? rearDamageMultiplier : frontDamageMultiplier;
        }
    }
}
