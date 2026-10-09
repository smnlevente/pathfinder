using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>Drone: rushes the nearest player with a slight weave and deals contact damage (GDD §7.3).</summary>
    public sealed class DroneBrain : EnemyBrain
    {
        [SerializeField, Range(0f, 1f), Tooltip("Sideways weave as a fraction of move speed.")] float weave = 0.35f;
        [SerializeField, Min(0.1f)] float weaveFrequency = 1.6f;

        float _phase;
        float _time;

        public override void ResetState(Enemy self)
        {
            _phase = self.Random.NextFloat() * Mathf.PI * 2f;
            _time = 0f;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            _time += deltaTime;
            var target = Target(self);
            if (target == null)
                return Vector2.zero;

            Vector2 toTarget = target.Position - self.Position;
            if (toTarget.sqrMagnitude < 1e-6f)
                return Vector2.zero;

            Vector2 forward = toTarget.normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            float sway = Mathf.Sin(_time * weaveFrequency * Mathf.PI * 2f + _phase) * weave;
            return (forward + side * sway).normalized * self.MoveSpeed;
        }
    }
}
