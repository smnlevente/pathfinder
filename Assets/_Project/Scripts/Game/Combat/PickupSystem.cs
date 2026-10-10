using System.Collections.Generic;
using Pathfinder.Core.Random;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// Scraps pickups (GDD §7.4), simulated on the fixed tick like projectiles. Drops burst outward, then fly
    /// to any living pilot whose magnet radius they enter, and despawn after a while if nobody collects them.
    /// Collection is decided by the authority.
    /// </summary>
    public sealed class PickupSystem
    {
        public const float Lifetime = 10f;
        const float BlinkTime = 2f;
        const float VisualHeight = 0.35f;
        const float MaxPiecesPerDrop = 12;
        const float CollectPadding = 0.35f;
        const float AttractAcceleration = 60f;
        const float MaxAttractSpeed = 24f;
        const float Drag = 5f;

        struct Pickup
        {
            public Vector2 Position;
            public Vector2 Previous;
            public Vector2 Velocity;
            public int Amount;
            public float Age;
            public bool Attracted;
            public Transform Visual;
        }

        readonly List<Pickup> _active = new List<Pickup>(128);
        readonly Stack<Transform> _pool = new Stack<Transform>();
        readonly Transform _visualRoot;
        readonly GameObject _scrapPrefab;

        public int Count => _active.Count;

        public int TotalAmount
        {
            get
            {
                int total = 0;
                foreach (var pickup in _active)
                    total += pickup.Amount;
                return total;
            }
        }

        public PickupSystem(Transform visualRoot, GameObject scrapPrefab)
        {
            _visualRoot = visualRoot;
            _scrapPrefab = scrapPrefab;
        }

        /// <summary>Splits the amount into several pieces that burst out from the drop point.</summary>
        public void SpawnScraps(Vector2 position, int amount, IRandom random)
        {
            if (amount <= 0)
                return;

            int pieces = (int)Mathf.Min(amount, MaxPiecesPerDrop);
            for (int i = 0; i < pieces; i++)
            {
                // Spread the remainder over the first pieces so the total is exact.
                int pieceAmount = amount / pieces + (i < amount % pieces ? 1 : 0);
                Vector2 direction = PlaneMath.FromHeading(random.Range(0f, 360f));

                var pickup = new Pickup
                {
                    Position = position,
                    Previous = position,
                    Velocity = direction * random.Range(2.5f, 6f),
                    Amount = pieceAmount,
                };

                if (_scrapPrefab != null)
                {
                    pickup.Visual = Rent();
                    pickup.Visual.position = PlaneMath.ToWorld(position, VisualHeight);
                }
                _active.Add(pickup);
            }
        }

        /// <summary>Hands every remaining pickup to a pilot, e.g. when a level ends.</summary>
        public void CollectAll(PlayerShip collector)
        {
            int total = TotalAmount;
            if (total > 0)
                collector.AddScraps(total);
            Clear();
        }

        public void Clear()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                RemoveAt(i, _active[i]);
        }

        internal void CapturePrevious()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var pickup = _active[i];
                pickup.Previous = pickup.Position;
                _active[i] = pickup;
            }
        }

        internal void Simulate(float deltaTime, CombatWorld world)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var pickup = _active[i];
                pickup.Age += deltaTime;

                var collector = FindCollector(pickup, out float distance);
                if (collector != null && distance <= collector.Radius + CollectPadding)
                {
                    collector.CollectScraps(pickup.Amount);
                    RemoveAt(i, pickup);
                    continue;
                }

                if (pickup.Age >= Lifetime)
                {
                    RemoveAt(i, pickup);
                    continue;
                }

                if (collector != null)
                {
                    // Accelerate toward the pilot, so pickups snap in satisfyingly.
                    pickup.Attracted = true;
                    Vector2 direction = (collector.Position - pickup.Position) / Mathf.Max(distance, 1e-4f);
                    float speed = Mathf.Min(MaxAttractSpeed, pickup.Velocity.magnitude + AttractAcceleration * deltaTime);
                    pickup.Velocity = direction * speed;
                }
                else
                {
                    pickup.Attracted = false;
                    pickup.Velocity *= Mathf.Max(0f, 1f - Drag * deltaTime);
                }

                pickup.Position = world.ClampToArena(pickup.Position + pickup.Velocity * deltaTime, 0.2f);
                _active[i] = pickup;
            }
        }

        internal void UpdateVisuals(float alpha)
        {
            foreach (var pickup in _active)
            {
                if (pickup.Visual == null)
                    continue;

                pickup.Visual.position = PlaneMath.ToWorld(Vector2.Lerp(pickup.Previous, pickup.Position, alpha), VisualHeight);

                // Blink before despawning so the player knows to hurry.
                bool expiring = !pickup.Attracted && pickup.Age > Lifetime - BlinkTime;
                bool visible = !expiring || Mathf.Repeat(pickup.Age * 8f, 1f) < 0.6f;
                if (pickup.Visual.gameObject.activeSelf != visible)
                    pickup.Visual.gameObject.SetActive(visible);
            }
        }

        static PlayerShip FindCollector(in Pickup pickup, out float distance)
        {
            PlayerShip best = null;
            distance = float.MaxValue;
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive)
                    continue;

                float d = Vector2.Distance(player.Position, pickup.Position);
                if (d <= player.MagnetRadius && d < distance)
                {
                    best = player;
                    distance = d;
                }
            }
            return best;
        }

        void RemoveAt(int index, in Pickup pickup)
        {
            if (pickup.Visual != null)
            {
                pickup.Visual.gameObject.SetActive(false);
                _pool.Push(pickup.Visual);
            }

            int last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }

        Transform Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            return Object.Instantiate(_scrapPrefab, _visualRoot).transform;
        }
    }
}
