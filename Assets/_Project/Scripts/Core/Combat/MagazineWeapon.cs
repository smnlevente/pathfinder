using System;

namespace Pathfinder.Core.Combat
{
    /// <summary>
    /// Fire-rate, magazine and reload cycle for primary weapons (GDD §6.1). The weapon reloads
    /// automatically when the magazine runs dry, or on request when it isn't full.
    /// </summary>
    public sealed class MagazineWeapon
    {
        public int MagazineSize { get; }
        public float FireInterval { get; }
        public float ReloadTime { get; }

        public int Ammo { get; private set; }
        public bool IsReloading => _reloadRemaining > 0f;

        /// <summary>0 at the start of a reload, 1 when the reload finishes. 1 when not reloading.</summary>
        public float ReloadProgress => IsReloading ? 1f - _reloadRemaining / ReloadTime : 1f;

        float _cooldown;
        float _reloadRemaining;

        public MagazineWeapon(int magazineSize, float shotsPerSecond, float reloadTime)
        {
            if (magazineSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(magazineSize), "Magazine size must be positive.");
            if (shotsPerSecond <= 0f)
                throw new ArgumentOutOfRangeException(nameof(shotsPerSecond), "Fire rate must be positive.");
            if (reloadTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(reloadTime), "Reload time must be positive.");

            MagazineSize = Ammo = magazineSize;
            FireInterval = 1f / shotsPerSecond;
            ReloadTime = reloadTime;
        }

        /// <summary>Advances the weapon by one tick and returns how many shots to fire this tick.</summary>
        public int Tick(float deltaTime, bool triggerHeld, bool reloadRequested)
        {
            if (reloadRequested && !IsReloading && Ammo < MagazineSize)
                StartReload();

            if (IsReloading)
            {
                _reloadRemaining -= deltaTime;
                if (_reloadRemaining > TimeTolerance.Epsilon)
                    return 0;

                _reloadRemaining = 0f;
                Ammo = MagazineSize;
            }

            int shots = 0;
            if (triggerHeld)
            {
                // Several shots per tick are possible when the fire rate exceeds the tick rate.
                while (_cooldown <= TimeTolerance.Epsilon && Ammo > 0)
                {
                    shots++;
                    Ammo--;
                    _cooldown += FireInterval;
                }

                if (Ammo == 0)
                {
                    StartReload();
                    return shots;
                }
            }

            // Shots happen at the start of the tick, and the rest of the tick counts down to the next one.
            // Idle time isn't banked: releasing the trigger must not allow a burst later.
            _cooldown = triggerHeld ? _cooldown - deltaTime : Math.Max(0f, _cooldown - deltaTime);
            return shots;
        }

        void StartReload()
        {
            _reloadRemaining = ReloadTime;
            _cooldown = 0f;
        }
    }
}
