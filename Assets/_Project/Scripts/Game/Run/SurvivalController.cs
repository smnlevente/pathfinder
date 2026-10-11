using System;
using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Meta;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// Survival (GDD §3.2): one arena, endless waves, and a clock. Every minute on the clock brings an equipment choice
    /// (skipping repairs the hull), and every five a galaxy's guardian: the Quarry Maw, the Binary Wardens, then the Hive
    /// Matriarch, whose fall wins. The clock holds while a guardian is alive, and the waves thin out. Wave budget, enemy
    /// health and elite odds follow the clock, so pressure grows with time rather than with how fast waves die. Each fallen
    /// guardian moves the waves on to the next galaxy's enemies and music. Dying or leaving banks what was earned, like a
    /// run, plus the best time. Authority only, on the fixed tick.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public sealed class SurvivalController : MonoBehaviour
    {
        public enum Phase { Fighting, Warning, Guardian, Won, Lost }

        const float RewardDelay = 1.5f;
        const float BannerTime = 3f;
        const float HintTime = 8f;
        static readonly Vector2 GuardianSpawn = new Vector2(0f, 8f);

        [SerializeField] CombatWorld world;
        [SerializeField] WaveDirector director;
        [SerializeField] SurvivalConfig config;
        [SerializeField, Tooltip("The guardians, their health by galaxy, cores and the boss heal.")] RunConfig runConfig;
        [SerializeField] EquipmentChoiceScreen equipmentChoice;
        [SerializeField] ContentDatabase content;

        IRandom _lootRandom;
        float _nextUpgrade;
        int _minutesPaid;
        float _warningLeft, _rewardDelay, _vacuumLeft, _bannerTimer, _guardianTime;
        float _startTime;
        int _previousBest;
        string _lastHitBy;

        /// <summary>Carries the loadout and the Dark Matter and Cores earned, like a run's.</summary>
        public RunState Run { get; private set; }
        public Phase State { get; private set; }
        public SurvivalConfig Config => config;

        /// <summary>Seconds survived, on the clock. It holds while a guardian is coming or alive.</summary>
        public float Clock { get; private set; }

        public int GuardiansDefeated { get; private set; }
        public static int GuardianCount => RunState.GalaxyCount;

        /// <summary>The galaxy the waves come from: one step on per fallen guardian.</summary>
        public int Tier => Mathf.Min(GuardiansDefeated, GuardianCount - 1);

        /// <summary>The guardian in the arena, while one is alive.</summary>
        public Enemy Guardian { get; private set; }

        /// <summary>The next guardian to come, or null once all have fallen.</summary>
        public EnemyDefinition NextGuardian => GuardiansDefeated < GuardianCount ? runConfig.BossFor(GuardiansDefeated) : null;

        public float NextGuardianTime => config.GuardianInterval * (GuardiansDefeated + 1);
        public float NextUpgradeTime => _nextUpgrade;
        public int UpgradesOffered { get; private set; }

        public bool IsWon => State == Phase.Won;
        public bool IsOver => State is Phase.Won or Phase.Lost;

        /// <summary>What this attempt banked into the meta progression. Set when it ends.</summary>
        public RunRewards? BankedRewards { get; private set; }

        /// <summary>The clock beat the best time on record when this attempt ended.</summary>
        public bool IsNewBest { get; private set; }

        /// <summary>Status lines for the HUD.</summary>
        public string Header { get; private set; } = string.Empty;
        public string Objective { get; private set; } = string.Empty;
        public string Prompt { get; private set; } = string.Empty;
        public string Banner { get; private set; } = string.Empty;

        void Start()
        {
            // The Hub starts a session with the loadout and seeds the world with it. Playing the scene directly starts one here.
            Run = RunSession.Current ?? RunSession.Begin(world.Seed);
            var seed = Run.Seed.Derive("survival");
            _lootRandom = seed.CreateRandom(SeedLabels.Loot);
            _startTime = Time.realtimeSinceStartup;
            _previousBest = MetaProgression.Current.BestSurvivalSeconds;
            _nextUpgrade = config.UpgradeInterval;

            world.BeginLevel(seed, config.EnemyHealthAt(0f), 1f);
            director.Begin(seed, 0, 1, 0, firstWaveDelay: 2f, keepToGalaxy: true);
            ApplyPressure();

            world.AllPlayersDown += OnAllPlayersDown;
            CombatEvents.Damaged += OnAnyDamaged;
            ShowBanner(Loc.T("banner.survival"));
            GameAudio.PlayMusic(SoundLibrary.ForGalaxy(0));
            UpdateTexts();
        }

        void OnDestroy()
        {
            CombatEvents.Damaged -= OnAnyDamaged;
            if (world != null)
                world.AllPlayersDown -= OnAllPlayersDown;
            if (Guardian != null)
                Guardian.Died -= OnGuardianDied;
        }

        void Update()
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.unscaledDeltaTime;
                if (_bannerTimer <= 0f)
                    Banner = string.Empty;
            }
        }

        void FixedUpdate()
        {
            if (IsOver || GameClock.Paused || !Authority.IsAuthority)
                return;

            float deltaTime = Time.fixedDeltaTime;
            if (_vacuumLeft > 0f)
            {
                _vacuumLeft -= deltaTime;
                if (_vacuumLeft <= 0f)
                    world.Pickups.Vacuum = false;
            }

            // The reward waits a moment so the guardian's death plays out first; the clock waits for the reward.
            if (_rewardDelay > 0f)
            {
                _rewardDelay -= deltaTime;
                if (_rewardDelay <= 0f)
                    OfferUpgrade(Loc.T("choice.boss_reward"), Rarity.Rare);
            }
            else if (State == Phase.Fighting)
            {
                AdvanceClock(deltaTime);
            }
            else if (State == Phase.Warning)
            {
                _warningLeft -= deltaTime;
                if (_warningLeft <= 0f)
                    SpawnGuardian();
            }
            else if (State == Phase.Guardian)
            {
                _guardianTime += deltaTime;
            }

            UpdateTexts();
        }

        void AdvanceClock(float deltaTime)
        {
            float guardianAt = NextGuardianTime;
            Clock = Mathf.Min(Clock + deltaTime, guardianAt);
            PayMinutes();
            ApplyPressure();

            if (Clock >= guardianAt)
                BeginWarning();
            else if (Clock >= _nextUpgrade)
            {
                _nextUpgrade += config.UpgradeInterval;
                OfferUpgrade(Loc.T("choice.survival_upgrade"), Rarity.Common);
            }
        }

        /// <summary>Dark Matter for every whole minute on the clock, more in later tiers.</summary>
        void PayMinutes()
        {
            int minutes = Mathf.FloorToInt(Clock / 60f);
            while (_minutesPaid < minutes)
            {
                _minutesPaid++;
                Run.AddDarkMatter(RunConfig.ForGalaxy(config.DarkMatterPerMinute, Tier));
            }
        }

        /// <summary>Sets the waves and enemy health from the clock, lighter while a guardian is in the arena.</summary>
        void ApplyPressure()
        {
            bool guardian = State is Phase.Warning or Phase.Guardian;
            world.SetEnemyHealthMultiplier(config.EnemyHealthAt(Clock));
            director.FixedBudget = config.Budget(Clock);
            director.FixedEliteChance = config.EliteChance(Clock);
            director.BudgetMultiplier = guardian ? config.GuardianBudgetMultiplier : 1f;
            director.WaveTimeout = guardian ? config.GuardianWaveInterval : config.WaveInterval;
            director.MaxEnemies = config.MaxEnemies;
        }

        // ---------------------------------------------------------------- upgrades

        /// <summary>
        /// A choice of random equipment, with rarity odds from the current tier. Skipping repairs part of the hull instead
        /// of paying Scraps: there is no Shop to spend them in.
        /// </summary>
        void OfferUpgrade(string title, Rarity minimum)
        {
            var pilot = FirstLivingPilot();
            if (pilot == null)
                return;

            UpgradesOffered++;
            float repair = pilot.Vitals.MaxHull * config.SkipRepairFraction;
            var offer = EquipmentOffers.Roll(content, pilot, config.UpgradeChoices, Tier, minimum, _lootRandom);
            if (offer.Count == 0)
            {
                pilot.Heal(repair);
                return;
            }
            equipmentChoice.Show(title, offer, pilot, Loc.T("choice.skip_repair", ("amount", Mathf.RoundToInt(repair))), () => pilot.Heal(repair), null);
        }

        // ---------------------------------------------------------------- guardians

        void BeginWarning()
        {
            State = Phase.Warning;
            _warningLeft = config.GuardianWarning;
            // The upgrade due at the same time is replaced by the guardian's reward.
            while (_nextUpgrade <= Clock + 0.01f)
                _nextUpgrade += config.UpgradeInterval;
            ApplyPressure();
            ShowBanner(Loc.T("banner.guardian_coming", ("name", NextGuardian.DisplayName.ToUpperInvariant())));
            GameAudio.Play(SoundCue.Roar);
            GameAudio.PlayMusic(MusicTrack.Boss);
            if (_warningLeft <= 0f)
                SpawnGuardian();
        }

        void SpawnGuardian()
        {
            int tier = GuardiansDefeated;
            var setup = runConfig.Boss;
            // Each guardian has its galaxy's health, as in a run, whatever the waves around it have reached.
            float health = RunConfig.ForGalaxy(runConfig.GalaxyEnemyHealth, tier) * setup.HealthMultiplier / world.EnemyHealthMultiplier;
            Guardian = world.SpawnEnemy(runConfig.BossFor(tier), GuardianSpawnPoint(), setup.Elite, dropsLoot: true, extraHealthMultiplier: health);
            Guardian.Died += OnGuardianDied;
            BossIntro.Play(Guardian);
            State = Phase.Guardian;
            _guardianTime = 0f;
        }

        /// <summary>The top of the arena, or the bottom if the pilot is up there: a guardian never lands on the ship.</summary>
        static Vector2 GuardianSpawnPoint()
        {
            var nearest = CombatRegistry.NearestLivingPlayer(GuardianSpawn);
            return nearest != null && (nearest.Position - GuardianSpawn).sqrMagnitude < 64f ? -GuardianSpawn : GuardianSpawn;
        }

        void OnGuardianDied(CombatEntity entity, DamageInfo info)
        {
            Guardian.Died -= OnGuardianDied;
            Guardian = null;
            int tier = GuardiansDefeated++;

            // As in a run: the bullets in the air turn harmless, the Scraps come home, and the pilot is patched up.
            world.Projectiles.Defuse(Team.Enemy);
            int cores = RunConfig.ForGalaxy(runConfig.SingularityCores, tier);
            Run.AddSingularityCores(cores);
            GameAudio.Play(SoundCue.BossDefeated);
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive)
                    continue;
                player.Heal(player.Vitals.MaxHull * runConfig.BossHealFraction);
                player.RefreshPerLevelEffects();
            }

            if (GuardiansDefeated >= GuardianCount)
            {
                Win();
                return;
            }

            ShowBanner(Loc.T(cores == 1 ? "banner.boss_defeated.one" : "banner.boss_defeated.many", ("cores", cores)));
            world.Pickups.Vacuum = true;
            _vacuumLeft = config.VacuumSeconds;
            State = Phase.Fighting;
            director.SetGalaxy(Tier);
            ApplyPressure();
            GameAudio.PlayMusic(SoundLibrary.ForGalaxy(Tier));
            _rewardDelay = RewardDelay;
        }

        // ---------------------------------------------------------------- ending

        /// <summary>The last guardian fell: the waves die with it, and the attempt is won.</summary>
        void Win()
        {
            State = Phase.Won;
            director.Stop();
            foreach (var enemy in new List<Enemy>(CombatRegistry.Enemies))
            {
                if (enemy != null && enemy.IsAlive)
                    enemy.ApplyDamage(new DamageInfo(enemy.Vitals.Hull + enemy.Vitals.Shield + 1f, Team.Player, 0, enemy.Position, Vector2.zero, DamageKind.Proc));
            }
            var collector = FirstLivingPilot();
            if (collector != null)
                world.Pickups.CollectAll(collector);
            Bank(won: true);
            GameAudio.PlayMusic(MusicTrack.None);
            UpdateTexts();
        }

        void OnAllPlayersDown()
        {
            if (IsOver)
                return;
            State = Phase.Lost;
            Bank(won: false);
            GameAudio.Play(SoundCue.RunOver);
            GameAudio.PlayMusic(MusicTrack.None);
            UpdateTexts();
        }

        /// <summary>Leaves from the pause menu. What was earned so far is banked.</summary>
        public void Abandon()
        {
            if (IsOver)
                return;
            State = Phase.Lost;
            Bank(won: false);
        }

        /// <summary>Banks Dark Matter, Cores and 10% of Scraps, and the time survived, then logs the attempt (GDD §11.1, §17).</summary>
        void Bank(bool won)
        {
            if (BankedRewards.HasValue)
                return;

            int scraps = 0;
            foreach (var player in CombatRegistry.Players)
                scraps += player.TotalScrapsGained;

            int seconds = Mathf.FloorToInt(Clock);
            IsNewBest = seconds > _previousBest;
            BankedRewards = MetaProgression.Current.BankSurvival(Run.DarkMatter, Run.SingularityCores, scraps, seconds);
            MetaProgression.Save();

            var pilot = CombatRegistry.Players.Count > 0 ? CombatRegistry.Players[0] : null;
            RunLog.Append(new RunLog.Entry
            {
                timeUtc = DateTime.UtcNow.ToString("u"),
                mode = "survival",
                seed = RunLog.SeedCode(Run.Seed),
                ship = pilot != null ? pilot.Definition.Id : Run.Loadout.ShipId,
                primary = pilot != null ? pilot.PrimaryWeapon.Id : Run.Loadout.PrimaryId,
                secondary = pilot != null && pilot.SecondaryWeapon != null ? pilot.SecondaryWeapon.Id : Run.Loadout.SecondaryId,
                won = won,
                galaxyReached = Tier + 1,
                levelsCleared = GuardiansDefeated,
                darkMatter = Run.DarkMatter,
                singularityCores = Run.SingularityCores,
                scrapsCollected = scraps,
                minutes = (Time.realtimeSinceStartup - _startTime) / 60f,
                survivedSeconds = Clock,
                killedBy = won ? string.Empty : _lastHitBy ?? "abandoned",
            });
        }

        /// <summary>Remembers what last hurt a pilot, for the log's "killed by".</summary>
        void OnAnyDamaged(CombatEntity target, DamageInfo info, DamageResult result)
        {
            if (target is not PlayerShip || result.TotalDamage <= 0f || info.SourceTeam != Team.Enemy)
                return;
            _lastHitBy = "unknown";
            foreach (var entity in CombatRegistry.OnTeam(Team.Enemy))
            {
                if (entity.EntityId == info.SourceId && entity is Enemy enemy)
                    _lastHitBy = enemy.Definition.DisplayName;
            }
        }

        // ---------------------------------------------------------------- debug

        /// <summary>
        /// Moves the clock forward, no further than the next guardian, skipping the upgrades on the way. Dark Matter for the
        /// minutes passed is paid. For tests and debug tools.
        /// </summary>
        public void DebugAdvance(float seconds)
        {
            if (State != Phase.Fighting)
                return;
            Clock = Mathf.Min(Clock + Mathf.Max(0f, seconds), NextGuardianTime);
            while (_nextUpgrade <= Clock && _nextUpgrade < NextGuardianTime)
                _nextUpgrade += config.UpgradeInterval;
            PayMinutes();
            ApplyPressure();
        }

        // ---------------------------------------------------------------- texts

        public static string FormatClock(float seconds)
        {
            int whole = Mathf.FloorToInt(Mathf.Max(0f, seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }

        void ShowBanner(string text)
        {
            Banner = text;
            _bannerTimer = BannerTime;
        }

        void UpdateTexts()
        {
            Header = Loc.T("hud.survival", ("time", FormatClock(Clock)), ("galaxy", Loc.T($"galaxy.{Tier}")));
            Prompt = string.Empty;
            var next = NextGuardian;
            switch (State)
            {
                case Phase.Fighting:
                    Objective = Loc.T("objective.survival", ("upgrade", FormatClock(_nextUpgrade - Clock)),
                        ("guardian", next != null ? next.DisplayName : string.Empty), ("time", FormatClock(NextGuardianTime - Clock)));
                    break;
                case Phase.Warning:
                    Objective = Loc.T("objective.survival_warning", ("guardian", next.DisplayName));
                    break;
                case Phase.Guardian:
                    Objective = Loc.T("objective.survival_guardian", ("guardian", next.DisplayName));
                    if (_guardianTime < HintTime)
                        Prompt = Loc.T($"{next.Id}.hint"); // how to hurt this guardian
                    break;
                default:
                    Objective = string.Empty;
                    break;
            }
        }

        static PlayerShip FirstLivingPilot()
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive)
                    return player;
            }
            return null;
        }
    }
}
