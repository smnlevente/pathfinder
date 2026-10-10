using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Broodling: a small Swarm creature born by the Hive Matriarch. It chases the ship in a weaving, erratic line and now
    /// and then darts forward, so a brood flows around the ship rather than marching at it (GDD §2: the Swarm moves
    /// fluidly). It bites by touch.
    /// </summary>
    public sealed class BroodlingBrain : EnemyBrain
    {
        [SerializeField, Min(0f), Tooltip("Side-to-side weave, as a fraction of its speed.")] float weave = 0.8f;
        [SerializeField, Min(0.1f)] float weaveFrequency = 3.2f;
        [SerializeField, Min(0.1f)] float dartIntervalMin = 1.6f;
        [SerializeField, Min(0.1f)] float dartIntervalMax = 3f;
        [SerializeField, Min(0f)] float dartTime = 0.35f;
        [SerializeField, Min(1f)] float dartSpeed = 1.9f;

        float _phase, _time, _dartTimer, _dartLeft;

        public override void ResetState(Enemy self)
        {
            _phase = self.Random.Range(0f, Mathf.PI * 2f);
            _time = 0f;
            _dartTimer = self.Random.Range(dartIntervalMin, dartIntervalMax);
            _dartLeft = 0f;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            var target = Target(self);
            if (target == null)
                return Vector2.zero;

            _time += deltaTime;
            Vector2 toTarget = target.Position - self.Position;
            Vector2 forward = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector2.up;
            Vector2 side = new Vector2(-forward.y, forward.x);

            _dartTimer -= deltaTime * self.AttackRate;
            if (_dartTimer <= 0f)
            {
                _dartLeft = dartTime;
                _dartTimer = self.Random.Range(dartIntervalMin, dartIntervalMax);
            }
            if (_dartLeft > 0f)
            {
                _dartLeft -= deltaTime;
                self.SpeedMultiplier = dartSpeed;
                self.AccelerationMultiplier = 3f;
                return forward * self.MoveSpeed * dartSpeed;
            }

            Vector2 direction = forward + side * (weave * Mathf.Sin(_time * weaveFrequency + _phase));
            return direction.normalized * self.MoveSpeed;
        }
    }
}
