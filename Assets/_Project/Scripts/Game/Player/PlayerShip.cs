using System;
using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// A pilot's ship: movement, dash, weapons, vitals, Scraps and equipment, all driven by <see cref="ShipCommand"/>s.
    /// Stats come from the ship definition plus a <see cref="StatSheet"/> of modifiers. Equipment effects hook into
    /// firing, hits, kills and dashes. Nothing here is local-player specific, so the same component works for
    /// remote co-op pilots.
    /// </summary>
    public sealed class PlayerShip : CombatEntity
    {
        [SerializeField] ShipDefinition ship;
        [SerializeField] PrimaryWeaponDefinition primaryWeapon;
        [SerializeField] SecondaryWeaponDefinition secondaryWeapon;
        [SerializeField, Tooltip("Distance from the ship's centre where projectiles spawn.")] float muzzleDistance = 0.9f;

        public override Team Team => Team.Player;
        public override Vitals Vitals => _vitals;
        public override bool IsIntangible => (Dash != null && Dash.IsDashing) || _intangibleRemaining > 0f;

        public ShipDefinition Definition => ship;
        public PrimaryWeaponDefinition PrimaryWeapon => primaryWeapon;
        public SecondaryWeaponDefinition SecondaryWeapon => secondaryWeapon;
        public IPrimaryWeapon Primary { get; private set; }
        public Cooldown SecondaryCooldown { get; private set; }
        public DashState Dash { get; private set; }

        /// <summary>Stat modifiers from equipment, traits and run effects.</summary>
        public StatSheet Stats { get; } = new StatSheet();

        /// <summary>Equipped items by category, with levels (GDD §8.1).</summary>
        public EquipmentSlots<EquipmentDefinition> Equipment { get; } = new EquipmentSlots<EquipmentDefinition>();

        /// <summary>Who pilots this ship. Null means no input, so the ship drifts to a stop.</summary>
        public IShipCommandSource CommandSource { get; set; }

        /// <summary>Controlled on this machine. Drives the camera and HUD.</summary>
        public bool IsLocal { get; set; }

        /// <summary>Debug invulnerability. Dev builds only, set by the debug hotkeys. Takes effect immediately.</summary>
        public bool GodMode
        {
            get => _godMode;
            set
            {
                _godMode = value;
                if (_vitals != null)
                    _vitals.ExternalInvulnerability = value || _tickInvulnerable;
            }
        }

        public CombatWorld World { get; private set; }
        public int PlayerIndex { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public Vector2 Velocity { get; private set; }
        public Vector2 MuzzlePosition => Position + AimDirection * muzzleDistance;

        public float MoveSpeed => Stats.Get(StatId.MoveSpeed, ship.MoveSpeed);
        public float MagnetRadius => Stats.Get(StatId.MagnetRadius, ship.PickupMagnetRadius);
        public float DashDistance => Stats.Get(StatId.DashDistance, ship.DashDistance);
        public float PrimaryDamageMultiplier => Stats.Get(StatId.ProjectileDamage, 1f);
        public float SecondaryDamageMultiplier => Stats.Get(StatId.SecondaryDamage, 1f);
        public float CritChance => Stats.Get(StatId.CritChance, 0f);

        /// <summary>Run currency carried by this pilot (GDD §11.1).</summary>
        public int Scraps { get; private set; }

        /// <summary>All Scraps gained this run, spent or not. 10% converts to Dark Matter when the run ends.</summary>
        public int TotalScrapsGained { get; private set; }

        /// <summary>The pilot pressed Interact during the latest tick. Interactables read it after the players simulate.</summary>
        public bool InteractPressed { get; private set; }

        /// <summary>Deterministic randomness for weapon spread and crits.</summary>
        public IRandom WeaponRandom { get; private set; } = new Pcg32(0UL, 0UL);

        /// <summary>Deterministic randomness for equipment and trait procs.</summary>
        public IRandom EffectRandom { get; private set; } = new Pcg32(0UL, 1UL);

        public event Action<PlayerShip> Dashed;
        public event Action<PlayerShip> Fired;
        public event Action<PlayerShip, SecondaryWeaponDefinition> SecondaryActivated;

        /// <summary>A secondary charge came back, from the cooldown or an effect such as Kill Refund. For the "ready" sound.</summary>
        public event Action<PlayerShip> SecondaryRecharged;

        /// <summary>An active reload was tried: true when it hit the window. For sound and the HUD.</summary>
        public event Action<PlayerShip, bool> ActiveReloaded;
        public event Action<PlayerShip, int> ScrapsCollected;

        /// <summary>Raised after the loadout changes, e.g. for the HUD.</summary>
        public event Action<PlayerShip> LoadoutChanged;

        readonly List<EquipmentEffect> _effects = new List<EquipmentEffect>();
        readonly List<StatModifier> _runModifiers = new List<StatModifier>();
        Vitals _vitals;
        Vector2 _dashDirection;
        bool _dashMovementOverridden;
        bool _wasDashing;
        float _intangibleRemaining;
        Vector2? _pullTarget;
        int _secondaryCharges;
        float _slowRemaining, _slowFactor = 1f;
        bool _secondaryHeld;
        float _pullSpeed;
        bool _godMode;
        bool _tickInvulnerable; // dash or intangibility, as of the last tick
        float _scrapsRemainder;
        int _shotIndex;

        protected override void Awake()
        {
            base.Awake();
            if (ship != null && primaryWeapon != null)
                Initialize(ship, primaryWeapon, secondaryWeapon);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CombatEvents.Damaged += OnAnyDamaged;
            CombatEvents.Killed += OnAnyKilled;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CombatEvents.Damaged -= OnAnyDamaged;
            CombatEvents.Killed -= OnAnyKilled;
        }

        /// <summary>Sets the ship and loadout and resets all state, including equipment.</summary>
        public void Initialize(ShipDefinition shipDefinition, PrimaryWeaponDefinition primary, SecondaryWeaponDefinition secondary)
        {
            ship = shipDefinition;
            primaryWeapon = primary;
            secondaryWeapon = secondary;

            _vitals = new Vitals(ship.MaxHull, ship.MaxShield)
            {
                ShieldRegenRate = ship.ShieldRegenRate,
                ShieldRegenDelay = ship.ShieldRegenDelay,
                HitInvulnerability = ship.HitInvulnerability,
                DamageTakenMultiplier = ship.DamageTakenMultiplier,
                ExternalInvulnerability = _godMode,
            };
            _tickInvulnerable = false;
            _pullTarget = null;
            Dash = new DashState(ship.DashCharges, ship.DashDuration, ship.DashRechargeTime);
            Velocity = Vector2.zero;
            Scraps = 0;
            TotalScrapsGained = 0;
            _runModifiers.Clear();
            Equipment.Clear();

            Rebuild();
        }

        /// <summary>Joins the ship to a world. The player index selects its deterministic random streams.</summary>
        public void Bind(CombatWorld world, int playerIndex)
        {
            World = world;
            PlayerIndex = playerIndex;
            var seed = world.Seed.Derive(SeedLabels.Player).Derive(playerIndex);
            WeaponRandom = seed.CreateRandom(SeedLabels.Weapon);
            EffectRandom = seed.CreateRandom("effects");
            Rebuild();
        }

        // ---------------------------------------------------------------- loadout

        public void EquipPrimary(PrimaryWeaponDefinition definition)
        {
            primaryWeapon = definition;
            Rebuild();
        }

        public void EquipSecondary(SecondaryWeaponDefinition definition)
        {
            secondaryWeapon = definition;
            SecondaryCooldown = null;
            Rebuild();
        }

        /// <summary>What equipping the item would do: equip, level up, already maxed, or needs a swap.</summary>
        public EquipOutcome PreviewEquip(EquipmentDefinition item) => Equipment.Preview(item, item.Category);

        /// <summary>Equips the item or levels up a duplicate. With <see cref="EquipOutcome.NeedsSwap"/> nothing changes. Authority only.</summary>
        public EquipOutcome Equip(EquipmentDefinition item)
        {
            var outcome = Equipment.TryAdd(item, item.Category);
            if (outcome == EquipOutcome.Equipped || outcome == EquipOutcome.LeveledUp)
                Rebuild();
            return outcome;
        }

        /// <summary>Swaps an equipped item for a new one at level I. The old item is lost (GDD §8.1).</summary>
        public void ReplaceEquipment(int slotIndex, EquipmentDefinition item)
        {
            Equipment.Replace(item.Category, slotIndex, item);
            Rebuild();
        }

        /// <summary>Raises an equipped item's level, e.g. a Shop upgrade (GDD §10.6).</summary>
        public bool UpgradeEquipment(EquipmentDefinition item)
        {
            if (!Equipment.LevelUp(item))
                return false;
            Rebuild();
            return true;
        }

        /// <summary>Removes an equipped item, e.g. traded away in a Mystery event. Authority only.</summary>
        public bool RemoveEquipment(EquipmentDefinition item)
        {
            if (!Authority.IsAuthority || !Equipment.Remove(item))
                return false;
            Rebuild();
            return true;
        }

        /// <summary>A permanent stat change for the rest of the run, e.g. Reinforce at a repair station.</summary>
        public void AddRunModifier(StatId stat, float flat, float percent)
        {
            _runModifiers.Add(new StatModifier(stat, flat, percent, _runModifiers));
            Rebuild();
        }

        /// <summary>
        /// Rebuilds stats and effects from scratch: ship trait, every equipped item at its level, and run modifiers. This is
        /// simple and can never leave stale state behind. It runs only when the loadout changes.
        /// </summary>
        void Rebuild()
        {
            if (ship == null || _vitals == null)
                return;

            Stats.Clear();
            foreach (var modifier in _runModifiers)
                Stats.Add(modifier);

            _effects.Clear();
            if (ship.Trait != null)
                AddEffect(ship.Trait.CreateEffect(), 1);
            foreach (var entry in Equipment.All)
                AddEffect(entry.Item.CreateEffect(), entry.Level);
            foreach (var effect in _effects)
                effect.ApplyStats(Stats);

            _vitals.SetLimits(Stats.Get(StatId.MaxHull, ship.MaxHull), Stats.Get(StatId.MaxShield, ship.MaxShield));
            _vitals.ShieldRegenDelay = Stats.Get(StatId.ShieldRegenDelay, ship.ShieldRegenDelay);
            _vitals.DamageTakenMultiplier = Stats.Get(StatId.DamageTaken, ship.DamageTakenMultiplier);

            int dashCharges = Mathf.Max(1, Stats.GetInt(StatId.DashCharges, ship.DashCharges));
            if (Dash == null || Dash.MaxCharges != dashCharges)
                Dash = new DashState(dashCharges, ship.DashDuration, ship.DashRechargeTime);

            if (primaryWeapon != null)
            {
                Primary = PrimaryWeapons.Create(primaryWeapon, WeaponRandom, Stats);
                _shotIndex = 0;
            }

            if (secondaryWeapon != null)
            {
                int charges = Mathf.Max(1, Stats.GetInt(StatId.SecondaryCharges, secondaryWeapon.Charges));
                float cooldown = Mathf.Max(0.5f, Stats.Get(StatId.SecondaryCooldown, secondaryWeapon.Cooldown));
                if (SecondaryCooldown == null || SecondaryCooldown.MaxCharges != charges || !Mathf.Approximately(SecondaryCooldown.RechargeTime, cooldown))
                {
                    SecondaryCooldown = new Cooldown(charges, cooldown);
                    _secondaryCharges = charges; // a fresh cooldown starts full: that isn't a recharge
                }
            }
            else
            {
                SecondaryCooldown = null;
            }

            LoadoutChanged?.Invoke(this);
        }

        void AddEffect(EquipmentEffect effect, int level)
        {
            effect.Bind(this, level);
            _effects.Add(effect);
        }

        // ---------------------------------------------------------------- economy and run hooks

        /// <summary>Adds Scraps directly, e.g. rewards. Authority only.</summary>
        public void AddScraps(int amount)
        {
            if (!Authority.IsAuthority || amount <= 0)
                return;

            Scraps += amount;
            TotalScrapsGained += amount;
            ScrapsCollected?.Invoke(this, amount);
        }

        /// <summary>Scraps the pilot starts a run with (meta upgrades). They aren't collected, so they don't count when banking.</summary>
        public void AddStartingScraps(int amount)
        {
            if (!Authority.IsAuthority || amount <= 0)
                return;

            Scraps += amount;
        }

        /// <summary>Scraps picked up from drops, boosted by Scraps-gain bonuses such as Salvage Magnet.</summary>
        public void CollectScraps(int amount)
        {
            float boosted = amount * Stats.Get(StatId.ScrapsGain, 1f) + _scrapsRemainder;
            int whole = Mathf.FloorToInt(boosted);
            _scrapsRemainder = boosted - whole;
            AddScraps(whole);
        }

        /// <summary>Takes up to <paramref name="amount"/> Scraps, as many as the pilot has. Returns how many were lost. Authority only.</summary>
        public int LoseScraps(int amount)
        {
            if (!Authority.IsAuthority || amount <= 0)
                return 0;
            int lost = Mathf.Min(amount, Scraps);
            Scraps -= lost;
            return lost;
        }

        /// <summary>Spends Scraps if the pilot has enough. Authority only.</summary>
        public bool TrySpendScraps(int amount)
        {
            if (!Authority.IsAuthority || amount < 0 || amount > Scraps)
                return false;

            Scraps -= amount;
            return true;
        }

        /// <summary>Restores hull and returns the amount healed. Authority only.</summary>
        public float Heal(float amount) => Authority.IsAuthority ? _vitals.Heal(amount) : 0f;

        /// <summary>Takes hull past the shield, never below 1 (a Mystery event's cost). Returns the hull lost. Authority only.</summary>
        public float DrainHull(float amount) => Authority.IsAuthority ? _vitals.DrainHull(amount) : 0f;

        /// <summary>Slows the ship to <paramref name="factor"/> of its speed for a while, e.g. inside a spore cloud. Dashes aren't slowed.</summary>
        public void Slow(float factor, float seconds)
        {
            _slowFactor = _slowRemaining > 0f ? Mathf.Min(_slowFactor, factor) : factor;
            _slowRemaining = Mathf.Max(_slowRemaining, seconds);
        }

        public bool IsSlowed => _slowRemaining > 0f;

        /// <summary>Raises max hull for the rest of the run (the Reinforce repair option, GDD §10.4). Authority only.</summary>
        public void Reinforce(float amount)
        {
            if (Authority.IsAuthority)
                AddRunModifier(StatId.MaxHull, amount, 0f);
        }

        /// <summary>Being drawn into the exit wormhole: no control, no damage. See <see cref="PullTo"/>.</summary>
        public bool IsPulled => _pullTarget.HasValue;

        /// <summary>Counts placements at a level entry, so the camera can cut to the ship instead of sliding (playtest 5).</summary>
        public int Placements { get; private set; }

        /// <summary>
        /// Draws the ship into a point, spinning faster as it goes, with no control and no damage: the wormhole taking it
        /// after a boss fight (playtest 4). Ends when the level ends (<see cref="StopPull"/>) or the ship is placed for the next.
        /// </summary>
        public void PullTo(Vector2 point)
        {
            _pullTarget = point;
            _pullSpeed = 3f;
        }

        /// <summary>Ends a wormhole pull, e.g. when the level it was leaving is torn down.</summary>
        public void StopPull() => _pullTarget = null;

        void SimulatePull(float deltaTime, CombatWorld world)
        {
            const float Acceleration = 30f, MaxSpeed = 28f, SpinDegrees = 720f;
            _vitals.ExternalInvulnerability = true;
            Vector2 toTarget = _pullTarget.Value - Position;
            float distance = toTarget.magnitude;
            _pullSpeed = Mathf.Min(MaxSpeed, _pullSpeed + Acceleration * deltaTime);
            float step = Mathf.Min(distance, _pullSpeed * deltaTime);
            Velocity = distance > 1e-4f ? toTarget / distance * _pullSpeed : Vector2.zero;
            Position = world.ClampToArena(Position + (distance > 1e-4f ? toTarget / distance * step : Vector2.zero), Radius);
            Heading += SpinDegrees * deltaTime * Mathf.Clamp01(_pullSpeed / MaxSpeed + 0.2f);
        }

        /// <summary>Places the ship at a level entry point, at rest, and tells its effects a new level began.</summary>
        public void PlaceForLevel(Vector2 position)
        {
            _pullTarget = null;
            _slowRemaining = 0f;
            Placements++;
            Teleport(position);
            Velocity = Vector2.zero;
            AimDirection = Vector2.up;
            Heading = 0f;
            SecondaryCooldown?.Refill(); // every level starts with the secondary ready (M7)
            RefreshPerLevelEffects();
        }

        /// <summary>Re-arms once-per-level equipment such as Last Stand: at each level, and after each Survival guardian.</summary>
        public void RefreshPerLevelEffects()
        {
            foreach (var effect in _effects)
                effect.OnLevelStart();
        }

        /// <summary>Instantly moves the ship during a dash. Used by Blink Dash.</summary>
        public void BlinkTo(Vector2 position)
        {
            Teleport(position);
            Velocity = Vector2.zero;
        }

        /// <summary>Bullets and contact pass through the ship for a while, e.g. Last Stand's grace.</summary>
        public void GrantIntangibility(float seconds) => _intangibleRemaining = Mathf.Max(_intangibleRemaining, seconds);

        /// <summary>Called by the projectile system when an enemy shot passes through the intangible ship.</summary>
        public void NotifyPhasedThrough()
        {
            foreach (var effect in _effects)
                effect.OnPhasedThrough();
        }

        /// <summary>Lets equipment modify a trigger pull before its projectiles spawn.</summary>
        public ShotParams PrepareShot()
        {
            var shot = new ShotParams { ShotIndex = ++_shotIndex, DamageMultiplier = 1f };
            foreach (var effect in _effects)
                effect.OnShotFired(ref shot);
            return shot;
        }

        internal void NotifyFired() => Fired?.Invoke(this);

        internal void NotifyReloadPressed(MagazineWeapon magazine)
        {
            foreach (var effect in _effects)
                effect.OnReloadPressedWhileReloading(magazine);
        }

        internal void NotifyActiveReload(bool perfect) => ActiveReloaded?.Invoke(this, perfect);

        /// <summary>The window of an active reload in progress, as (start, end) fractions of the reload; null when there is none.</summary>
        public Vector2? ReloadWindow
        {
            get
            {
                foreach (var effect in _effects)
                {
                    var window = effect.ReloadWindow;
                    if (window.HasValue)
                        return window;
                }
                return null;
            }
        }

        protected override bool TryPreventDeath(in DamageInfo info)
        {
            foreach (var effect in _effects)
            {
                if (effect.TryPreventLethal())
                    return true;
            }
            return false;
        }

        void OnAnyDamaged(CombatEntity target, DamageInfo info, DamageResult result)
        {
            // On-hit effects fire only for this ship's primary projectiles. Procs and burns can't chain.
            if (!Authority.IsAuthority || info.SourceId != EntityId || target.Team == Team || info.Kind != DamageKind.Projectile || result.Blocked)
                return;
            foreach (var effect in _effects)
                effect.OnHit(target, info, result);
        }

        void OnAnyKilled(CombatEntity target, DamageInfo info)
        {
            if (!Authority.IsAuthority || info.SourceId != EntityId || !(target is Enemy enemy))
                return;
            foreach (var effect in _effects)
                effect.OnKill(enemy, info);
        }

        // ---------------------------------------------------------------- simulation

        public override void Simulate(float deltaTime, CombatWorld world)
        {
            if (_pullTarget.HasValue)
            {
                SimulatePull(deltaTime, world);
                return;
            }

            var command = CommandSource?.ReadCommand(this) ?? default;
            Vector2 move = Vector2.ClampMagnitude(command.Move, 1f);
            InteractPressed = command.Interact;

            if (command.Aim.sqrMagnitude > 1e-4f)
                AimDirection = command.Aim.normalized;

            if (command.Dash && Dash.TryStart())
            {
                _dashDirection = move.sqrMagnitude > 0.01f ? move.normalized : AimDirection;
                _dashMovementOverridden = false;
                foreach (var effect in _effects)
                    _dashMovementOverridden |= effect.OverrideDashMovement(_dashDirection);
                foreach (var effect in _effects)
                    effect.OnDashStart(_dashDirection);
                Dashed?.Invoke(this);
            }

            // Movement uses the dash state from before this tick's Dash.Tick, so a dash covers its full distance.
            bool dashing = Dash.IsDashing;
            _tickInvulnerable = dashing || _intangibleRemaining > 0f;
            _vitals.ExternalInvulnerability = _tickInvulnerable || _godMode;

            float moveSpeed = MoveSpeed * (_slowRemaining > 0f ? _slowFactor : 1f);
            if (dashing)
            {
                Velocity = _dashMovementOverridden ? Vector2.zero : _dashDirection * (DashDistance / ship.DashDuration);
            }
            else
            {
                // Exiting a dash snaps back to normal speed instead of sliding.
                Velocity = Vector2.ClampMagnitude(Velocity, moveSpeed);
                float acceleration = moveSpeed / ship.AccelerationTime;
                Velocity = Vector2.MoveTowards(Velocity, move * moveSpeed, acceleration * deltaTime);
            }

            Vector2 previous = Position;
            Vector2 unclamped = previous + Velocity * deltaTime;
            Position = world.ClampToArena(unclamped, Radius);
            if (Position != unclamped)
                Velocity = (Position - previous) / deltaTime; // stop pushing into the wall

            Heading = PlaneMath.HeadingDegrees(AimDirection);

            Dash.Tick(deltaTime);
            _intangibleRemaining = Mathf.Max(0f, _intangibleRemaining - deltaTime);
            _slowRemaining = Mathf.Max(0f, _slowRemaining - deltaTime);
            if (_wasDashing && !Dash.IsDashing)
                OnDashEnded();
            _wasDashing = Dash.IsDashing;
            _vitals.Tick(deltaTime);

            Primary.Tick(deltaTime, command, this, world);

            // One press, one salvo: holding the button doesn't spend a second charge the next tick (playtest 5: with
            // Twin Charge, a click fired both charges at once).
            bool secondaryPressed = command.FireSecondary && !_secondaryHeld;
            _secondaryHeld = command.FireSecondary;
            if (SecondaryCooldown != null)
            {
                SecondaryCooldown.Tick(deltaTime);
                if (SecondaryCooldown.Charges > _secondaryCharges)
                    SecondaryRecharged?.Invoke(this); // includes charges Kill Refund returned since the last tick
                if (secondaryPressed && SecondaryCooldown.TryUse())
                {
                    secondaryWeapon.Activate(this, world, WeaponRandom);
                    SecondaryActivated?.Invoke(this, secondaryWeapon);
                }
                _secondaryCharges = SecondaryCooldown.Charges;
            }
        }

        void OnDashEnded()
        {
            float extra = 0f;
            foreach (var effect in _effects)
                extra = Mathf.Max(extra, effect.ExtraDashIntangibility);
            GrantIntangibility(extra);

            foreach (var effect in _effects)
                effect.OnDashEnd();
        }
    }
}
