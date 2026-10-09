using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
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
        public static IPrimaryWeapon Create(PrimaryWeaponDefinition definition, IRandom random) => definition.FireMode switch
        {
            PrimaryFireMode.Charge => new ChargePrimary(definition),
            _ => new AutomaticPrimary(definition, random),
        };
    }

    /// <summary>Magazine weapons: Photon Repeater, Plasma Cutter, Neutron Bomb Launcher, Bot Dispatcher.</summary>
    public sealed class AutomaticPrimary : IPrimaryWeapon
    {
        readonly MagazineWeapon _magazine;
        readonly IRandom _random;

        public PrimaryWeaponDefinition Definition { get; }
        public MagazineWeapon Magazine => _magazine;

        public AutomaticPrimary(PrimaryWeaponDefinition definition, IRandom random)
        {
            Definition = definition;
            _random = random;
            _magazine = new MagazineWeapon(definition.MagazineSize, definition.ShotsPerSecond, definition.ReloadTime);
        }

        public PrimaryWeaponStatus Status => _magazine.IsReloading
            ? new PrimaryWeaponStatus($"RELOADING {_magazine.ReloadProgress * 100f:0}%", _magazine.ReloadProgress, warning: true)
            : new PrimaryWeaponStatus($"{_magazine.Ammo} / {_magazine.MagazineSize}", _magazine.Ammo / (float)_magazine.MagazineSize, warning: false);

        public void Tick(float deltaTime, in ShipCommand command, PlayerShip ship, CombatWorld world)
        {
            int shots = _magazine.Tick(deltaTime, command.FirePrimary, command.Reload);
            for (int i = 0; i < shots; i++)
                FireShot(ship, world);
        }

        void FireShot(PlayerShip ship, CombatWorld world)
        {
            var weapon = Definition;
            int count = weapon.ProjectilesPerShot;
            Vector2 muzzle = ship.MuzzlePosition;

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

                world.Projectiles.Spawn(new ProjectileSpawn(
                    muzzle, PlaneMath.Rotate(ship.AimDirection, angle), weapon.ProjectileSpeed, weapon.Damage, weapon.ProjectileRadius,
                    weapon.Range, ship.Team, ship.EntityId, weapon.ProjectilePrefab, weapon.ImpactPrefab,
                    pierce: weapon.Pierce, damageAtMaxRange: weapon.DamageAtMaxRange));
            }

            ship.NotifyFired();
        }
    }

    /// <summary>Hold-to-charge weapons such as the Quantum Charger. Damage, size and visual scale grow with charge.</summary>
    public sealed class ChargePrimary : IPrimaryWeapon
    {
        readonly ChargeWeapon _charge;

        public PrimaryWeaponDefinition Definition { get; }
        public ChargeWeapon Charge => _charge;

        public ChargePrimary(PrimaryWeaponDefinition definition)
        {
            Definition = definition;
            _charge = new ChargeWeapon(definition.MinChargeTime, definition.FullChargeTime, definition.OverheatTime, definition.LockoutTime);
        }

        public PrimaryWeaponStatus Status
        {
            get
            {
                if (_charge.IsLockedOut)
                    return new PrimaryWeaponStatus("OVERHEATED", 1f, warning: true);
                if (_charge.HeatFraction > 0f)
                    return new PrimaryWeaponStatus($"RELEASE! {(1f - _charge.HeatFraction) * Definition.OverheatTime:0.0}s", 1f - _charge.HeatFraction, warning: true, charge: 1f);
                if (_charge.IsCharging)
                    return new PrimaryWeaponStatus($"CHARGE {_charge.ChargeFraction * 100f:0}%", _charge.ChargeFraction, warning: false, charge: _charge.ChargeFraction);
                return new PrimaryWeaponStatus("HOLD TO CHARGE", 0f, warning: false);
            }
        }

        public void Tick(float deltaTime, in ShipCommand command, PlayerShip ship, CombatWorld world)
        {
            if (!_charge.Tick(deltaTime, command.FirePrimary, out float power))
                return;

            var weapon = Definition;
            world.Projectiles.Spawn(new ProjectileSpawn(
                ship.MuzzlePosition, ship.AimDirection, weapon.ProjectileSpeed,
                Mathf.Lerp(weapon.Damage, weapon.MaxChargeDamage, power),
                Mathf.Lerp(weapon.ProjectileRadius, weapon.MaxChargeRadius, power),
                weapon.Range, ship.Team, ship.EntityId, weapon.ProjectilePrefab, weapon.ImpactPrefab,
                pierce: weapon.Pierce, damageAtMaxRange: weapon.DamageAtMaxRange,
                visualScale: Mathf.Lerp(1f, weapon.MaxChargeVisualScale, power)));

            ship.NotifyFired();
        }
    }
}
