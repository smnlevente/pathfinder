using Pathfinder.Core.Random;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// Spore Drifter (M7, galaxy 3): a bloated Swarm spore sac that floats slowly after the ship, bobbing on a lazy
    /// sideways sway. When it dies it bursts into a spore cloud that slows and burns, so it's best popped at a distance;
    /// shot down up close, it leaves the cloud on top of you. A despawn (a level ending) leaves no cloud.
    /// </summary>
    public sealed class SporeDrifterBrain : EnemyBrain
    {
        [SerializeField, Min(0f), Tooltip("Sideways sway, as a fraction of its speed.")] float sway = 0.6f;
        [SerializeField, Min(0.1f)] float swayFrequency = 1.1f;

        [Header("Death cloud")]
        [SerializeField, Min(0.1f)] float cloudRadius = 2.2f;
        [SerializeField, Min(0.1f)] float cloudDuration = 5f;
        [SerializeField, Range(0.1f, 1f)] float cloudSlow = 0.6f;
        [SerializeField, Min(0f), Tooltip("Damage each burn tick to a ship inside (every 0.5 s).")] float cloudBurn = 4f;
        [SerializeField] GameObject cloudPrefab;
        [SerializeField, Tooltip("A one-shot flash where it bursts.")] GameObject burstPrefab;

        float _phase, _time;
        bool _subscribed;

        public float CloudRadius => cloudRadius;

        public override void ResetState(Enemy self)
        {
            _phase = self.Random.Range(0f, Mathf.PI * 2f);
            _time = 0f;
            if (!_subscribed)
            {
                _subscribed = true;
                self.Died += (entity, info) => Burst(self);
            }
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
            return (forward + side * (sway * Mathf.Sin(_time * swayFrequency + _phase))).normalized * self.MoveSpeed;
        }

        void Burst(Enemy self)
        {
            var world = self.World;
            if (world == null || !Authority.IsAuthority)
                return;
            Vector2 at = world.ClampToArena(self.Position, cloudRadius * 0.5f);
            world.Clouds.Spawn(new CloudSpawn(at, cloudRadius, cloudDuration, cloudSlow, cloudBurn, self.EntityId, cloudPrefab));
            GameAudio.Play(SoundCue.Erupt, PlaneMath.ToWorld(at));
            if (burstPrefab != null)
            {
                var burst = Instantiate(burstPrefab, PlaneMath.ToWorld(at), Quaternion.identity);
                if (burst.TryGetComponent(out Presentation.RingPulse pulse))
                    pulse.SizeMultiplier = cloudRadius;
            }
        }
    }
}
