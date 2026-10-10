using Pathfinder.Core.Combat;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>What the HUD needs to show about a primary weapon, whatever its fire mode.</summary>
    public readonly struct PrimaryWeaponStatus
    {
        /// <summary>Short state text, e.g. "24 / 30", "RELOADING 40%" or "OVERHEATED".</summary>
        public string Label { get; }

        /// <summary>Gauge from 0 to 1: ammo, reload, or charge progress.</summary>
        public float Fill { get; }

        /// <summary>The gauge should read as a warning, e.g. reloading or overheating.</summary>
        public bool Warning { get; }

        /// <summary>Charge level for visuals, from 0 to 1. Always 0 for non-charging weapons.</summary>
        public float Charge { get; }

        public PrimaryWeaponStatus(string label, float fill, bool warning, float charge = 0f)
        {
            Label = label;
            Fill = fill;
            Warning = warning;
            Charge = charge;
        }
    }

    /// <summary>Runtime state of an equipped primary weapon. Ticked once per simulation tick by its ship.</summary>
    public interface IPrimaryWeapon
    {
        PrimaryWeaponDefinition Definition { get; }
        PrimaryWeaponStatus Status { get; }
        void Tick(float deltaTime, in ShipCommand command, PlayerShip ship, CombatWorld world);
    }

    public static class PrimaryWeapons
    {
        /// <summary>Builds the runtime weapon. Fire rate and magazine size include stat modifiers, e.g. Overclock.</summary>
        public static IPrimaryWeapon Create(PrimaryWeaponDefinition definition, IRandom random, StatSheet stats) => definition.FireMode switch
        {
            PrimaryFireMode.Charge => new ChargePrimary(definition, random, stats),
            _ => new AutomaticPrimary(definition, random, stats),
        };

        /// <summary>
        /// Spawns one projectile with the ship's damage and pierce bonuses and a crit roll. The crit is rolled at fire time,
        /// so hit order can never change the outcome.
        /// </summary>
        internal static void Spawn(PlayerShip ship, CombatWorld world, IRandom random, PrimaryWeaponDefinition weapon, in ShotParams shot,
            Vector2 direction, float baseDamage, float radius, float visualScale = 1f)
        {
            float damage = baseDamage * ship.PrimaryDamageMultiplier * shot.DamageMultiplier;
            if (ship.CritChance > 0f && random.Chance(ship.CritChance))
                damage *= 2f;

            int pierce = weapon.Pierce + ship.Stats.GetInt(StatId.Pierce, 0) + shot.ExtraPierce;
            world.Projectiles.Spawn(new ProjectileSpawn(
                ship.MuzzlePosition, direction, weapon.ProjectileSpeed, damage, radius, weapon.Range, ship.Team, ship.EntityId,
                weapon.ProjectilePrefab, weapon.ImpactPrefab, pierce: pierce, damageAtMaxRange: weapon.DamageAtMaxRange,
                visualScale: visualScale));
        }

        /// <summary>Spawns the extra fan projectiles some equipment adds around a shot, e.g. Split Shot.</summary>
        internal static void SpawnExtras(PlayerShip ship, CombatWorld world, IRandom random, PrimaryWeaponDefinition weapon, in ShotParams shot,
            Vector2 direction, float baseDamage, float radius, float visualScale = 1f)
        {
            for (int i = 0; i < shot.ExtraProjectiles; i++)
            {
                int side = i % 2 == 0 ? 1 : -1;
                float angle = side * shot.ExtraSpreadDegrees * (i / 2 + 1);
                Spawn(ship, world, random, weapon, shot, PlaneMath.Rotate(direction, angle), baseDamage, radius, visualScale);
            }
        }
    }

    /// <summary>Magazine weapons: Photon Repeater, Plasma Cutter, Neutron Bomb Launcher, Bot Dispatcher.</summary>
    public sealed class AutomaticPrimary : IPrimaryWeapon
    {
        readonly MagazineWeapon _magazine;
        readonly IRandom _random;

        public PrimaryWeaponDefinition Definition { get; }
        public MagazineWeapon Magazine => _magazine;

        public AutomaticPrimary(PrimaryWeaponDefinition definition, IRandom random, StatSheet stats)
        {
            Definition = definition;
            _random = random;
            _magazine = new MagazineWeapon(
                Mathf.Max(1, stats.GetInt(StatId.MagazineSize, definition.MagazineSize)),
                Mathf.Max(0.1f, stats.Get(StatId.FireRate, definition.ShotsPerSecond)),
                definition.ReloadTime);
        }

        public PrimaryWeaponStatus Status => _magazine.IsReloading
            ? new PrimaryWeaponStatus(Localization.Loc.T("weapon.reloading", ("percent", _magazine.ReloadProgress * 100f)), _magazine.ReloadProgress, warning: true)
            : new PrimaryWeaponStatus(Localization.Loc.T("weapon.ammo", ("ammo", _magazine.Ammo), ("size", _magazine.MagazineSize)), _magazine.Ammo / (float)_magazine.MagazineSize, warning: false);

        public void Tick(float deltaTime, in ShipCommand command, PlayerShip ship, CombatWorld world)
        {
            int shots = _magazine.Tick(deltaTime, command.FirePrimary, command.Reload);
            for (int i = 0; i < shots; i++)
                FireShot(ship, world);
        }

        void FireShot(PlayerShip ship, CombatWorld world)
        {
            var weapon = Definition;
            var shot = ship.PrepareShot();
            int count = weapon.ProjectilesPerShot;

            for (int i = 0; i < count; i++)
            {
                float angle;
                if (count == 1)
                {
                    angle = weapon.SpreadDegrees > 0f ? _random.Range(-0.5f, 0.5f) * weapon.SpreadDegrees : 0f;
                }
                else
                {
                    // Even fan with a little jitter, so a shotgun blast is consistent but not mechanical.
                    float slot = weapon.SpreadDegrees / count;
                    angle = -weapon.SpreadDegrees * 0.5f + slot * (i + 0.5f) + _random.Range(-0.3f, 0.3f) * slot;
                }

                Vector2 direction = PlaneMath.Rotate(ship.AimDirection, angle);
                PrimaryWeapons.Spawn(ship, world, _random, weapon, shot, direction, weapon.Damage, weapon.ProjectileRadius);
            }

            if (shot.ExtraProjectiles > 0)
                PrimaryWeapons.SpawnExtras(ship, world, _random, weapon, shot, ship.AimDirection, weapon.Damage, weapon.ProjectileRadius);

            ship.NotifyFired();
        }
    }

    /// <summary>Hold-to-charge weapons such as the Quantum Charger. Damage, size and visual scale grow with charge.</summary>
    public sealed class ChargePrimary : IPrimaryWeapon
    {
        readonly ChargeWeapon _charge;
        readonly IRandom _random;

        public PrimaryWeaponDefinition Definition { get; }
        public ChargeWeapon Charge => _charge;

        public ChargePrimary(PrimaryWeaponDefinition definition, IRandom random, StatSheet stats)
        {
            Definition = definition;
            _random = random;

            // Fire-rate bonuses shorten the charge.
            float speed = Mathf.Max(0.1f, stats.Get(StatId.FireRate, 1f));
            _charge = new ChargeWeapon(definition.MinChargeTime / speed, definition.FullChargeTime / speed, definition.OverheatTime, definition.LockoutTime);
        }

        public PrimaryWeaponStatus Status
        {
            get
            {
                if (_charge.IsLockedOut)
                    return new PrimaryWeaponStatus(Localization.Loc.T("weapon.overheated"), 1f, warning: true);
                if (_charge.HeatFraction > 0f)
                    return new PrimaryWeaponStatus(Localization.Loc.T("weapon.release", ("seconds", (1f - _charge.HeatFraction) * Definition.OverheatTime)), 1f - _charge.HeatFraction, warning: true, charge: 1f);
                if (_charge.IsCharging)
                    return new PrimaryWeaponStatus(Localization.Loc.T("weapon.charge", ("percent", _charge.ChargeFraction * 100f)), _charge.ChargeFraction, warning: false, charge: _charge.ChargeFraction);
                return new PrimaryWeaponStatus(Localization.Loc.T("weapon.hold"), 0f, warning: false);
            }
        }

        public void Tick(float deltaTime, in ShipCommand command, PlayerShip ship, CombatWorld world)
        {
            if (!_charge.Tick(deltaTime, command.FirePrimary, out float power))
                return;

            var weapon = Definition;
            var shot = ship.PrepareShot();
            float damage = Mathf.Lerp(weapon.Damage, weapon.MaxChargeDamage, power);
            float radius = Mathf.Lerp(weapon.ProjectileRadius, weapon.MaxChargeRadius, power);
            float scale = Mathf.Lerp(1f, weapon.MaxChargeVisualScale, power);

            PrimaryWeapons.Spawn(ship, world, _random, weapon, shot, ship.AimDirection, damage, radius, scale);
            if (shot.ExtraProjectiles > 0)
                PrimaryWeapons.SpawnExtras(ship, world, _random, weapon, shot, ship.AimDirection, damage, radius, scale);

            ship.NotifyFired();
        }
    }
}
