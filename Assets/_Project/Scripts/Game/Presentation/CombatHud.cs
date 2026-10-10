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
    /// overlays; a crosshair whose ring shows ammo, reload or charge; and a red edge flash when the hull is hit. Built in
    /// uGUI below the menus, which cover it when open.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField] RunFlow runFlow;
        [SerializeField] WaveDirector waveDirector;
        [SerializeField, Tooltip("Optional. Shows the debug key hints when present and enabled.")] DebugHotkeys debugHotkeys;
        [SerializeField, Tooltip("Optional. In the Run scene: level, objective and run status.")] RunDirector runDirector;

        static readonly Color HullColor = new Color(0.95f, 0.35f, 0.3f);
        static readonly Color ShieldColor = new Color(0.3f, 0.85f, 1f);
        static readonly Color DashColor = new Color(0.4f, 1f, 0.9f);
        static readonly Color WeaponColor = new Color(0.55f, 0.85f, 1f);
        static readonly Color WarningColor = new Color(1f, 0.6f, 0.2f);
        static readonly Color ScrapsColor = new Color(1f, 0.8f, 0.3f);
        static readonly Color DarkMatterColor = new Color(0.65f, 0.55f, 1f);
        static readonly Color BossColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color BannerColor = new Color(1f, 0.95f, 0.6f);

        const int MaxDashPips = 4;
        const float VignetteFade = 2.5f;
        const float GamepadReticleDistance = 150f;

        UiBar _hull, _shield, _primary, _secondary, _boss;
        readonly UiBar[] _dash = new UiBar[MaxDashPips];
        Text _hullText, _primaryText, _secondaryText, _loadoutText;
        Text _scraps, _darkMatter, _seed, _header, _objective, _bossName, _banner, _prompt, _debug;
        Text _overlayTitle, _overlayHint;
        GameObject _playerPanel, _bossPanel, _overlay;
        Image _vignette;
        RectTransform _canvasRect, _reticle;
        Image _reticleFill;
        bool _gamepadAiming;
        float _vignetteAlpha;
        float _fps;
        PlayerShip _subscribed;
        readonly StringBuilder _builder = new StringBuilder();

        void Awake() => Build();

        void Update()
        {
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);

            var player = LocalPlayer();
            Subscribe(player);
            _playerPanel.SetActive(player != null && player.Vitals != null);
            if (_playerPanel.activeSelf)
                RefreshPlayer(player);

            RefreshStatus(player);
            RefreshOverlay();
            RefreshReticle(player);

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
                var cooldown = player.SecondaryCooldown;
                _secondaryText.text = $"{player.SecondaryWeapon.DisplayName.ToUpperInvariant()}   {(cooldown.IsReady ? Loc.T("hud.ready") : Loc.T("hud.cooldown", ("seconds", cooldown.TimeUntilReady)))}";
                _secondary.Set(cooldown.IsReady ? 1f : cooldown.RechargeProgress, cooldown.IsReady ? WeaponColor : WarningColor);
            }

            var dash = player.Dash;
            for (int i = 0; i < MaxDashPips; i++)
            {
                bool shown = i < dash.MaxCharges;
                _dash[i].Rect.gameObject.SetActive(shown);
                if (shown)
                    _dash[i].Set(i < dash.Charges ? 1f : i == dash.Charges ? dash.RechargeProgress : 0f);
            }

            // Ship trait and equipped items (GDD §8), coloured by rarity.
            _builder.Clear();
            if (!string.IsNullOrEmpty(player.Definition.TraitTitle))
                _builder.Append(player.Definition.DisplayName.ToUpperInvariant()).Append("   ·   ").Append(player.Definition.TraitTitle).Append('\n');
            foreach (var entry in player.Equipment.All)
            {
                _builder.Append("<color=#").Append(UiKit.Hex(EquipmentChoiceScreen.RarityColor(entry.Item.Rarity))).Append('>')
                    .Append(entry.Item.DisplayName).Append(' ').Append(EquipmentChoiceScreen.Roman(entry.Level)).Append("</color>\n");
            }
            _loadoutText.text = _builder.ToString();
            _scraps.text = Loc.T("hud.scraps", ("amount", player.Scraps));
        }

        void RefreshStatus(PlayerShip player)
        {
            _scraps.gameObject.SetActive(player != null);
            _seed.text = world != null
                ? Loc.T("hud.seed", ("seed", RunSeed.ToCode(world.Seed)), ("fps", Settings.GameSettings.ShowFps ? Loc.T("hud.fps", ("fps", _fps)) : string.Empty))
                : string.Empty;

            var run = runDirector != null ? runDirector.Run : null;
            _darkMatter.text = run != null ? Loc.T("hud.currencies", ("darkmatter", run.DarkMatter), ("cores", run.SingularityCores)) : string.Empty;
            _debug.text = debugHotkeys != null && debugHotkeys.isActiveAndEnabled ? debugHotkeys.HintText : string.Empty;

            var level = runDirector != null ? runDirector.Level : null;
            if (level != null && level.IsActive)
            {
                var context = level.Context;
                string kind = context.Ambush ? Loc.T("hud.level.ambush") : context.FromMystery && !context.IsEvent ? Loc.T("hud.level.mystery") : string.Empty;
                _header.text = Loc.T("hud.level", ("galaxy", context.GalaxyIndex + 1), ("row", context.Row + 1), ("type", LevelNames.Title(context.Type)), ("kind", kind));
                _objective.text = level.Objective;
                _banner.text = level.Banner;
                _prompt.text = level.Prompt;

                var boss = level.Boss;
                _bossPanel.SetActive(boss != null && boss.IsAlive);
                if (_bossPanel.activeSelf)
                {
                    _boss.Set(boss.Vitals.Hull / boss.Vitals.MaxHull);
                    _bossName.text = boss.Definition.DisplayName.ToUpperInvariant();
                }
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

        void RefreshOverlay()
        {
            string title = null, hint = null;
            if (runFlow != null && runFlow.IsVictory)
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
            if (result.HullDamage > 0f)
                _vignetteAlpha = Mathf.Clamp(0.35f + result.HullDamage / 40f, 0.35f, 0.8f);
        }

        void OnDestroy()
        {
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

            // Top left: the pilot.
            _playerPanel = UiKit.Node("Pilot", root).gameObject;
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

            // Top right: currencies and the seed.
            _seed = Corner(root, "Seed", -18f, 16, UiKit.TextDim);
            _scraps = Corner(root, "Scraps", -44f, 30, ScrapsColor, FontStyle.Bold);
            _darkMatter = Corner(root, "Dark Matter", -84f, 20, DarkMatterColor, FontStyle.Bold);

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
            _bossName = UiKit.Text(bossRect, "Name", string.Empty, 18, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 26f));

            _banner = UiKit.Text(root, "Banner", string.Empty, 42, BannerColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_banner.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1700f, 60f));
            AddShadow(_banner);
            _prompt = UiKit.Text(root, "Prompt", string.Empty, 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_prompt.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 40f));
            AddShadow(_prompt);
            _debug = UiKit.Text(root, "Dev Keys", string.Empty, 16, UiKit.TextDim, TextAnchor.MiddleLeft);
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
