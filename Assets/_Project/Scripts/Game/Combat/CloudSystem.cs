using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// Clouds left on the floor by enemies (M7: a Spore Drifter bursts into one when it dies). A cloud sits still for a
    /// while; a ship inside is slowed, and burns once every <see cref="BurnInterval"/>. Overlapping clouds don't stack:
    /// each burn tick, a ship takes the strongest burn of the clouds it is in. Owned by the world, so clouds outlive the
    /// enemy that left them and go when the level is cleared. Part of the fixed tick.
    /// </summary>
    public sealed class CloudSystem
    {
        public const float BurnInterval = 0.5f;

        sealed class Cloud
        {
            public CloudSpawn Spawn;
            public float Age;
            public GameObject Object;
            public SporeCloud View;
        }

        readonly List<Cloud> _clouds = new List<Cloud>();
        readonly Transform _visualRoot;
        float _burnTimer;

        public CloudSystem(Transform visualRoot) => _visualRoot = visualRoot;

        public int Count => _clouds.Count;

        /// <summary>The clouds' centres, for tests.</summary>
        public IEnumerable<Vector2> Positions
        {
            get
            {
                foreach (var cloud in _clouds)
                    yield return cloud.Spawn.Position;
            }
        }

        public void Spawn(in CloudSpawn spawn)
        {
            var cloud = new Cloud { Spawn = spawn };
            if (spawn.ViewPrefab != null)
            {
                cloud.Object = Object.Instantiate(spawn.ViewPrefab, PlaneMath.ToWorld(spawn.Position), Quaternion.identity, _visualRoot);
                if (cloud.Object.TryGetComponent(out cloud.View))
                    cloud.View.Begin(spawn.Radius);
            }
            _clouds.Add(cloud);
        }

        public void Simulate(float deltaTime)
        {
            if (_clouds.Count == 0)
                return;

            for (int i = _clouds.Count - 1; i >= 0; i--)
            {
                var cloud = _clouds[i];
                cloud.Age += deltaTime;
                float remaining = cloud.Spawn.Duration - cloud.Age;
                if (remaining <= 0f)
                {
                    if (cloud.Object != null)
                        Object.Destroy(cloud.Object);
                    _clouds.RemoveAt(i);
                    continue;
                }
                cloud.View?.SetLife(cloud.Age, remaining);
            }

            _burnTimer -= deltaTime;
            bool burn = _burnTimer <= 0f;
            if (burn)
                _burnTimer = BurnInterval;

            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive)
                    continue;
                Cloud worst = null;
                foreach (var cloud in _clouds)
                {
                    float reach = cloud.Spawn.Radius + player.Radius;
                    if ((player.Position - cloud.Spawn.Position).sqrMagnitude > reach * reach)
                        continue;
                    player.Slow(cloud.Spawn.Slow, 0.15f);
                    if (worst == null || cloud.Spawn.BurnDamage > worst.Spawn.BurnDamage)
                        worst = cloud;
                }
                if (burn && worst != null && worst.Spawn.BurnDamage > 0f)
                {
                    Vector2 away = player.Position - worst.Spawn.Position;
                    player.ApplyDamage(new DamageInfo(worst.Spawn.BurnDamage, Team.Enemy, worst.Spawn.SourceId, player.Position,
                        away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up, DamageKind.Contact));
                }
            }
        }

        public void Clear()
        {
            foreach (var cloud in _clouds)
            {
                if (cloud.Object != null)
                    Object.Destroy(cloud.Object);
            }
            _clouds.Clear();
        }
    }

    /// <summary>A cloud to leave on the floor: where, how big, for how long, and what it does to a ship inside.</summary>
    public readonly struct CloudSpawn
    {
        public Vector2 Position { get; }
        public float Radius { get; }
        public float Duration { get; }

        /// <summary>A ship inside moves at this fraction of its speed.</summary>
        public float Slow { get; }

        /// <summary>Damage each burn tick to a ship inside.</summary>
        public float BurnDamage { get; }

        public int SourceId { get; }
        public GameObject ViewPrefab { get; }

        public CloudSpawn(Vector2 position, float radius, float duration, float slow, float burnDamage, int sourceId, GameObject viewPrefab)
        {
            Position = position;
            Radius = radius;
            Duration = duration;
            Slow = slow;
            BurnDamage = burnDamage;
            SourceId = sourceId;
            ViewPrefab = viewPrefab;
        }
    }
}
