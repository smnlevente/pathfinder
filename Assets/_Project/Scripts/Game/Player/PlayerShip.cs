using System;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// A pilot's ship: movement, dash, primary weapon and vitals, all driven by <see cref="ShipCommand"/>s.
    /// Nothing here is local-player specific, so the same component works for remote co-op pilots.
    /// </summary>
    public sealed class PlayerShip : CombatEntity
    {
        [SerializeField] ShipDefinition ship;
        [SerializeField] PrimaryWeaponDefinition primaryWeapon;
        [SerializeField, Tooltip("Distance from the ship's centre where projectiles spawn.")] float muzzleDistance = 0.9f;

        public override Team Team => Team.Player;
        public override Vitals Vitals => _vitals;
        public override bool IsIntangible => Dash != null && Dash.IsDashing;

        public ShipDefinition Definition => ship;
        public PrimaryWeaponDefinition PrimaryWeapon => primaryWeapon;
        public DashState Dash { get; private set; }
        public MagazineWeapon Weapon { get; private set; }

        /// <summary>Who pilots this ship. Null means no input, so the ship drifts to a stop.</summary>
        public IShipCommandSource CommandSource { get; set; }

        /// <summary>Controlled on this machine. Drives the camera and HUD.</summary>
        public bool IsLocal { get; set; }

        public int PlayerIndex { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public Vector2 Velocity { get; private set; }

        public event Action<PlayerShip> Dashed;
        public event Action<PlayerShip> Fired;

        Vitals _vitals;
        IRandom _weaponRandom;
        Vector2 _dashDirection;

        protected override void Awake()
        {
            base.Awake();
            if (ship != null && primaryWeapon != null)
                Initialize(ship, primaryWeapon);
        }

        /// <summary>Sets the loadout and resets all state.</summary>
        public void Initialize(ShipDefinition shipDefinition, PrimaryWeaponDefinition weapon)
        {
            ship = shipDefinition;
            primaryWeapon = weapon;

            _vitals = new Vitals(ship.MaxHull, ship.MaxShield)
            {
                ShieldRegenRate = ship.ShieldRegenRate,
                ShieldRegenDelay = ship.ShieldRegenDelay,
                HitInvulnerability = ship.HitInvulnerability,
                DamageTakenMultiplier = ship.DamageTakenMultiplier,
            };
            Dash = new DashState(ship.DashCharges, ship.DashDuration, ship.DashRechargeTime);
            Weapon = new MagazineWeapon(weapon.MagazineSize, weapon.ShotsPerSecond, weapon.ReloadTime);
            Velocity = Vector2.zero;
        }

        /// <summary>Joins the ship to a world. The player index selects its deterministic random streams.</summary>
        public void Bind(CombatWorld world, int playerIndex)
        {
            PlayerIndex = playerIndex;
            _weaponRandom = world.Seed.Derive(SeedLabels.Player).Derive(playerIndex).CreateRandom(SeedLabels.Weapon);
        }

        public override void Simulate(float deltaTime, CombatWorld world)
        {
            var command = CommandSource?.ReadCommand(this) ?? default;
            Vector2 move = Vector2.ClampMagnitude(command.Move, 1f);

            if (command.Aim.sqrMagnitude > 1e-4f)
                AimDirection = command.Aim.normalized;

            if (command.Dash && Dash.TryStart())
            {
                _dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : AimDirection;
                Dashed?.Invoke(this);
            }

            // Movement uses the dash state from before this tick's Dash.Tick, so a dash covers its full distance.
            bool dashing = Dash.IsDashing;
            _vitals.ExternalInvulnerability = dashing;

            if (dashing)
            {
                Velocity = _dashDirection * (ship.DashDistance / ship.DashDuration);
            }
            else
            {
                // Exiting a dash snaps back to normal speed instead of sliding.
                Velocity = Vector2.ClampMagnitude(Velocity, ship.MoveSpeed);
                float acceleration = ship.MoveSpeed / ship.AccelerationTime;
                Velocity = Vector2.MoveTowards(Velocity, move * ship.MoveSpeed, acceleration * deltaTime);
            }

            Vector2 previous = Position;
            Vector2 unclamped = previous + Velocity * deltaTime;
            Position = world.ClampToArena(unclamped, Radius);
            if (Position != unclamped)
                Velocity = (Position - previous) / deltaTime; // stop pushing into the wall

            Heading = PlaneMath.HeadingDegrees(AimDirection);

            Dash.Tick(deltaTime);
            _vitals.Tick(deltaTime);

            int shots = Weapon.Tick(deltaTime, command.FirePrimary, command.Reload);
            for (int i = 0; i < shots; i++)
                Fire(world);
        }

        void Fire(CombatWorld world)
        {
            var weapon = primaryWeapon;
            Vector2 muzzle = Position + AimDirection * muzzleDistance;

            for (int i = 0; i < weapon.ProjectilesPerShot; i++)
            {
                float spread = weapon.SpreadDegrees > 0f && _weaponRandom != null
                    ? _weaponRandom.Range(-0.5f, 0.5f) * weapon.SpreadDegrees
                    : 0f;

                world.Projectiles.Spawn(new ProjectileSpawn(
                    muzzle, PlaneMath.Rotate(AimDirection, spread), weapon.ProjectileSpeed, weapon.Damage,
                    weapon.ProjectileRadius, weapon.Range, Team, EntityId, weapon.ProjectilePrefab, weapon.ImpactPrefab));
            }

            Fired?.Invoke(this);
        }
    }
}
