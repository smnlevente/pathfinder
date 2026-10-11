using System.Text;
using Pathfinder.Core.Combat;
using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Dev;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// The combat HUD (GDD §12.2): hull and shield, weapons, dash charges and equipment top left; currencies top right;
    /// level, objective and the boss bar top centre; banners and prompts; the pause, galaxy-cleared and run-over
    /// overlays; a crosshair whose ring shows ammo, reload or charge; and a red edge flash when the hull is hit. A small
    /// readout follows the ship itself, where the pilot is looking: an ammo/reload ring around it and hull and shield
    /// bars under it (playtest 6: nobody watches the cursor or the corners). The corner panels sit in a centred 16:9
    /// area, so they stay in view on ultrawide screens. In local co-op (M8) every pilot has their own panel, readout and
    /// crosshair in their colour; player 2's ship diagram sits bottom right. Built in uGUI below the menus, which cover it
    /// when open.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField] RunFlow runFlow;
        [SerializeField] WaveDirector waveDirector;
        [SerializeField, Tooltip("Optional. Shows the debug key hints when present and enabled.")] DebugHotkeys debugHotkeys;
        [SerializeField, Tooltip("Optional. In the Run scene: level, objective and run status.")] RunDirector runDirector;
        [SerializeField, Tooltip("Optional. In the Survival scene: the clock, what comes next, and the guardian.")] SurvivalController survival;
        [SerializeField, Tooltip("Optional: currencies as pictures and numbers instead of words (playtest 7).")] Content.GameIcons icons;

        internal static readonly Color HullColor = new Color(0.95f, 0.35f, 0.3f);
        internal static readonly Color ShieldColor = new Color(0.3f, 0.85f, 1f);
        internal static readonly Color DashColor = new Color(0.4f, 1f, 0.9f);
        internal static readonly Color WeaponColor = new Color(0.55f, 0.85f, 1f);
        internal static readonly Color WarningColor = new Color(1f, 0.6f, 0.2f);
        static readonly Color ScrapsColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color DarkMatterColor = new Color(0.65f, 0.55f, 1f);
        static readonly Color CoresColor = new Color(1f, 0.55f, 0.85f);
        static readonly Color BossColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color BannerColor = new Color(1f, 0.95f, 0.6f);

        const int MaxDashPips = 4;
        const float VignetteFade = 2.5f;
        const float GamepadReticleDistance = 150f;

        const int MaxPilots = CoopRules.MaxPilots;

        UiBar _boss;
        readonly UiBar[] _bossParts = new UiBar[2];
        readonly Text[] _bossPartLabels = new Text[2];
        Text _scraps, _darkMatter, _cores, _seed, _header, _objective, _bossName, _banner, _prompt, _debug;
        GameObject _runCurrencies, _scrapsRoot;
        readonly PilotPanel[] _panels = new PilotPanel[MaxPilots];
        readonly ShipReadout[] _readouts = new ShipReadout[MaxPilots];
        readonly RectTransform[] _reticles = new RectTransform[MaxPilots];
        readonly Image[] _reticleFills = new Image[MaxPilots];
        readonly Image[] _reticleTracks = new Image[MaxPilots];
        readonly ShipSlotsView[] _slotViews = new ShipSlotsView[2];
        readonly GameObject[] _slotPanels = new GameObject[2];
        readonly Text[] _slotTags = new Text[2];
        BossIntroCard _bossIntro;
        Text _overlayTitle, _overlayHint;
        GameObject _bossPanel, _overlay;
        Image _vignette;
        RectTransform _canvasRect, _safeArea;
        bool _gamepadAiming;
        float _vignetteAlpha;
        float _fps;
        readonly System.Collections.Generic.List<PlayerShip> _locals = new System.Collections.Generic.List<PlayerShip>();
        readonly System.Collections.Generic.List<PlayerShip> _subscribed = new System.Collections.Generic.List<PlayerShip>();

        void Awake()
        {
            Build();
            BossIntro.Requested += OnBossIntro;
        }

        void OnBossIntro(Enemy boss) => _bossIntro.Play(boss);

        /// <summary>Whether a boss's intro card is showing. For tests.</summary>
        public bool BossIntroPlaying => _bossIntro.IsPlaying;

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);

            CollectLocalPilots();
            Subscribe(_locals);
            bool coop = _locals.Count > 1;
            var first = _locals.Count > 0 ? _locals[0] : null;
            for (int i = 0; i < MaxPilots; i++)
            {
                var pilot = i < _locals.Count ? _locals[i] : null;
                bool shown = pilot != null && pilot.Vitals != null;
                _panels[i].Root.SetActive(shown);
                if (shown)
                    _panels[i].Refresh(pilot, coop);
            }
            RefreshSlots(coop);

            RefreshStatus(first);
            RefreshOverlay();
            _bossIntro.Update();
            bool flying = !_overlay.activeSelf && !ModalScreens.AnyOpen;
            RefreshReticles(flying);
            var view = Camera.main;
            for (int i = 0; i < MaxPilots; i++)
                _readouts[i].Refresh(i < _locals.Count ? _locals[i] : null, flying, coop, view);

            _vignetteAlpha = Mathf.Max(0f, _vignetteAlpha - VignetteFade * Time.unscaledDeltaTime);
            _vignette.color = new Color(1f, 0.1f, 0.08f, _vignetteAlpha);
            _vignette.enabled = _vignetteAlpha > 0.001f;
        }

        // ---------------------------------------------------------------- refresh

        /// <summary>
        /// Bottom left, player 1's ship and its slots; bottom right, player 2's in co-op (M8). While player 3 or 4 picks an
        /// item, theirs borrows a corner. The diagram of the pilot a menu is for lights up where the item would go.
        /// </summary>
        void RefreshSlots(bool coop)
        {
            var chooser = EquipmentPreview.Pilot;
            for (int i = 0; i < _slotViews.Length; i++)
            {
                var pilot = i < _locals.Count && (i == 0 || coop) ? _locals[i] : null;
                if (chooser != null && chooser.PlayerIndex >= _slotViews.Length && chooser.PlayerIndex % _slotViews.Length == i)
                    pilot = chooser;
                bool shown = pilot != null && pilot.Vitals != null;
                _slotPanels[i].SetActive(shown);
                if (!shown)
                    continue;
                bool previewing = chooser == null || chooser == pilot;
                _slotViews[i].Refresh(pilot, previewing ? EquipmentPreview.Item : null, previewing ? EquipmentPreview.ReplaceIndex : -1);
                _slotTags[i].gameObject.SetActive(coop);
                _slotTags[i].text = CoopRules.Tag(pilot.PlayerIndex);
                _slotTags[i].color = CoopRules.PilotColor(pilot.PlayerIndex);
            }
        }

        /// <summary>Each pilot's corner panel (M8). For tests.</summary>
        public System.Collections.Generic.IReadOnlyList<PilotPanel> PilotPanels => _panels;

        /// <summary>Each pilot's readout on their ship (M8). For tests.</summary>
        public System.Collections.Generic.IReadOnlyList<ShipReadout> Readouts => _readouts;

        /// <summary>Player 2's ship diagram, bottom right in co-op (M8). For tests.</summary>
        public ShipSlotsView SecondSlots => _slotViews[1];
        public bool SecondSlotsShown => _slotPanels[1].activeSelf;

        /// <summary>The ship diagram with its equipment slots, for tests.</summary>        /// <summary>The ship diagram with its equipment slots, for tests.</summary>
        public ShipSlotsView Slots => _slotViews[0];

        /// <summary>
        /// "SEEKER SWARM   READY". With several charges it also counts them and shows when the next one returns:
        /// "SEEKER SWARM   1/2   READY · +1 in 4.2s". Charges recharge one at a time.
        /// </summary>
        public static string SecondaryLabel(PlayerShip player)
        {
            var cooldown = player.SecondaryCooldown;
            string name = player.SecondaryWeapon.DisplayName.ToUpperInvariant();
            if (cooldown.MaxCharges <= 1)
                return $"{name}   {(cooldown.IsReady ? Loc.T("hud.ready") : Loc.T("hud.cooldown", ("seconds", cooldown.TimeUntilReady)))}";

            string charges = Loc.T("hud.secondary.charges", ("charges", cooldown.Charges), ("max", cooldown.MaxCharges));
            string state = !cooldown.IsReady ? Loc.T("hud.cooldown", ("seconds", cooldown.TimeUntilReady))
                : cooldown.Charges < cooldown.MaxCharges ? Loc.T("hud.secondary.next", ("seconds", cooldown.TimeUntilNextCharge))
                : Loc.T("hud.ready");
            return $"{name}   {charges}   {state}";
        }

        void RefreshStatus(PlayerShip player)
        {
            _scrapsRoot.SetActive(player != null);
            if (player != null) // co-op pilots share one wallet (M8)
                _scraps.text = icons != null ? player.Scraps.ToString() : Loc.T("hud.scraps", ("amount", player.Scraps));
            _seed.text = world != null
                ? Loc.T("hud.seed", ("seed", RunSeed.ToCode(world.Seed)), ("fps", Settings.GameSettings.ShowFps ? Loc.T("hud.fps", ("fps", _fps)) : string.Empty))
                : string.Empty;

            var run = runDirector != null ? runDirector.Run : survival != null ? survival.Run : null;
            if (icons != null)
            {
                _runCurrencies.SetActive(run != null);
                if (run != null)
                {
                    _darkMatter.text = run.DarkMatter.ToString();
                    _cores.text = run.SingularityCores.ToString();
                }
            }
            else
            {
                _darkMatter.text = run != null ? Loc.T("hud.currencies", ("darkmatter", run.DarkMatter), ("cores", run.SingularityCores)) : string.Empty;
            }
            _debug.text = debugHotkeys != null && debugHotkeys.isActiveAndEnabled ? debugHotkeys.HintText : string.Empty;

            var level = runDirector != null ? runDirector.Level : null;
            if (survival != null)
            {
                _header.text = survival.Header;
                _objective.text = survival.Objective;
                _banner.text = survival.Banner;
                _prompt.text = survival.Prompt;
                ShowBoss(survival.Guardian);
            }
            else if (level != null && level.IsActive)
            {
                var context = level.Context;
                string kind = context.Ambush ? Loc.T("hud.level.ambush") : context.FromMystery && !context.IsEvent ? Loc.T("hud.level.mystery") : string.Empty;
                _header.text = Loc.T("hud.level", ("galaxy", context.GalaxyIndex + 1), ("row", context.Row + 1), ("type", LevelNames.Title(context.Type)), ("kind", kind));
                _objective.text = level.Objective;
                _banner.text = level.Banner;
                _prompt.text = level.Prompt;
                ShowBoss(level.Boss);
            }
            else
            {
                _header.text = runDirector == null && waveDirector != null
                    ? Loc.T("hud.wave", ("wave", waveDirector.Wave), ("enemies", CombatRegistry.Enemies.Count))
                    : string.Empty;
                _objective.text = _banner.text = _prompt.text = string.Empty;
                _bossPanel.SetActive(false);
            }
        }

        void ShowBoss(Enemy boss)
        {
            _bossPanel.SetActive(boss != null && boss.IsAlive);
            if (_bossPanel.activeSelf)
            {
                _bossName.text = boss.Definition.DisplayName.ToUpperInvariant();
                RefreshBossBars(boss);
            }
        }

        /// <summary>One bar for a boss, or one per part for a boss in several pieces, with a reboot countdown on downed parts.</summary>
        void RefreshBossBars(Enemy boss)
        {
            var parts = boss.TryGetComponent(out IMultiPartBoss multi) && multi.PartCount > 1 ? multi : null;
            _boss.Rect.gameObject.SetActive(parts == null);
            if (parts == null)
                _boss.Set(boss.Vitals.Hull / boss.Vitals.MaxHull);

            for (int i = 0; i < _bossParts.Length; i++)
            {
                bool shown = parts != null && i < parts.PartCount;
                _bossParts[i].Rect.gameObject.SetActive(shown);
                if (!shown)
                    continue;

                var part = parts.Part(i);
                float downtime = parts.PartDowntime(i);
                float health = part != null && part.IsAlive ? part.Vitals.Hull / part.Vitals.MaxHull : 0f;
                _bossParts[i].Set(health, downtime > 0f ? UiKit.TextDim : BossColor);
                string name = parts.PartName(i);
                _bossPartLabels[i].text = downtime > 0f ? Loc.T("hud.boss.rebooting", ("name", name), ("seconds", downtime)) : name;
                _bossPartLabels[i].color = downtime > 0f ? WarningColor : Color.white;
            }
        }

        void RefreshOverlay()
        {
            string title = null, hint = null;
            if (survival != null && survival.IsOver)
            {
                title = Loc.T(survival.IsWon ? "hud.survival.victory" : "hud.ship_destroyed");
                hint = $"{SurvivalSummary()}\n\n{Loc.T("hud.survival.end_hint")}";
            }
            else if (runFlow != null && runFlow.IsVictory)
            {
                title = Loc.T("hud.victory");
                hint = $"{RunSummary()}\n\n{Loc.T("hud.end_hint")}";
            }
            else if (runFlow != null && runFlow.IsGameOver)
            {
                title = Loc.T(runDirector != null ? "hud.run_over" : "hud.ship_destroyed");
                hint = runDirector != null ? $"{RunSummary()}\n\n{Loc.T("hud.end_hint")}" : Loc.T("hud.restart_hint");
            }
            else if (runDirector != null && runDirector.State == RunDirector.Phase.GalaxyCleared)
            {
                title = Loc.T("hud.galaxy_cleared");
                hint = Loc.T("hud.jumping", ("galaxy", runDirector.GalaxyName));
            }

            _overlay.SetActive(title != null);
            if (title != null)
            {
                _overlayTitle.text = title;
                _overlayHint.text = hint;
            }
        }

        /// <summary>
        /// The crosshair follows the mouse, or sits along the aim for a gamepad. Its ring shows the primary's ammo,
        /// reload or charge (GDD §12.2). It replaces the system cursor while flying; menus bring the cursor back. In co-op
        /// (M8) each pilot has one in their colour; only player 1 can aim with the mouse.
        /// </summary>
        void RefreshReticles(bool flyingAllowed)
        {
            var mouse = Mouse.current;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.rightStick.ReadValue().sqrMagnitude > 0.04f)
                _gamepadAiming = true;
            else if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.5f)
                _gamepadAiming = false;

            bool coop = _locals.Count > 1;
            bool anyFlying = false;
            var camera = Camera.main;
            for (int i = 0; i < MaxPilots; i++)
            {
                var player = i < _locals.Count ? _locals[i] : null;
                bool flying = flyingAllowed && player != null && player.IsAlive;
                _reticles[i].gameObject.SetActive(flying);
                if (!flying)
                    continue;
                anyFlying = true;

                // The pilot's own controls know how they aim; a scripted pilot (tests) follows the last device used.
                bool usesMouse = player.CommandSource is LocalInputCommandSource input ? input.UsesMouse : i == 0 && !_gamepadAiming && mouse != null;
                Vector2 screen;
                if (usesMouse && mouse != null)
                {
                    screen = mouse.position.ReadValue();
                }
                else
                {
                    if (camera == null)
                        continue;
                    Vector2 ship = camera.WorldToScreenPoint(player.transform.position);
                    float scale = Screen.height / UiKit.ReferenceSize.y;
                    screen = ship + player.AimDirection * (GamepadReticleDistance * scale);
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local))
                    _reticles[i].anchoredPosition = local;

                var status = player.Primary.Status;
                _reticleFills[i].fillAmount = status.Fill;
                _reticleFills[i].color = status.Warning ? WarningColor : WeaponColor;
                var color = CoopRules.PilotColor(player.PlayerIndex);
                _reticleTracks[i].color = coop ? new Color(color.r, color.g, color.b, 0.45f) : new Color(1f, 1f, 1f, 0.18f);
            }
            Cursor.visible = !anyFlying && !MenuMouse.Blocked; // no cursor over a co-op player's menu the mouse can't work
        }

        /// <summary>For tests: player 1's ship readout is showing, and its ammo ring's fill.</summary>
        public bool ShipReadoutShown => _readouts[0].Shown;
        public float ShipAmmoFill => _readouts[0].AmmoFill;
        public float ShipHullFill => _readouts[0].HullFill;
        public bool SweetSpotShown => _readouts[0].SweetSpotShown;
        public RectTransform SafeArea => _safeArea;

        string RunSummary()
        {
            var run = runDirector != null ? runDirector.Run : null;
            if (run == null)
                return string.Empty;
            string banked = runDirector.BankedRewards is { } rewards
                ? "\n" + Loc.T("hud.banked", ("total", rewards.TotalDarkMatter), ("harvested", rewards.DarkMatter),
                    ("converted", rewards.ConvertedScraps), ("cores", rewards.SingularityCores))
                : string.Empty;
            return Loc.T("hud.summary", ("galaxy", run.GalaxyIndex + 1), ("levels", run.LevelsCleared), ("seed", RunSeed.ToCode(run.Seed))) + banked;
        }

        string SurvivalSummary()
        {
            string summary = Loc.T("hud.survival.summary", ("time", SurvivalController.FormatClock(survival.Clock)),
                ("guardians", survival.GuardiansDefeated), ("total", SurvivalController.GuardianCount), ("seed", RunSeed.ToCode(survival.Run.Seed)));
            if (survival.IsNewBest)
                summary += "\n" + Loc.T("hud.survival.best");
            if (survival.BankedRewards is { } rewards)
                summary += "\n" + Loc.T("hud.banked", ("total", rewards.TotalDarkMatter), ("harvested", rewards.DarkMatter),
                    ("converted", rewards.ConvertedScraps), ("cores", rewards.SingularityCores));
            return summary;
        }

        // ---------------------------------------------------------------- hull-hit flash

        /// <summary>Every local pilot's hull hits flash the screen's edge.</summary>
        void Subscribe(System.Collections.Generic.List<PlayerShip> players)
        {
            for (int i = _subscribed.Count - 1; i >= 0; i--)
            {
                if (_subscribed[i] != null && players.Contains(_subscribed[i]))
                    continue;
                if (_subscribed[i] != null)
                    _subscribed[i].Damaged -= OnPlayerDamaged;
                _subscribed.RemoveAt(i);
            }
            foreach (var player in players)
            {
                if (_subscribed.Contains(player))
                    continue;
                player.Damaged += OnPlayerDamaged;
                _subscribed.Add(player);
            }
        }

        void OnPlayerDamaged(CombatEntity entity, DamageInfo info, DamageResult result)
        {
            if (result.HullDamage > 0f)
                _vignetteAlpha = Mathf.Clamp(0.35f + result.HullDamage / 40f, 0.35f, 0.8f);
        }

        void OnDestroy()
        {
            BossIntro.Requested -= OnBossIntro;
            _bossIntro?.Stop();
            _locals.Clear();
            Subscribe(_locals);
            foreach (var readout in _readouts)
                readout?.Release();
            Cursor.visible = true;
        }

        /// <summary>The local pilots, player 1 first.</summary>
        void CollectLocalPilots()
        {
            _locals.Clear();
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal)
                    _locals.Add(player);
            }
            _locals.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
        }

        // ---------------------------------------------------------------- view        // ---------------------------------------------------------------- view

        void Build()
        {
            var canvas = UiKit.Canvas("HUD", transform, sortingOrder: 0);
            var root = canvas.transform;
            _canvasRect = (RectTransform)root;

            _vignette = UiKit.Image(root, "Hull Hit", Color.clear, UiKit.Vignette);
            UiKit.Stretch(_vignette.rectTransform);

            // On each ship: an ammo/reload ring around it, hull and shield bars under it. Under every other panel.
            for (int i = 0; i < MaxPilots; i++)
                _readouts[i] = new ShipReadout(root, i == 0 ? "Ship Readout" : $"Ship Readout P{i + 1}");

            // The corner panels live in a centred 16:9 area, so they stay near the action on ultrawide screens.
            _safeArea = UiKit.Node("Safe Area 16x9", root);
            _safeArea.gameObject.AddComponent<SafeAreaFitter>();

            // The pilots: player 1 top left, player 2 top right below the currencies, players 3 and 4 below them (M8).
            _panels[0] = new PilotPanel(_safeArea, "Pilot", right: false, top: -26f);
            _panels[1] = new PilotPanel(_safeArea, "Pilot P2", right: true, top: -140f);
            _panels[2] = new PilotPanel(_safeArea, "Pilot P3", right: false, top: -200f);
            _panels[3] = new PilotPanel(_safeArea, "Pilot P4", right: true, top: -310f);

            // Bottom left: the ship and its equipment slots (playtest 7); player 2's bottom right in co-op. On their own
            // canvas above the Shop and the choice cards, so they stay lit while you shop and show where the item you're
            // pointing at would go.
            var slotsCanvas = UiKit.Canvas("HUD Ship Slots", transform, sortingOrder: 35);
            var slotsArea = UiKit.Node("Safe Area 16x9", slotsCanvas.transform);
            slotsArea.gameObject.AddComponent<SafeAreaFitter>();
            for (int i = 0; i < _slotViews.Length; i++)
            {
                var corner = new Vector2(i == 0 ? 0f : 1f, 0f);
                var slotsRoot = UiKit.Node(i == 0 ? "Ship Slots" : "Ship Slots P2", slotsArea);
                UiKit.Place(slotsRoot, corner, corner, new Vector2(i == 0 ? 36f : -36f, 46f), new Vector2(420f, 340f));
                _slotPanels[i] = slotsRoot.gameObject;
                _slotViews[i] = new ShipSlotsView(slotsRoot);
                _slotTags[i] = UiKit.Text(slotsRoot, "Player", string.Empty, 26, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
                UiKit.Place(_slotTags[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 24f), new Vector2(120f, 34f));
                AddShadow(_slotTags[i]);
                _slotTags[i].gameObject.SetActive(false);
            }

            _bossIntro = new BossIntroCard(transform, icons != null ? icons.Boss : null);

            // Top right: currencies and the seed.
            _seed = Corner(_safeArea, "Seed", -18f, 16, UiKit.TextDim);
            if (icons != null)
            {
                // Pictures and numbers (playtest 7): a cog for Scraps, a crystal for Dark Matter, a singularity for Cores.
                var scrapsRow = CornerRow(_safeArea, "Scraps", -44f);
                _scrapsRoot = scrapsRow.gameObject;
                _scraps = Counter(scrapsRow, "Scraps", icons.Scraps, 30, ScrapsColor);
                var runRow = CornerRow(_safeArea, "Run Currencies", -88f);
                _runCurrencies = runRow.gameObject;
                _darkMatter = Counter(runRow, "Dark Matter", icons.DarkMatter, 22, DarkMatterColor);
                _cores = Counter(runRow, "Cores", icons.Cores, 22, CoresColor);
            }
            else
            {
                _scraps = Corner(_safeArea, "Scraps", -44f, 30, ScrapsColor, FontStyle.Bold);
                _scrapsRoot = _scraps.gameObject;
                _darkMatter = Corner(_safeArea, "Dark Matter", -84f, 20, DarkMatterColor, FontStyle.Bold);
            }

            // Top centre: level, objective, boss.
            _header = Centre(root, "Level", -14f, 24, Color.white, FontStyle.Bold);
            _objective = Centre(root, "Objective", -48f, 20, new Color(0.8f, 0.92f, 1f));
            _bossPanel = UiKit.Node("Boss", root).gameObject;
            var bossRect = (RectTransform)_bossPanel.transform;
            UiKit.Place(bossRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(800f, 48f));
            _boss = new UiBar(bossRect, "Health", BossColor);
            UiKit.Place(_boss.Rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(800f, 18f));
            foreach (float mark in new[] { 1f / 3f, 2f / 3f })
            {
                // Phase marks at 1/3 and 2/3 health (GDD §7.5: three health bar segments).
                var tick = UiKit.Image(_boss.Rect, "Phase Mark", Color.black);
                UiKit.Place(tick.rectTransform, new Vector2(mark, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 18f));
            }
            // A boss in two parts (the Binary Wardens) gets a bar for each, side by side, named inside the bar.
            for (int i = 0; i < _bossParts.Length; i++)
            {
                _bossParts[i] = new UiBar(bossRect, $"Part {i + 1}", BossColor);
                UiKit.Place(_bossParts[i].Rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(i == 0 ? -205f : 205f, 2f), new Vector2(390f, 22f));
                _bossPartLabels[i] = UiKit.Text(_bossParts[i].Rect, "Label", string.Empty, 17, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Stretch(_bossPartLabels[i].rectTransform).offsetMin = new Vector2(10f, 0f);
                AddShadow(_bossPartLabels[i]);
                _bossParts[i].Rect.gameObject.SetActive(false);
            }
            _bossName = UiKit.Text(bossRect, "Name", string.Empty, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 26f));

            _banner = UiKit.Text(root, "Banner", string.Empty, 42, BannerColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_banner.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1700f, 60f));
            AddShadow(_banner);
            _prompt = UiKit.Text(root, "Prompt", string.Empty, 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_prompt.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 40f));
            AddShadow(_prompt);
            _debug = UiKit.Text(_safeArea, "Dev Keys", string.Empty, 16, UiKit.TextDim, TextAnchor.MiddleLeft);
            UiKit.Place(_debug.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 16f), new Vector2(1840f, 28f));

            // Crosshairs, one per pilot: a centre dot inside a dim track and the ammo ring.
            for (int i = 0; i < MaxPilots; i++)
            {
                var reticle = UiKit.Node(i == 0 ? "Crosshair" : $"Crosshair P{i + 1}", root);
                reticle.anchorMin = reticle.anchorMax = new Vector2(0.5f, 0.5f);
                reticle.sizeDelta = new Vector2(56f, 56f);
                _reticleTracks[i] = UiKit.Image(reticle, "Track", new Color(1f, 1f, 1f, 0.18f), UiKit.Ring);
                UiKit.Stretch(_reticleTracks[i].rectTransform);
                var fill = UiKit.Image(reticle, "Ammo", WeaponColor, UiKit.Ring);
                UiKit.Stretch(fill.rectTransform);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = (int)Image.Origin360.Top;
                fill.fillClockwise = false;
                var dot = UiKit.Image(reticle, "Dot", Color.white, UiKit.Circle);
                UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));
                _reticles[i] = reticle;
                _reticleFills[i] = fill;
                reticle.gameObject.SetActive(false);
            }

            // Full-screen overlays: paused, galaxy cleared, run over.
            var overlay = UiKit.Image(root, "Overlay", new Color(0f, 0f, 0f, 0.65f));
            UiKit.Stretch(overlay.rectTransform);
            _overlay = overlay.gameObject;
            _overlayTitle = UiKit.Text(overlay.transform, "Title", string.Empty, 68, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_overlayTitle.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600f, 90f));
            _overlayHint = UiKit.Text(overlay.transform, "Hint", string.Empty, 24, UiKit.TextBody, TextAnchor.UpperCenter);
            UiKit.Place(_overlayHint.rectTransform, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1500f, 300f));
            _overlay.SetActive(false);
        }

        /// <summary>A ring that fills counter-clockwise from the top, like the crosshair's.</summary>
        internal static Image RingFill(RectTransform parent, string name, Color color, float size)
        {
            var ring = UiKit.Image(parent, name, color, UiKit.Ring);
            UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = false;
            return ring;
        }

        static UiBar Bar(RectTransform parent, string name, Color color, float top, float width, float height)
        {
            var bar = new UiBar(parent, name, color);
            UiKit.Place(bar.Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -top), new Vector2(width, height));
            return bar;
        }

        static Text Label(RectTransform parent, string name, float top, int size, Color color)
        {
            var text = UiKit.Text(parent, name, string.Empty, size, color, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -top), new Vector2(560f, 26f));
            AddShadow(text);
            return text;
        }

        static Text Corner(Transform root, string name, float top, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var text = UiKit.Text(root, name, string.Empty, size, color, TextAnchor.UpperRight, style);
            UiKit.Place(text.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, top), new Vector2(600f, 40f));
            AddShadow(text);
            return text;
        }

        /// <summary>A right-aligned row in the top-right corner that sizes itself to what it holds.</summary>
        static RectTransform CornerRow(Transform root, string name, float top)
        {
            var row = UiKit.Node(name, root);
            UiKit.Place(row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, top), new Vector2(600f, 40f));
            UiKit.Horizontal(row, 22f, null, TextAnchor.MiddleRight);
            return row;
        }

        /// <summary>A picture and a number, e.g. the Scraps cog and how many you have.</summary>
        static Text Counter(Transform row, string name, Sprite icon, int size, Color color)
        {
            var counter = UiKit.Node(name, row);
            UiKit.Horizontal(counter, 6f, null, TextAnchor.MiddleRight);
            var image = UiKit.Image(counter, "Icon", color, icon);
            image.preserveAspect = true;
            UiKit.Layout(image, preferredWidth: size + 8f, preferredHeight: size + 8f);
            var value = UiKit.Text(counter, "Value", string.Empty, size, color, TextAnchor.MiddleLeft, FontStyle.Bold);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddShadow(value);
            return value;
        }

        static Text Centre(Transform root, string name, float top, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var text = UiKit.Text(root, name, string.Empty, size, color, TextAnchor.UpperCenter, style);
            UiKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, top), new Vector2(1100f, 34f));
            AddShadow(text);
            return text;
        }

        /// <summary>A dark drop shadow keeps HUD text readable over bright explosions.</summary>
        internal static void AddShadow(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }
    }
}
