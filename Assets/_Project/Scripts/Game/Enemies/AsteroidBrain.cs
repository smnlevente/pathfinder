using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// The Shattered Expanse's hazard (M7): a chunk of rock drifting across the arena, bouncing off the walls and tumbling
    /// as it goes. It hurts what it hits and blocks shots, so it's cover as much as danger, and it breaks for a scrap.
    /// No aim and no mind.
    /// </summary>
    public sealed class AsteroidBrain : EnemyBrain
    {
        [SerializeField, Tooltip("The model, tumbled for show.")] Transform rock;
        [SerializeField] float tumbleSpeed = 40f;

        Vector2 _direction;
        Vector3 _tumbleAxis;

        public override void ResetState(Enemy self)
        {
            // Roughly toward the middle, so it crosses the arena instead of hugging a wall.
            Vector2 toCentre = -self.Position;
            float spread = self.Random.Range(-50f, 50f);
            _direction = toCentre.sqrMagnitude > 1f ? PlaneMath.Rotate(toCentre.normalized, spread) : PlaneMath.FromHeading(self.Random.Range(0f, 360f));
            _tumbleAxis = new Vector3(self.Random.Range(-1f, 1f), self.Random.Range(-0.3f, 0.3f), self.Random.Range(-1f, 1f)).normalized;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            // Bounce off the walls.
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(self.Radius + 0.2f, self.Radius + 0.2f);
            if ((self.Position.x <= -half.x && _direction.x < 0f) || (self.Position.x >= half.x && _direction.x > 0f))
                _direction.x = -_direction.x;
            if ((self.Position.y <= -half.y && _direction.y < 0f) || (self.Position.y >= half.y && _direction.y > 0f))
                _direction.y = -_direction.y;

            if (rock != null && _tumbleAxis != Vector3.zero)
                rock.Rotate(_tumbleAxis, tumbleSpeed * deltaTime, Space.World);
            return _direction * self.MoveSpeed;
        }
    }
}
