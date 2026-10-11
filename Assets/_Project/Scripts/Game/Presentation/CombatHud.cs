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
    /// area, so they stay in view on ultrawide screens. Built in uGUI below the menus, which cover it when open.
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

        static readonly Color HullColor = new Color(0.95f, 0.35f, 0.3f);
        static readonly Color ShieldColor = new Color(0.3f, 0.85f, 1f);
        static readonly Color DashColor = new Color(0.4f, 1f, 0.9f);
        static readonly Color WeaponColor = new Color(0.55f, 0.85f, 1f);
        static readonly Color WarningColor = new Color(1f, 0.6f, 0.2f);
        static readonly Color ScrapsColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color DarkMatterColor = new Color(0.65f, 0.55f, 1f);
        static readonly Color CoresColor = new Color(1f, 0.55f, 0.85f);
        static readonly Color BossColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color BannerColor = new Color(1f, 0.95f, 0.6f);

        const int MaxDashPips = 4;
        const float VignetteFade = 2.5f;
        const float GamepadReticleDistance = 150f;

        UiBar _hull, _shield, _primary, _secondary, _boss, _shipHull, _shipShield;
        readonly UiBar[] _bossParts = new UiBar[2];
        readonly Text[] _bossPartLabels = new Text[2];
        readonly UiBar[] _dash = new UiBar[MaxDashPips];
        Text _hullText, _primaryText, _secondaryText, _loadoutText;
        Text _scraps, _darkMatter, _cores, _seed, _header, _objective, _bossName, _banner, _prompt, _debug;
        GameObject _runCurrencies, _scrapsRoot;
        ShipSlotsView _slots;
        GameObject _slotsPanel;
        BossIntroCard _bossIntro;
        Text _overlayTitle, _overlayHint;
        GameObject _playerPanel, _bossPanel, _overlay;
        Image _vignette;
        RectTransform _canvasRect, _reticle, _safeArea, _shipReadout;
        Image _reticleFill, _shipRing, _sweetSpot;
        CanvasGroup _shipGroup;
        float _shipAlpha, _lastHitTime = -10f;
        bool _gamepadAiming;
        float _vignetteAlpha;
        float _fps;
        PlayerShip _subscribed;
        readonly StringBuilder _builder = new StringBuilder();

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

            var player = LocalPlayer();
            Subscribe(player);
            _playerPanel.SetActive(player != null && player.Vitals != null);
            _slotsPanel.SetActive(_playerPanel.activeSelf);
            if (_playerPanel.activeSelf)
            {
                RefreshPlayer(player);
                _slots.Refresh(player, EquipmentPreview.Item, EquipmentPreview.ReplaceIndex);
            }

            RefreshStatus(player);
            RefreshOverlay();
            _bossIntro.Update();
            RefreshReticle(player);
            RefreshShipReadout(player);

            _vignetteAlpha = Mathf.Max(0f, _vignetteAlpha - VignetteFade * Time.unscaledDeltaTime);
            _vignette.color = new Color(1f, 0.1f, 0.08f, _vignetteAlpha);
            _vignette.enabled = _vignetteAlpha > 0.001f;
        }

        // ---------------------------------------------------------------- refresh

        void RefreshPlayer(PlayerShip player)
        {
            var vitals = player.Vitals;
            _hull.Set(vitals.Hull / vitals.MaxHull);
            _shield.Rect.gameObject.SetActive(vitals.MaxShield > 0f);
            _shield.Set(vitals.MaxShield > 0f ? vitals.Shield / vitals.MaxShield : 0f);
            _hullText.text = Loc.T("hud.hull", ("hull", Mathf.CeilToInt(vitals.Hull)), ("max", Mathf.RoundToInt(vitals.MaxHull)),
                ("shield", Mathf.CeilToInt(vitals.Shield)), ("god", player.GodMode ? Loc.T("hud.god") : string.Empty));

            // Primary: name, state, and a gauge for ammo, reload or charge.
            var status = player.Primary.Status;
            _primaryText.text = $"{player.PrimaryWeapon.DisplayName.ToUpperInvariant()}   {status.Label}";
            _primary.Set(status.Fill, status.Warning ? WarningColor : WeaponColor);

            bool hasSecondary = player.SecondaryWeapon != null;
            _secondaryText.gameObject.SetActive(hasSecondary);
            _secondary.Rect.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                _secondaryText.text = SecondaryLabel(player);
                // The gauge fills as the next charge comes back; full only when every charge is (playtest 5).
                var cooldown = player.SecondaryCooldown;
                bool full = cooldown.Charges >= cooldown.MaxCharges;
                _secondary.Set(full ? 1f : cooldown.RechargeProgress, cooldown.IsReady ? WeaponColor : WarningColor);
            }

            var dash = player.Dash;
            for (int i = 0; i < MaxDashPips; i++)
            {
                bool shown = i < dash.MaxCharges;
                _dash[i].Rect.gameObject.SetActive(shown);
                if (shown)
                    _dash[i].Set(i < dash.Charges ? 1f : i == dash.Charges ? dash.RechargeProgress : 0f);
            }

            // Ship trait; the equipped items are on the ship diagram (playtest 7). An item without a picture is listed here by name.
            _builder.Clear();
            if (!string.IsNullOrEmpty(player.Definition.TraitTitle))
                _builder.Append(player.Definition.DisplayName.ToUpperInvariant()).Append("   ·   ").Append(player.Definition.TraitTitle).Append('\n');
            foreach (var entry in player.Equipment.All)
            {
                if (entry.Item.Icon != null)
                    continue;
                _builder.Append("<color=#").Append(UiKit.Hex(EquipmentChoiceScreen.RarityColor(entry.Item.Rarity))).Append('>')
                    .Append(entry.Item.DisplayName).Append(' ').Append(EquipmentChoiceScreen.Roman(entry.Level)).Append("</color>\n");
            }
            _loadoutText.text = _builder.ToString();
            _scraps.text = icons != null ? player.Scraps.ToString() : Loc.T("hud.scraps", ("amount", player.Scraps));
        }

        /// <summary>The ship diagram with its equipment slots, for tests.</summary>
        public ShipSlotsView Slots => _slots;

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
        /// reload or charge (GDD §12.2). It replaces the system cursor while flying; menus bring the cursor back.
        /// </summary>
        void RefreshReticle(PlayerShip player)
        {
            bool flying = player != null && player.IsAlive && !_overlay.activeSelf && !ModalScreens.AnyOpen;
            _reticle.gameObject.SetActive(flying);
            Cursor.visible = !flying;
            if (!flying)
                return;

            var mouse = Mouse.current;
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.rightStick.ReadValue().sqrMagnitude > 0.04f)
                _gamepadAiming = true;
            else if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.5f)
                _gamepadAiming = false;

            Vector2 screen;
            if (!_gamepadAiming && mouse != null)
            {
                screen = mouse.position.ReadValue();
            }
            else
            {
                var camera = Camera.main;
                if (camera == null)
                    return;
                Vector2 ship = camera.WorldToScreenPoint(player.transform.position);
                float scale = Screen.height / UiKit.ReferenceSize.y;
                screen = ship + player.AimDirection * (GamepadReticleDistance * scale);
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local))
                _reticle.anchoredPosition = local;

            var status = player.Primary.Status;
            _reticleFill.fillAmount = status.Fill;
            _reticleFill.color = status.Warning ? WarningColor : WeaponColor;
        }

        /// <summary>
        /// The readout on the ship: the ammo ring (reload or charge progress while those run) and hull and shield bars. It
        /// stays faint while all is well and brightens when it matters: reloading, low ammo or hull, or just hit.
        /// </summary>
        void RefreshShipReadout(PlayerShip player)
        {
            bool shown = player != null && player.IsAlive && !_overlay.activeSelf && !ModalScreens.AnyOpen && Camera.main != null;
            _shipReadout.gameObject.SetActive(shown);
            if (!shown)
                return;

            // Centred on the ship's body, which floats half a unit above its ground point. Anchored at the ship's
            // normalised viewport position, so it lines up at any resolution or aspect.
            Vector2 viewport = Camera.main.WorldToViewportPoint(player.transform.position + Vector3.up * 0.5f);
            _shipReadout.anchorMin = _shipReadout.anchorMax = viewport;
            _shipReadout.anchoredPosition = Vector2.zero;

            var status = player.Primary.Status;
            _shipRing.fillAmount = status.Fill;
            _shipRing.color = status.Warning ? WarningColor : WeaponColor;
            var window = player.ReloadWindow;
            _sweetSpot.enabled = window.HasValue;
            if (window.HasValue)
            {
                // The sweet spot of an active reload, on the ring where the reload progress will pass it.
                _sweetSpot.fillAmount = window.Value.y - window.Value.x;
                _sweetSpot.rectTransform.localEulerAngles = new Vector3(0f, 0f, window.Value.x * 360f);
            }

            var vitals = player.Vitals;
            float hull = vitals.Hull / vitals.MaxHull;
            _shipHull.Set(hull);
            _shipShield.Rect.gameObject.SetActive(vitals.MaxShield > 0f);
            _shipShield.Set(vitals.MaxShield > 0f ? vitals.Shield / vitals.MaxShield : 0f);

            bool matters = status.Warning || status.Charge > 0f || status.Fill < 0.3f || hull < 0.35f || Time.unscaledTime - _lastHitTime < 1.5f;
            _shipAlpha = Mathf.MoveTowards(_shipAlpha, matters ? 1f : 0.45f, Time.unscaledDeltaTime * 4f);
            _shipGroup.alpha = _shipAlpha;
        }

        /// <summary>For tests: the ship readout is showing, and its ammo ring's fill.</summary>
        public bool ShipReadoutShown => _shipReadout != null && _shipReadout.gameObject.activeSelf;
        public float ShipAmmoFill => _shipRing != null ? _shipRing.fillAmount : 0f;
        public float ShipHullFill => _shipHull != null ? _shipHull.Fill : 0f;
        public bool SweetSpotShown => _sweetSpot != null && _sweetSpot.enabled;
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

        void Subscribe(PlayerShip player)
        {
            if (player == _subscribed)
                return;
            if (_subscribed != null)
                _subscribed.Damaged -= OnPlayerDamaged;
            _subscribed = player;
            if (_subscribed != null)
                _subscribed.Damaged += OnPlayerDamaged;
        }

        void OnPlayerDamaged(CombatEntity entity, DamageInfo info, DamageResult result)
        {
            if (result.TotalDamage > 0f)
                _lastHitTime = Time.unscaledTime;
            if (result.HullDamage > 0f)
                _vignetteAlpha = Mathf.Clamp(0.35f + result.HullDamage / 40f, 0.35f, 0.8f);
        }

        void OnDestroy()
        {
            BossIntro.Requested -= OnBossIntro;
            _bossIntro?.Stop();
            Subscribe(null);
            Cursor.visible = true;
        }

        static PlayerShip LocalPlayer()
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal)
                    return player;
            }
            return null;
        }

        // ---------------------------------------------------------------- view

        void Build()
        {
            var canvas = UiKit.Canvas("HUD", transform, sortingOrder: 0);
            var root = canvas.transform;
            _canvasRect = (RectTransform)root;

            _vignette = UiKit.Image(root, "Hull Hit", Color.clear, UiKit.Vignette);
            UiKit.Stretch(_vignette.rectTransform);

            // On the ship: an ammo/reload ring around it, hull and shield bars under it. Under every other panel.
            _shipReadout = UiKit.Node("Ship Readout", root);
            _shipReadout.anchorMin = _shipReadout.anchorMax = new Vector2(0.5f, 0.5f);
            _shipReadout.sizeDelta = new Vector2(190f, 190f);
            _shipGroup = _shipReadout.gameObject.AddComponent<CanvasGroup>();
            _shipGroup.interactable = _shipGroup.blocksRaycasts = false;
            var track = UiKit.Image(_shipReadout, "Ammo Track", new Color(1f, 1f, 1f, 0.12f), UiKit.Ring);
            UiKit.Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(164f, 164f));
            _sweetSpot = RingFill(_shipReadout, "Sweet Spot", UiKit.Gold, 178f);
            _sweetSpot.enabled = false;
            _shipRing = RingFill(_shipReadout, "Ammo", WeaponColor, 164f);
            _shipShield = new UiBar(_shipReadout, "Shield", ShieldColor);
            UiKit.Place(_shipShield.Rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -94f), new Vector2(90f, 5f));
            _shipHull = new UiBar(_shipReadout, "Hull", HullColor);
            UiKit.Place(_shipHull.Rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -103f), new Vector2(90f, 8f));

            // The corner panels live in a centred 16:9 area, so they stay near the action on ultrawide screens.
            _safeArea = UiKit.Node("Safe Area 16x9", root);
            _safeArea.gameObject.AddComponent<SafeAreaFitter>();

            // Top left: the pilot.
            _playerPanel = UiKit.Node("Pilot", _safeArea).gameObject;
            var panel = (RectTransform)_playerPanel.transform;
            UiKit.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -26f), new Vector2(560f, 520f));

            _shield = Bar(panel, "Shield", ShieldColor, 0f, 380f, 8f);
            _hull = Bar(panel, "Hull", HullColor, 12f, 380f, 30f);
            _hullText = UiKit.Text(_hull.Rect, "Label", string.Empty, 18, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Stretch(_hullText.rectTransform).offsetMin = new Vector2(12f, 0f);
            AddShadow(_hullText);

            _primaryText = Label(panel, "Primary", 50f, 18, WeaponColor);
            _primary = Bar(panel, "Primary Gauge", WeaponColor, 76f, 380f, 6f);
            _secondaryText = Label(panel, "Secondary", 88f, 18, WeaponColor);
            _secondary = Bar(panel, "Secondary Gauge", WeaponColor, 114f, 380f, 6f);
            for (int i = 0; i < MaxDashPips; i++)
            {
                _dash[i] = new UiBar(panel, $"Dash {i + 1}", DashColor);
                UiKit.Place(_dash[i].Rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * 52f, -128f), new Vector2(46f, 10f));
            }
            _loadoutText = Label(panel, "Loadout", 150f, 18, UiKit.TextBody);
            _loadoutText.rectTransform.sizeDelta = new Vector2(560f, 360f);
            // Bottom left: the ship and its equipment slots (playtest 7). On its own canvas above the Shop and the choice
            // cards, so it stays lit while you shop and shows where the item you're pointing at would go.
            var slotsCanvas = UiKit.Canvas("HUD Ship Slots", transform, sortingOrder: 35);
            var slotsArea = UiKit.Node("Safe Area 16x9", slotsCanvas.transform);
            slotsArea.gameObject.AddComponent<SafeAreaFitter>();
            var slotsRoot = UiKit.Node("Ship Slots", slotsArea);
            UiKit.Place(slotsRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(36f, 46f), new Vector2(420f, 340f));
            _slotsPanel = slotsRoot.gameObject;
            _slots = new ShipSlotsView(slotsRoot);

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

            // Crosshair: a centre dot inside a dim track and the ammo ring.
            _reticle = UiKit.Node("Crosshair", root);
            _reticle.anchorMin = _reticle.anchorMax = new Vector2(0.5f, 0.5f);
            _reticle.sizeDelta = new Vector2(56f, 56f);
            UiKit.Stretch(UiKit.Image(_reticle, "Track", new Color(1f, 1f, 1f, 0.18f), UiKit.Ring).rectTransform);
            _reticleFill = UiKit.Image(_reticle, "Ammo", WeaponColor, UiKit.Ring);
            UiKit.Stretch(_reticleFill.rectTransform);
            _reticleFill.type = Image.Type.Filled;
            _reticleFill.fillMethod = Image.FillMethod.Radial360;
            _reticleFill.fillOrigin = (int)Image.Origin360.Top;
            _reticleFill.fillClockwise = false;
            var dot = UiKit.Image(_reticle, "Dot", Color.white, UiKit.Circle);
            UiKit.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));

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
        static Image RingFill(RectTransform parent, string name, Color color, float size)
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
        static void AddShadow(Text text)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }
    }
}
