using System;
using System.Collections.Generic;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>What a level is and where it sits in the run.</summary>
    public readonly struct LevelContext
    {
        public MapNodeType Type { get; }
        public Seed Seed { get; }
        public int GalaxyIndex { get; }

        /// <summary>0-based map row.</summary>
        public int Row { get; }

        public bool FromMystery { get; }
        public bool Ambush { get; }

        /// <summary>A Mystery event: a narrative choice instead of a regular level (GDD §10.5).</summary>
        public bool IsEvent => Type == MapNodeType.Mystery;

        public LevelContext(MapNodeType type, Seed seed, int galaxyIndex, int row, bool fromMystery, bool ambush)
        {
            Type = type;
            Seed = seed;
            GalaxyIndex = galaxyIndex;
            Row = row;
            FromMystery = fromMystery;
            Ambush = ambush;
        }
    }

    /// <summary>
    /// Runs one level in the arena (GDD §10). It sets up the level type's objective, pays its rewards, and opens the exit
    /// wormhole once the objective is met. Flying into the exit ends the level. Objective logic runs on the
    /// authority, on the fixed tick, after the players have simulated.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class LevelController : MonoBehaviour
    {
        const float StationRadius = 2.2f;
        const float CachePickupRadius = 1.4f;
        const float BannerTime = 3f;
        const float BossRewardDelay = 1.5f;

        [SerializeField] CombatWorld world;
        [SerializeField] WaveDirector director;
        [SerializeField] RunConfig config;
        [SerializeField] ChoiceDialog dialog;
        [SerializeField] EquipmentChoiceScreen equipmentChoice;
        [SerializeField] ContentDatabase content;

        public sealed class CatalogItem
        {
            public EquipmentDefinition Item;
            public bool Sold;
        }

        sealed class HarvestNode
        {
            public Vector2 Position;
            public float Progress;
            public bool Harvested;
            public GameObject View;
            public ObjectiveMarker Marker;
        }

        readonly List<HarvestNode> _nodes = new List<HarvestNode>();
        readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        readonly List<CatalogItem> _catalog = new List<CatalogItem>();
        RunState _run;
        IRandom _random;
        IRandom _lootRandom;
        IRandom _shopRandom;
        int _rerolls;
        GameObject _exit;
        Vector2 _stationPosition;
        ObjectiveMarker _stationMarker;
        bool _stationUsed;
        Enemy _cacheCarrier;
        Vector2? _cachePosition;
        GameObject _cacheView;
        bool _cacheCollected;
        bool _escalated;
        bool _exited;
        float _bannerTimer;
        float _bossRewardDelay;
        float _levelTime;
        IRandom _eventRandom;
        bool _eventFinished;

        public bool IsActive { get; private set; }
        public LevelContext Context { get; private set; }
        public bool ExitOpen { get; private set; }
        public float RewardMultiplier { get; private set; } = 1f;
        public RunConfig Config => config;

        /// <summary>The level boss, while one is alive.</summary>
        public Enemy Boss { get; private set; }

        /// <summary>The Mystery event being played, if this level is one.</summary>
        public MysteryEventDefinition Event { get; private set; }

        /// <summary>What the last event choice did. For tests and the HUD.</summary>
        public EventResult LastEventResult { get; private set; }

        GameIcons Icons => content != null ? content.Icons : null;

        public int DarkMatterNodes => _nodes.Count;
        public int DarkMatterHarvested { get; private set; }
        public float InstabilityRemaining { get; private set; }
        public bool CacheCollected => _cacheCollected;
        public IReadOnlyList<CatalogItem> Catalog => _catalog;
        public int RerollPrice => config.RerollPrice + config.RerollPriceIncrease * _rerolls;
        public Vector2? CachePosition => _cacheCollected ? null : _cachePosition;
        public Vector2 StationPosition => _stationPosition;

        public IEnumerable<Vector2> UnharvestedNodes
        {
            get
            {
                foreach (var node in _nodes)
                {
                    if (!node.Harvested)
                        yield return node.Position;
                }
            }
        }

        /// <summary>Short status line for the HUD.</summary>
        public string Objective { get; private set; } = string.Empty;

        /// <summary>Contextual hint, e.g. "Press E to use the repair station".</summary>
        public string Prompt { get; private set; } = string.Empty;

        /// <summary>Temporary announcement, e.g. "+30 SCRAPS".</summary>
        public string Banner { get; private set; } = string.Empty;

        /// <summary>Raised when a pilot flies into the open exit.</summary>
        public event Action<LevelController> Exited;

        public void Begin(RunState run, LevelContext context)
        {
            End(null);
            _run = run;
            Context = context;
            IsActive = true;
            _exited = false;
            _bossRewardDelay = 0f;
            _levelTime = 0f;
            _random = context.Seed.CreateRandom(SeedLabels.Layout);
            _lootRandom = context.Seed.CreateRandom(SeedLabels.Loot);
            _shopRandom = context.Seed.CreateRandom("shop");
            _rerolls = 0;

            RewardMultiplier = context.Ambush ? config.AmbushRewardMultiplier : context.FromMystery ? config.MysteryRewardMultiplier : 1f;
            float scrapsDrop = context.IsEvent ? config.AmbushRewardMultiplier // drops only matter if the event turns into a fight
                : RewardMultiplier * (context.Type == MapNodeType.Scraps ? config.ScrapsDropMultiplier : 1f);
            world.BeginLevel(context.Seed, RunConfig.ForGalaxy(config.GalaxyEnemyHealth, context.GalaxyIndex), scrapsDrop);

            foreach (var player in CombatRegistry.Players)
            {
                player.PlaceForLevel(config.PlayerEntry);
                // The ship arrives through a wormhole: a flash where it lands, and the camera cuts to it (playtest 5).
                if (config.ArrivalEffect != null)
                    Instantiate(config.ArrivalEffect, PlaneMath.ToWorld(config.PlayerEntry, 0.5f), Quaternion.identity);
            }
            Audio.GameAudio.Play(Audio.SoundCue.WarpIn);

            float budget = context.Ambush ? config.AmbushBudgetMultiplier : 1f;
            int directorRow = context.Row + 1;
            switch (context.Type)
            {
                case MapNodeType.Scraps:
                    director.Begin(context.Seed, context.GalaxyIndex, directorRow, config.ScrapsWaves, config.ScrapsBudgetMultiplier * budget);
                    director.Completed += OnScrapsWavesCleared;
                    break;

                case MapNodeType.Equipment:
                    director.Begin(context.Seed, context.GalaxyIndex, directorRow, config.EquipmentWaves, budget);
                    director.WaveStarted += OnEquipmentWaveStarted;
                    director.Completed += OnEquipmentWavesCleared;
                    break;

                case MapNodeType.DarkMatter:
                    director.Begin(context.Seed, context.GalaxyIndex, directorRow, 0, config.PressureBudgetMultiplier, firstWaveDelay: 3f);
                    director.WaveTimeout = config.PressureWaveInterval;
                    SpawnHarvestNodes();
                    InstabilityRemaining = config.InstabilityTime;
                    _escalated = false;
                    OpenExit();
                    break;

                case MapNodeType.Repair:
                    SpawnStation(config.RepairStationPrefab);
                    OpenExit();
                    break;

                case MapNodeType.Shop:
                    SpawnStation(config.ShopTerminalPrefab);
                    OpenExit();
                    break;

                case MapNodeType.Boss:
                    SpawnBoss();
                    break;

                case MapNodeType.Mystery:
                    BeginEvent();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(context), $"Unsupported level type {context.Type}.");
            }

            ShowBanner(context.Ambush ? Loc.T("banner.ambush") : context.IsEvent ? Loc.T("banner.mystery")
                : context.FromMystery ? Loc.T("banner.mystery_level", ("type", LevelNames.Title(context.Type))) : LevelNames.Title(context.Type));
            UpdateTexts();
        }

        /// <summary>Tears the level down. Leftover Scraps go to the collector.</summary>
        public void End(PlayerShip collector)
        {
            director.Completed -= OnScrapsWavesCleared;
            director.WaveStarted -= OnEquipmentWaveStarted;
            director.Completed -= OnEquipmentWavesCleared;
            director.Completed -= OnEventAmbushCleared;
            director.Stop();

            if (_cacheCarrier != null)
                _cacheCarrier.Died -= OnCacheCarrierDied;
            if (Boss != null)
                Boss.Died -= OnBossDied;

            world.ClearLevel(collector);
            foreach (var player in CombatRegistry.Players)
                player.StopPull(); // the wormhole took them: the pull (and its camera rumble) ends with the level
            foreach (var spawned in _spawnedObjects)
            {
                if (spawned != null)
                    Destroy(spawned);
            }
            _spawnedObjects.Clear();
            _nodes.Clear();
            _catalog.Clear();

            _exit = null;
            _stationMarker = null;
            _stationUsed = false;
            _cacheCarrier = null;
            _cachePosition = null;
            _cacheView = null;
            _cacheCollected = false;
            Boss = null;
            Event = null;
            _eventFinished = false;
            DarkMatterHarvested = 0;
            ExitOpen = false;
            IsActive = false;
            Prompt = Objective = string.Empty;
        }

        /// <summary>Completes the objective at once and opens the exit. For tests and debug tools.</summary>
        public void DebugCompleteObjective()
        {
            if (!IsActive)
                return;

            var enemies = new List<Enemy>(CombatRegistry.Enemies);
            foreach (var enemy in enemies)
                enemy.Despawn();
            director.Stop();
            OpenExit();
        }

        /// <summary>Flies every pilot into the exit. For tests and debug tools.</summary>
        public void DebugTakeExit()
        {
            if (dialog.IsOpen)
                dialog.Close(); // an event waiting for a choice
            if (!ExitOpen)
                DebugCompleteObjective();
            foreach (var player in CombatRegistry.Players)
                player.PlaceForLevel(config.ExitPosition);
        }

        void FixedUpdate()
        {
            if (!IsActive || GameClock.Paused || !Authority.IsAuthority)
                return;

            // An event that is over leaves for the map once its menus have closed.
            if (_eventFinished && !dialog.IsOpen && !equipmentChoice.IsOpen)
            {
                _eventFinished = false;
                _exited = true;
                Exited?.Invoke(this);
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            _levelTime += deltaTime;
            switch (Context.Type)
            {
                case MapNodeType.Equipment: TickCache(); break;
                case MapNodeType.DarkMatter: TickHarvest(deltaTime); break;
                case MapNodeType.Repair:
                case MapNodeType.Shop: TickStation(); break;
            }

            // The boss reward waits a moment so its death plays out first; the exit waits for the reward.
            if (_bossRewardDelay > 0f)
            {
                _bossRewardDelay -= deltaTime;
                if (_bossRewardDelay <= 0f)
                    OfferBossReward();
            }

            if (ExitOpen && !_exited && _bossRewardDelay <= 0f && AnyPilotWithin(config.ExitPosition, config.ExitRadius))
            {
                _exited = true;
                Exited?.Invoke(this);
            }

            UpdateTexts();
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

        // ---------------------------------------------------------------- Scraps level (GDD §10.1)

        void OnScrapsWavesCleared(WaveDirector source)
        {
            int bonus = Mathf.RoundToInt(RunConfig.ForGalaxy(config.ScrapsCompletionBonus, Context.GalaxyIndex) * RewardMultiplier);
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive)
                    player.AddScraps(bonus);
            }
            ShowBanner(Loc.T("banner.level_clear", ("bonus", bonus)));
            world.Pickups.Vacuum = true;
            OpenExit();
        }

        // ---------------------------------------------------------------- Equipment level (GDD §10.2)

        void OnEquipmentWaveStarted(WaveDirector source)
        {
            // The final wave always brings an elite carrying the Equipment Cache.
            if (source.Wave != source.WaveCount || _cacheCarrier != null || config.CacheCarrier == null)
                return;

            _cacheCarrier = world.SpawnEnemy(config.CacheCarrier, source.PickSpawnPoint(), config.CacheCarrierElite);
            _cacheCarrier.Died += OnCacheCarrierDied;
        }

        void OnCacheCarrierDied(Pathfinder.Game.Combat.CombatEntity entity, DamageInfo info)
        {
            _cachePosition = entity.Position;
            if (config.CachePrefab != null)
            {
                _cacheView = Instantiate(config.CachePrefab, PlaneMath.ToWorld(entity.Position), Quaternion.identity);
                _spawnedObjects.Add(_cacheView);
            }
            ShowBanner(Loc.T("banner.cache_dropped"));
        }

        void TickCache()
        {
            if (_cacheCollected || !_cachePosition.HasValue)
                return;

            var collector = NearestPilotWithin(_cachePosition.Value, CachePickupRadius);
            if (collector == null)
                return;

            _cacheCollected = true;
            if (_cacheView != null)
                _cacheView.SetActive(false);

            // Mystery and ambush caches guarantee Rare or better (GDD §10.5).
            var minimum = Context.FromMystery ? Rarity.Rare : Rarity.Common;
            OfferEquipment(Loc.T("choice.cache"), collector, minimum, took => TryCompleteEquipment());
        }

        void OnEquipmentWavesCleared(WaveDirector source) => TryCompleteEquipment();

        void TryCompleteEquipment()
        {
            bool carrierDone = _cacheCarrier == null || !_cacheCarrier.IsAlive;
            if (director.IsComplete && carrierDone && _cacheCollected && !equipmentChoice.IsOpen)
            {
                OpenExit();
                world.Pickups.Vacuum = true;
            }
        }

        /// <summary>
        /// Shows a choice of random equipment (GDD §10.2): 3 items, plus the ship's bonus choices. Items the pilot already has
        /// at the maximum level are never offered.
        /// </summary>
        void OfferEquipment(string title, PlayerShip pilot, Rarity minimum, Action<bool> onClosed)
        {
            var offer = RollEquipment(config.CacheChoices, minimum, pilot, _lootRandom);
            if (offer.Count == 0)
            {
                pilot.AddScraps(config.SkipScraps);
                onClosed?.Invoke(false);
                return;
            }
            equipmentChoice.Show(title, offer, pilot, config.SkipScraps, onClosed);
        }

        public List<EquipmentDefinition> RollEquipment(int count, Rarity minimum, PlayerShip pilot, IRandom random) =>
            EquipmentOffers.Roll(content, pilot, count, Context.GalaxyIndex, minimum, random);

        // ---------------------------------------------------------------- Dark Matter level (GDD §10.3)

        void SpawnHarvestNodes()
        {
            int count = _random.Range(config.DarkMatterNodesMin, config.DarkMatterNodesMax + 1);
            Vector2 half = world.ArenaSize * 0.5f - new Vector2(4f, 4f);

            for (int i = 0; i < count; i++)
            {
                Vector2 position = Vector2.zero;
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    position = new Vector2(_random.Range(-half.x, half.x), _random.Range(-half.y, half.y));
                    if (IsSpacedOut(position))
                        break;
                }

                var node = new HarvestNode { Position = position };
                if (config.DarkMatterNodePrefab != null)
                {
                    node.View = Instantiate(config.DarkMatterNodePrefab, PlaneMath.ToWorld(position), Quaternion.identity);
                    node.Marker = node.View.GetComponent<ObjectiveMarker>();
                    _spawnedObjects.Add(node.View);
                }
                _nodes.Add(node);
            }
        }

        bool IsSpacedOut(Vector2 position)
        {
            if (Vector2.Distance(position, config.PlayerEntry) < 6f || Vector2.Distance(position, config.ExitPosition) < 4f)
                return false;
            foreach (var other in _nodes)
            {
                if (Vector2.Distance(position, other.Position) < 5f)
                    return false;
            }
            return true;
        }

        void TickHarvest(float deltaTime)
        {
            foreach (var node in _nodes)
            {
                if (node.Harvested)
                    continue;

                bool occupied = NearestPilotWithin(node.Position, config.HarvestRadius) != null;
                node.Progress = occupied ? node.Progress + deltaTime : Mathf.Max(0f, node.Progress - deltaTime * 0.5f);
                if (node.Marker != null)
                    node.Marker.SetProgress(node.Progress / config.HarvestTime);

                if (node.Progress >= config.HarvestTime - 1e-4f)
                {
                    node.Harvested = true;
                    DarkMatterHarvested++;
                    int amount = Mathf.RoundToInt(RunConfig.ForGalaxy(config.DarkMatterPerNode, Context.GalaxyIndex) * RewardMultiplier);
                    _run?.AddDarkMatter(amount);
                    if (node.View != null)
                        node.View.SetActive(false);
                    ShowBanner(DarkMatterHarvested == _nodes.Count ? Loc.T("banner.darkmatter_all") : Loc.T("banner.darkmatter", ("amount", amount)));
                }
            }

            InstabilityRemaining = Mathf.Max(0f, InstabilityRemaining - deltaTime);
            if (InstabilityRemaining <= 0f && !_escalated)
            {
                // The greed check: staying after the timer runs out gets dangerous fast.
                _escalated = true;
                director.BudgetMultiplier = config.EscalatedBudgetMultiplier;
                director.WaveTimeout = config.EscalatedWaveInterval;
                ShowBanner(Loc.T("banner.unstable"));
            }
        }

        // ---------------------------------------------------------------- Repair and Shop (GDD §10.4, §10.6)

        void SpawnStation(GameObject prefab)
        {
            _stationPosition = Vector2.zero;
            _stationUsed = false;
            if (Context.Type == MapNodeType.Shop)
                RollCatalog();
            if (prefab == null)
                return;

            var station = Instantiate(prefab, PlaneMath.ToWorld(_stationPosition), Quaternion.identity);
            _stationMarker = station.GetComponent<ObjectiveMarker>();
            _spawnedObjects.Add(station);
        }

        void TickStation()
        {
            if (dialog.IsOpen)
                return;

            var pilot = NearestPilotWithin(_stationPosition, StationRadius);
            bool usable = Context.Type == MapNodeType.Shop || !_stationUsed;
            if (pilot != null && usable && pilot.InteractPressed)
                OpenStation(pilot);
        }

        void OpenStation(PlayerShip pilot)
        {
            if (Context.Type == MapNodeType.Repair)
                OpenRepair(pilot);
            else
                OpenShop(pilot);
        }

        /// <summary>Repair station: restore 40% hull, or +15 max hull for the run. One use.</summary>
        void OpenRepair(PlayerShip pilot)
        {
            dialog.Show(Loc.T("repair.title"), () => Loc.T("repair.body", ("hull", Mathf.CeilToInt(pilot.Vitals.Hull)), ("max", Mathf.RoundToInt(pilot.Vitals.MaxHull))), () =>
            {
                float repair = pilot.Vitals.MaxHull * config.RepairFraction;
                return new[]
                {
                    new ChoiceDialog.Option
                    {
                        Label = Loc.T("repair.repair"), Note = Loc.T("repair.repair.note", ("amount", Mathf.RoundToInt(repair))), NoteColor = new Color(0.45f, 1f, 0.6f),
                        Detail = Loc.T("repair.repair.detail", ("percent", config.RepairFraction * 100f)),
                        Icon = Icons != null ? Icons.Repair : null, IconTint = RepairGreen,
                        OnChoose = () => { ApplyRepair(pilot, RepairChoice.Repair); return false; },
                    },
                    new ChoiceDialog.Option
                    {
                        Label = Loc.T("repair.reinforce"), Note = Loc.T("repair.reinforce.note", ("amount", Mathf.RoundToInt(config.ReinforceHull))), NoteColor = new Color(0.45f, 1f, 0.6f),
                        Detail = Loc.T("repair.reinforce.detail"),
                        Icon = Icons != null ? Icons.Reinforce : null, IconTint = RepairGreen,
                        OnChoose = () => { ApplyRepair(pilot, RepairChoice.Reinforce); return false; },
                    },
                    new ChoiceDialog.Option { Label = Loc.T("common.leave"), OnChoose = () => false },
                };
            });
        }

        public enum RepairChoice { Repair, Reinforce }

        static readonly Color RepairGreen = new Color(0.45f, 1f, 0.6f);

        /// <summary>A price as the Scraps cog and a number (playtest 7: fewer words), or in words without icons.</summary>
        void SetPrice(ChoiceDialog.Option option, int price)
        {
            if (Icons != null && Icons.Scraps != null)
            {
                option.Note = price.ToString();
                option.NoteIcon = Icons.Scraps;
                option.NoteIconTint = UiKit.Gold;
            }
            else
            {
                option.Note = Loc.T("common.price", ("amount", price));
            }
        }

        /// <summary>Applies a repair station choice. Also called directly by tests.</summary>
        public void ApplyRepair(PlayerShip pilot, RepairChoice choice)
        {
            if (Context.Type != MapNodeType.Repair || _stationUsed)
                return;

            if (choice == RepairChoice.Repair)
                pilot.Heal(pilot.Vitals.MaxHull * config.RepairFraction);
            else
                pilot.Reinforce(config.ReinforceHull);

            _stationUsed = true;
            if (_stationMarker != null)
                _stationMarker.SetSpent();
            ShowBanner(Loc.T(choice == RepairChoice.Repair ? "banner.repaired" : "banner.reinforced"));
        }

        /// <summary>The Shop (GDD §10.6): buy equipment, upgrade equipped items, repair hull, reroll the catalog.</summary>
        void OpenShop(PlayerShip pilot)
        {
            dialog.Show(Loc.T("shop.title"), () => Loc.T("shop.body", ("scraps", pilot.Scraps), ("hull", Mathf.CeilToInt(pilot.Vitals.Hull)), ("max", Mathf.RoundToInt(pilot.Vitals.MaxHull))), () =>
            {
                var options = new List<ChoiceDialog.Option>();
                for (int i = 0; i < _catalog.Count; i++)
                {
                    int index = i;
                    var entry = _catalog[i];
                    int price = PriceOf(entry.Item);
                    var outcome = pilot.PreviewEquip(entry.Item);
                    string state = entry.Sold ? Loc.T("shop.sold") : outcome == EquipOutcome.AlreadyMaxed ? Loc.T("shop.maxed") : null;
                    var owned = pilot.Equipment.Find(entry.Item);
                    int level = owned != null ? owned.Level + 1 : 1;
                    string action = outcome == EquipOutcome.LeveledUp ? Loc.T("shop.level_up", ("from", EquipmentChoiceScreen.Roman(owned.Level)), ("to", EquipmentChoiceScreen.Roman(level)))
                        : outcome == EquipOutcome.NeedsSwap ? Loc.T("shop.swap") : Loc.T("shop.new");
                    var option = new ChoiceDialog.Option
                    {
                        Section = i == 0 ? Loc.T("shop.catalog") : null,
                        PreviewItem = entry.Sold ? null : entry.Item,
                        Icon = entry.Item.Icon,
                        IconTint = EquipmentChoiceScreen.RarityColor(entry.Item.Rarity),
                        Label = entry.Item.DisplayName,
                        Detail = Loc.T("shop.item", ("rarity", EquipmentChoiceScreen.RarityName(entry.Item.Rarity)),
                            ("category", EquipmentChoiceScreen.CategoryName(entry.Item.Category)), ("action", action), ("description", entry.Item.Describe(level))),
                        Note = state,
                        Accent = EquipmentChoiceScreen.RarityColor(entry.Item.Rarity),
                        Enabled = !entry.Sold && outcome != EquipOutcome.AlreadyMaxed && pilot.Scraps >= price,
                        OnChoose = () =>
                        {
                            BuyEquipment(pilot, index, reopenShop: true);
                            return dialog.IsOpen;
                        },
                    };
                    if (state == null)
                        SetPrice(option, price);
                    options.Add(option);
                }

                bool firstUpgrade = true;
                foreach (var equipped in pilot.Equipment.All)
                {
                    if (equipped.Level >= EquipmentRules.MaxLevel)
                        continue;
                    var item = equipped.Item;
                    int price = UpgradePrice(equipped.Level);
                    var upgrade = new ChoiceDialog.Option
                    {
                        Section = firstUpgrade ? Loc.T("shop.upgrades") : null,
                        PreviewItem = item,
                        Icon = item.Icon,
                        IconTint = EquipmentChoiceScreen.RarityColor(item.Rarity),
                        Label = Loc.T("shop.upgrade", ("item", item.DisplayName), ("level", EquipmentChoiceScreen.Roman(equipped.Level + 1))),
                        Detail = item.Describe(equipped.Level + 1),
                        Accent = EquipmentChoiceScreen.RarityColor(item.Rarity),
                        Enabled = pilot.Scraps >= price,
                        OnChoose = () => { UpgradeEquipment(pilot, item); return true; },
                    };
                    SetPrice(upgrade, price);
                    options.Add(upgrade);
                    firstUpgrade = false;
                }

                var repair = RepairOffer(pilot, 25, Loc.T("shop.repair_25"));
                repair.Section = Loc.T("shop.services");
                options.Add(repair);
                options.Add(RepairOffer(pilot, Mathf.CeilToInt(pilot.Vitals.MaxHull - pilot.Vitals.Hull), Loc.T("shop.repair_full")));
                options.Add(new ChoiceDialog.Option
                {
                    Label = Loc.T("shop.reroll"),
                    Detail = Loc.T("shop.reroll.detail"),
                    Enabled = pilot.Scraps >= RerollPrice,
                    OnChoose = () => { RerollCatalog(pilot); return true; },
                });
                SetPrice(options[options.Count - 1], RerollPrice);
                options.Add(new ChoiceDialog.Option { Label = Loc.T("common.leave"), OnChoose = () => false });
                return options;
            });
        }

        void RollCatalog()
        {
            _catalog.Clear();
            var pilot = FirstLivingPilot();
            if (pilot == null || content == null)
                return;
            foreach (var item in RollEquipment(config.ShopCatalogSize, Rarity.Common, pilot, _shopRandom))
                _catalog.Add(new CatalogItem { Item = item });
        }

        int PriceOf(EquipmentDefinition item) => config.EquipmentPrices[Mathf.Clamp((int)item.Rarity, 0, config.EquipmentPrices.Length - 1)];

        int UpgradePrice(int currentLevel) => config.UpgradePrices[Mathf.Clamp(currentLevel - 1, 0, config.UpgradePrices.Length - 1)];

        /// <summary>
        /// Buys catalog item <paramref name="index"/>. A full category goes through the swap screen, and the pilot pays only
        /// if a swap is confirmed. Returns true when the purchase completed immediately. Also called directly by tests.
        /// </summary>
        public bool BuyEquipment(PlayerShip pilot, int index, bool reopenShop = false)
        {
            if (Context.Type != MapNodeType.Shop || index < 0 || index >= _catalog.Count)
                return false;

            var entry = _catalog[index];
            int price = PriceOf(entry.Item);
            var outcome = pilot.PreviewEquip(entry.Item);
            if (entry.Sold || outcome == EquipOutcome.AlreadyMaxed || pilot.Scraps < price)
                return false;

            if (outcome == EquipOutcome.NeedsSwap)
            {
                if (dialog.IsOpen)
                    dialog.Close();
                equipmentChoice.ShowSwap(entry.Item, pilot, took =>
                {
                    if (took && pilot.TrySpendScraps(price))
                        entry.Sold = true;
                    if (reopenShop)
                        OpenShop(pilot);
                });
                return false;
            }

            pilot.TrySpendScraps(price);
            pilot.Equip(entry.Item);
            entry.Sold = true;
            return true;
        }

        /// <summary>Upgrades an equipped item one level for Scraps (35 for I→II, 60 for II→III). Also called directly by tests.</summary>
        public bool UpgradeEquipment(PlayerShip pilot, EquipmentDefinition item)
        {
            var entry = pilot.Equipment.Find(item);
            if (Context.Type != MapNodeType.Shop || entry == null || entry.Level >= EquipmentRules.MaxLevel)
                return false;

            int price = UpgradePrice(entry.Level);
            if (!pilot.TrySpendScraps(price))
                return false;
            return pilot.UpgradeEquipment(item);
        }

        /// <summary>Rerolls the unsold catalog. Each reroll costs more (15, then +10). Also called directly by tests.</summary>
        public bool RerollCatalog(PlayerShip pilot)
        {
            if (Context.Type != MapNodeType.Shop || !pilot.TrySpendScraps(RerollPrice))
                return false;
            _rerolls++;
            RollCatalog();
            return true;
        }

        ChoiceDialog.Option RepairOffer(PlayerShip pilot, int hull, string label)
        {
            int missing = Mathf.CeilToInt(pilot.Vitals.MaxHull - pilot.Vitals.Hull);
            int amount = Mathf.Min(hull, missing);
            int price = Mathf.CeilToInt(amount * config.RepairPricePerHull);
            var option = new ChoiceDialog.Option
            {
                Label = label,
                Icon = Icons != null ? Icons.Repair : null,
                IconTint = RepairGreen,
                Note = amount > 0 ? null : Loc.T("shop.hull_full"),
                Enabled = amount > 0 && pilot.Scraps >= price,
                OnChoose = () =>
                {
                    BuyRepair(pilot, amount);
                    return true;
                },
            };
            if (amount > 0)
                SetPrice(option, price);
            return option;
        }

        /// <summary>Buys hull repair at the shop price. Returns false if the pilot can't afford it. Also called directly by tests.</summary>
        public bool BuyRepair(PlayerShip pilot, int hull)
        {
            if (Context.Type != MapNodeType.Shop || hull <= 0)
                return false;

            int price = Mathf.CeilToInt(hull * config.RepairPricePerHull);
            if (!pilot.TrySpendScraps(price))
                return false;

            pilot.Heal(hull);
            return true;
        }

        // ---------------------------------------------------------------- Boss (GDD §7.5, §10.7)

        void SpawnBoss()
        {
            var boss = config.Boss;
            Boss = world.SpawnEnemy(config.BossFor(Context.GalaxyIndex), new Vector2(0f, 8f), boss.Elite, dropsLoot: true, extraHealthMultiplier: boss.HealthMultiplier);
            Boss.Died += OnBossDied;

            for (int i = 0; i < boss.EscortCount && boss.Escort != null; i++)
            {
                float side = boss.EscortCount > 1 ? Mathf.Lerp(-10f, 10f, i / (boss.EscortCount - 1f)) : 0f;
                world.SpawnEnemy(boss.Escort, new Vector2(side, 6f));
            }
        }

        void OnBossDied(Pathfinder.Game.Combat.CombatEntity entity, DamageInfo info)
        {
            Boss = null;

            // The fight is won: every enemy bullet still in the air turns harmless and fades out (playtest 2), and the
            // Scraps on the floor come home.
            world.Projectiles.Defuse(Pathfinder.Core.Combat.Team.Enemy);
            world.Pickups.Vacuum = true;
            Audio.GameAudio.Play(Audio.SoundCue.BossDefeated);
            int cores = RunConfig.ForGalaxy(config.SingularityCores, Context.GalaxyIndex);
            _run?.AddSingularityCores(cores);
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive)
                    player.Heal(player.Vitals.MaxHull * config.BossHealFraction);
            }
            ShowBanner(Loc.T(cores == 1 ? "banner.boss_defeated.one" : "banner.boss_defeated.many", ("cores", cores)));
            OpenExit();
            _bossRewardDelay = BossRewardDelay;
        }

        /// <summary>
        /// Boss rewards include a guaranteed Rare-or-better equipment choice (GDD §7.5). Once it's made, the wormhole draws
        /// the pilots in (playtest 4).
        /// </summary>
        void OfferBossReward()
        {
            var pilot = FirstLivingPilot();
            if (pilot != null)
                OfferEquipment(Loc.T("choice.boss_reward"), pilot, Rarity.Rare, _ => PullIntoWormhole());
        }

        void PullIntoWormhole()
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive)
                    player.PullTo(config.ExitPosition);
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

        // ---------------------------------------------------------------- Mystery event (GDD §10.5)

        bool _ambushRunning;

        void BeginEvent()
        {
            _ambushRunning = false;
            _eventRandom = Context.Seed.CreateRandom("event");
            Event = MysteryEvents.Pick(content != null ? content.Events : null, _run, _eventRandom);
            var pilot = FirstLivingPilot();
            if (Event == null || pilot == null)
            {
                OpenExit();
                return;
            }

            _run?.MarkEventSeen(Event.Id);
            ShowEvent(pilot);
        }

        /// <summary>Plays a specific event in the current event level. For tests and debug tools.</summary>
        public void DebugShowEvent(MysteryEventDefinition definition)
        {
            var pilot = FirstLivingPilot();
            if (!IsActive || !Context.IsEvent || definition == null || pilot == null)
                return;
            if (dialog.IsOpen)
                dialog.Close();
            Event = definition;
            ShowEvent(pilot);
        }

        void ShowEvent(PlayerShip pilot)
        {
            dialog.Show(Event.DisplayName.ToUpperInvariant(), () => Event.BodyText, () =>
            {
                var options = new List<ChoiceDialog.Option>();
                for (int i = 0; i < Event.Choices.Length; i++)
                {
                    var choice = Event.Choices[i];
                    int captured = i;
                    bool available = MysteryEvents.IsAvailable(choice, pilot);
                    options.Add(new ChoiceDialog.Option
                    {
                        Label = Event.ChoiceLabel(i),
                        Detail = MysteryEvents.Describe(choice),
                        Note = available ? null : MysteryEvents.RequirementText(choice),
                        NoteColor = Presentation.Ui.UiKit.Bad,
                        Enabled = available,
                        OnChoose = () =>
                        {
                            ResolveEvent(captured, pilot);
                            return true; // the dialog now shows the result
                        },
                    });
                }
                return options;
            });
        }

        void ResolveEvent(int index, PlayerShip pilot)
        {
            var result = MysteryEvents.Apply(Event, index, pilot, _run, _eventRandom);
            LastEventResult = result;
            string lines = string.Join("\n", result.Lines);
            string body = string.IsNullOrEmpty(result.Text) ? lines : lines.Length > 0 ? $"{result.Text}\n\n{lines}" : result.Text;
            dialog.Show(Event.DisplayName.ToUpperInvariant(), () => body, () => new[]
            {
                new ChoiceDialog.Option
                {
                    Label = Loc.T(result.Ambush ? "common.fight" : "common.continue"),
                    OnChoose = () =>
                    {
                        if (result.EquipmentOffer.HasValue)
                            OfferEquipment(Loc.T("choice.found"), pilot, result.EquipmentOffer.Value, _ => FinishEvent(result));
                        else
                            FinishEvent(result);
                        return false;
                    },
                },
            });
        }

        void FinishEvent(EventResult result)
        {
            if (!result.Ambush)
            {
                _eventFinished = true;
                return;
            }

            _ambushRunning = true;
            RewardMultiplier = config.AmbushRewardMultiplier;
            director.Begin(Context.Seed, Context.GalaxyIndex, Context.Row + 1, config.EquipmentWaves, config.AmbushBudgetMultiplier);
            director.Completed += OnEventAmbushCleared;
            ShowBanner(Loc.T("banner.ambush"));
        }

        void OnEventAmbushCleared(WaveDirector source)
        {
            director.Completed -= OnEventAmbushCleared;
            _ambushRunning = false;
            int bonus = Mathf.RoundToInt(RunConfig.ForGalaxy(config.ScrapsCompletionBonus, Context.GalaxyIndex) * RewardMultiplier);
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsAlive)
                    player.AddScraps(bonus);
            }
            ShowBanner(Loc.T("banner.ambush_survived", ("bonus", bonus)));
            world.Pickups.Vacuum = true;
            OpenExit();
        }

        // ---------------------------------------------------------------- helpers

        void OpenExit()
        {
            if (ExitOpen)
                return;

            ExitOpen = true;
            Audio.GameAudio.Play(Audio.SoundCue.ExitOpen);
            if (config.ExitPrefab != null)
            {
                _exit = Instantiate(config.ExitPrefab, PlaneMath.ToWorld(config.ExitPosition), Quaternion.identity);
                _spawnedObjects.Add(_exit);
            }
        }

        bool AnyPilotWithin(Vector2 point, float radius) => NearestPilotWithin(point, radius) != null;

        static PlayerShip NearestPilotWithin(Vector2 point, float radius)
        {
            PlayerShip best = null;
            float bestDistance = radius;
            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsAlive)
                    continue;
                float distance = Vector2.Distance(player.Position, point);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = player;
                }
            }
            return best;
        }

        void ShowBanner(string text)
        {
            Banner = text;
            _bannerTimer = BannerTime;
        }

        void UpdateTexts()
        {
            Prompt = string.Empty;
            string exit = ExitOpen ? Loc.T("objective.exit_open") : string.Empty;
            (string, object) wave = ("wave", director.Wave), count = ("count", director.WaveCount);

            switch (Context.Type)
            {
                case MapNodeType.Scraps:
                    Objective = ExitOpen ? Loc.T("objective.level_clear") : Loc.T("objective.waves", wave, count);
                    break;
                case MapNodeType.Equipment:
                    bool carrierAlive = _cacheCarrier != null && _cacheCarrier.IsAlive;
                    Objective = ExitOpen ? Loc.T("objective.cache_done")
                        : _cachePosition.HasValue && !_cacheCollected ? Loc.T("objective.cache_collect")
                        : _cacheCollected ? Loc.T("objective.cache_secured")
                        : carrierAlive ? Loc.T("objective.cache_carrier", wave, count)
                        : Loc.T("objective.waves", wave, count);
                    break;
                case MapNodeType.DarkMatter:
                    int seconds = Mathf.CeilToInt(InstabilityRemaining);
                    string timer = InstabilityRemaining > 0f ? Loc.T("objective.instability", ("minutes", seconds / 60), ("seconds", seconds % 60)) : Loc.T("objective.unstable");
                    Objective = Loc.T("objective.darkmatter", ("harvested", DarkMatterHarvested), ("total", _nodes.Count), ("timer", timer), ("exit", exit));
                    break;
                case MapNodeType.Repair:
                    Objective = Loc.T(_stationUsed ? "objective.repaired" : "objective.repair") + exit;
                    if (!_stationUsed && AnyPilotWithin(_stationPosition, StationRadius))
                        Prompt = Loc.T("prompt.repair");
                    break;
                case MapNodeType.Shop:
                    Objective = Loc.T("objective.shop") + exit;
                    if (AnyPilotWithin(_stationPosition, StationRadius))
                        Prompt = Loc.T("prompt.shop");
                    break;
                case MapNodeType.Boss:
                    Objective = Loc.T(ExitOpen ? "objective.boss_done" : "objective.boss");
                    if (!ExitOpen && _levelTime < 8f)
                        Prompt = Loc.T($"{config.BossFor(Context.GalaxyIndex).Id}.hint"); // how to hurt this galaxy's boss
                    break;
                case MapNodeType.Mystery:
                    Objective = ExitOpen ? Loc.T("objective.ambush_done")
                        : _ambushRunning ? Loc.T("objective.ambush", wave, count) : Loc.T("objective.mystery");
                    break;
            }
        }
    }

    /// <summary>Display names for level types: <c>level.{type}</c> and <c>level.{type}.desc</c> in the .loc files.</summary>
    public static class LevelNames
    {
        public static string Title(MapNodeType type) => Loc.T($"level.{Key(type)}");

        public static string Description(MapNodeType type) => Loc.T($"level.{Key(type)}.desc");

        /// <summary>The map symbol for the type.</summary>
        public static string Glyph(MapNodeType type) => Loc.T($"glyph.{Key(type)}");

        static string Key(MapNodeType type) => type.ToString().ToLowerInvariant();
    }
}
