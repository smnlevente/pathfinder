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
    /// A pilot's ship: movement, dash, weapons, vitals and Scraps, all driven by <see cref="ShipCommand"/>s.
    /// Nothing here is local-player specific, so the same component works for remote co-op pilots.
    /// </summary>
    public sealed class PlayerShip : CombatEntity
    {
        [SerializeField] ShipDefinition ship;
        [SerializeField] PrimaryWeaponDefinition primaryWeapon;
        [SerializeField] SecondaryWeaponDefinition secondaryWeapon;
        [SerializeField, Tooltip("Distance from the ship's centre where projectiles spawn.")] float muzzleDistance = 0.9f;

        public override Team Team => Team.Player;
        public override Vitals Vitals => _vitals;
        public override bool IsIntangible => Dash != null && Dash.IsDashing;

        public ShipDefinition Definition => ship;
        public PrimaryWeaponDefinition PrimaryWeapon => primaryWeapon;
        public SecondaryWeaponDefinition SecondaryWeapon => secondaryWeapon;
        public IPrimaryWeapon Primary { get; private set; }
        public Cooldown SecondaryCooldown { get; private set; }
        public DashState Dash { get; private set; }

        /// <summary>Who pilots this ship. Null means no input, so the ship drifts to a stop.</summary>
        public IShipCommandSource CommandSource { get; set; }

        /// <summary>Controlled on this machine. Drives the camera and HUD.</summary>
        public bool IsLocal { get; set; }

        /// <summary>Debug invulnerability. Dev builds only, set by the debug hotkeys.</summary>
        public bool GodMode { get; set; }

        public int PlayerIndex { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public Vector2 Velocity { get; private set; }
        public Vector2 MuzzlePosition => Position + AimDirection * muzzleDistance;
        public float MagnetRadius => ship.PickupMagnetRadius;

        /// <summary>Run currency carried by this pilot (GDD §11.1).</summary>
        public int Scraps { get; private set; }

        public event Action<PlayerShip> Dashed;
        public event Action<PlayerShip> Fired;
        public event Action<PlayerShip, SecondaryWeaponDefinition> SecondaryActivated;
        public event Action<PlayerShip, int> ScrapsCollected;

        Vitals _vitals;
        IRandom _weaponRandom = new Pcg32(0UL, 0UL);
        Vector2 _dashDirection;

        protected override void Awake()
        {
            base.Awake();
            if (ship != null && primaryWeapon != null)
                Initialize(ship, primaryWeapon, secondaryWeapon);
        }

        /// <summary>Sets the ship and loadout and resets all state.</summary>
        public void Initialize(ShipDefinition shipDefinition, PrimaryWeaponDefinition primary, SecondaryWeaponDefinition secondary)
        {
            ship = shipDefinition;
            _vitals = new Vitals(ship.MaxHull, ship.MaxShield)
            {
                ShieldRegenRate = ship.ShieldRegenRate,
                ShieldRegenDelay = ship.ShieldRegenDelay,
                HitInvulnerability = ship.HitInvulnerability,
                DamageTakenMultiplier = ship.DamageTakenMultiplier,
            };
            Dash = new DashState(ship.DashCharges, ship.DashDuration, ship.DashRechargeTime);
            Velocity = Vector2.zero;
            Scraps = 0;

            EquipPrimary(primary);
            EquipSecondary(secondary);
        }

        /// <summary>Joins the ship to a world. The player index selects its deterministic random streams.</summary>
        public void Bind(CombatWorld world, int playerIndex)
        {
            PlayerIndex = playerIndex;
            _weaponRandom = world.Seed.Derive(SeedLabels.Player).Derive(playerIndex).CreateRandom(SeedLabels.Weapon);
            Primary = PrimaryWeapons.Create(primaryWeapon, _weaponRandom);
        }

        public void EquipPrimary(PrimaryWeaponDefinition definition)
        {
            primaryWeapon = definition;
            Primary = PrimaryWeapons.Create(definition, _weaponRandom);
        }

        public void EquipSecondary(SecondaryWeaponDefinition definition)
        {
            secondaryWeapon = definition;
            SecondaryCooldown = definition != null ? new Cooldown(definition.Charges, definition.Cooldown) : null;
        }

        /// <summary>Adds Scraps. Authority only.</summary>
        public void AddScraps(int amount)
        {
            if (!Authority.IsAuthority || amount <= 0)
                return;

            Scraps += amount;
            ScrapsCollected?.Invoke(this, amount);
        }

        internal void NotifyFired() => Fired?.Invoke(this);

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
            _vitals.ExternalInvulnerability = dashing || GodMode;

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

            Primary.Tick(deltaTime, command, this, world);

            if (SecondaryCooldown != null)
            {
                SecondaryCooldown.Tick(deltaTime);
                if (command.FireSecondary && SecondaryCooldown.TryUse())
                {
                    secondaryWeapon.Activate(this, world, _weaponRandom);
                    SecondaryActivated?.Invoke(this, secondaryWeapon);
                }
            }
        }
    }
}
