using System.Collections.Generic;
using Pathfinder.Core.Run;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// The Hub (GDD §11.2, §12.1): start a run or Survival (GDD §3.2) after choosing a ship and loadout (GDD §5.4), or spend
    /// Dark Matter and Singularity Cores in the Pathfinder Lab. Locked weapons are skipped in the loadout until bought.
    /// Mouse, keyboard and gamepad all work.
    /// </summary>
    public sealed class HubMenu : MonoBehaviour
    {
        static readonly string[] MainOptions = { "hub.start", "hub.survival", "hub.lab", "hub.options", "hub.quit" };
        readonly List<string> _mainKeys = new List<string>(); // the main page's rows: Continue Run first when a run is saved

        public enum Page { Main, Loadout, Lab, Options }

        [SerializeField] ContentDatabase content;

        MenuInput _input;
        Page _page;
        int _selected;
        int _ship, _primary, _secondary;
        bool _survival; // the loadout page launches Survival rather than a run

        Text _title, _subtitle, _meta, _pageTitle, _hint, _credits;

        static readonly Color WeaponColor = new Color(0.55f, 0.85f, 1f);
        static readonly Color DarkMatterColor = new Color(0.65f, 0.55f, 1f);
        RectTransform _list, _panel;
        UiScrollList _scroll;

        /// <summary>The panel starts this far below the top of the 1080-unit-high canvas and must end above its bottom.</summary>
        const float PanelTop = 300f, BottomMargin = 30f;

        /// <summary>The page's rows and the list that scrolls them. For tests.</summary>
        public IReadOnlyList<UiRow> Rows => _rows;
        public UiScrollList List => _scroll;
        public RectTransform Panel => _panel;
        readonly List<UiRow> _rows = new List<UiRow>();
        readonly List<bool> _enabled = new List<bool>();

        public Page CurrentPage => _page;

        /// <summary>Opens a page directly. For tests and debug tools.</summary>
        public void Open(Page page) => ShowPage(page);

        void Awake()
        {
            _input = new MenuInput();
            Build();
            ShowPage(Page.Main);
            Loc.Changed += OnLanguageChanged;
        }

        void OnDestroy() => Loc.Changed -= OnLanguageChanged;

        void OnLanguageChanged() => ShowPage(_page, keepSelection: true);

        void Update()
        {
            var step = _input.ReadStep();
            if (step.y != 0 && _rows.Count > 0)
            {
                _selected = (_selected - step.y + _rows.Count) % _rows.Count;
                _scroll.ScrollTo(_rows[_selected].Rect); // keep the selection in view on a long page
            }

            if (_page == Page.Loadout && step.x != 0 && _selected < 3)
                Cycle(_selected, step.x);
            else if (_page == Page.Options && step.x != 0 && _selected < SettingsOptions.All.Count)
                ChangeOption(_selected, option => option.Change(step.x, false));
            else if (_input.Submitted)
                Activate(_selected);
            else if (_input.Cancelled && _page != Page.Main)
                ShowPage(Page.Main);

            _meta.text = MetaLine();
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetState(i == _selected, _enabled[i]);
        }

        // ---------------------------------------------------------------- actions

        void Activate(int index)
        {
            switch (_page)
            {
                case Page.Main:
                    ChooseMain(index);
                    break;
                case Page.Loadout:
                    if (index == 3 && _survival)
                        StartSurvival();
                    else if (index == 3)
                        StartRun();
                    else
                        Cycle(index, 1);
                    break;
                case Page.Options:
                    if (index < SettingsOptions.All.Count)
                        ChangeOption(index, option => option.Click());
                    else
                    {
                        ShowPage(Page.Main);
                    }
                    break;
                case Page.Lab:
                    if (index < content.MetaUpgrades.Length)
                    {
                        if (MetaProgression.TryPurchase(content.MetaUpgrades[index]))
                            ShowPage(Page.Lab, keepSelection: true);
                    }
                    else
                    {
                        ShowPage(Page.Main);
                    }
                    break;
            }
        }

        /// <summary>Changes an option and shows the new value. A new language rebuilds the page in that language.</summary>
        void ChangeOption(int index, System.Action<SettingOption> change)
        {
            var option = SettingsOptions.All[index];
            string language = Loc.Language;
            change(option);
            if (Loc.Language != language)
                ShowPage(Page.Options, keepSelection: true);
            else if (index < _rows.Count)
                OptionRows.Refresh(_rows[index], option);
        }

        void ChooseMain(int index)
        {
            switch (index < _mainKeys.Count ? _mainKeys[index] : string.Empty)
            {
                case "hub.continue":
                    ContinueRun();
                    break;
                case "hub.start":
                case "hub.survival":
                    OpenLoadout(survival: _mainKeys[index] == "hub.survival");
                    break;
                case "hub.lab":
                    ShowPage(Page.Lab);
                    break;
                case "hub.options":
                    ShowPage(Page.Options);
                    break;
                default:
                    Application.Quit();
                    break;
            }
        }

        /// <summary>Picks the saved run back up at the galaxy map it was saved at (M7).</summary>
        public void ContinueRun()
        {
            if (!RunSaveFile.TryLoad(out var data))
            {
                ShowPage(Page.Main);
                return;
            }
            var run = RunSession.Resume(data);
            CombatWorld.NextSeedOverride = run.Seed;
            SceneManager.LoadScene(SceneNames.Run);
        }

        /// <summary>The loadout page, launching a run or Survival.</summary>
        public void OpenLoadout(bool survival)
        {
            _survival = survival;
            _primary = NextAvailable(content.PrimaryWeapons, _primary, 0);
            _secondary = NextAvailable(content.SecondaryWeapons, _secondary, 0);
            ShowPage(Page.Loadout);
            _selected = 3;
        }

        void Cycle(int row, int step)
        {
            switch (row)
            {
                case 0: _ship = NextAvailable(content.Ships, _ship, step); break;
                case 1: _primary = NextAvailable(content.PrimaryWeapons, _primary, step); break;
                case 2: _secondary = NextAvailable(content.SecondaryWeapons, _secondary, step); break;
            }
            ShowPage(Page.Loadout, keepSelection: true);
        }

        /// <summary>Steps through the list, skipping content that is still locked (GDD §11.2).</summary>
        int NextAvailable<T>(T[] items, int current, int step) where T : ContentDefinition
        {
            if (items.Length == 0)
                return 0;
            int direction = step < 0 ? -1 : 1;
            int index = step == 0 ? current : Wrap(current + direction, items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                if (MetaProgression.IsAvailable(items[index], content))
                    return index;
                index = Wrap(index + direction, items.Length);
            }
            return current;
        }

        static int Wrap(int value, int count) => count == 0 ? 0 : (value % count + count) % count;

        /// <summary>Starts a new run. A saved one is given up, and what it earned is banked as a lost run.</summary>
        public void StartRun()
        {
            RunSaveFile.DiscardAndBank();
            Launch(SceneNames.Run);
        }

        public void StartSurvival() => Launch(SceneNames.Survival);

        void Launch(string scene)
        {
            // The run seed also seeds the arena, so the whole run replays from a single code.
            var loadout = new PilotLoadout(content.Ships[_ship].Id, content.PrimaryWeapons[_primary].Id, content.SecondaryWeapons[_secondary].Id);
            var run = RunSession.Begin(loadout: loadout);
            CombatWorld.NextSeedOverride = run.Seed;
            SceneManager.LoadScene(scene);
        }

        // ---------------------------------------------------------------- view

        void Build()
        {
            var canvas = UiKit.Canvas("Hub", transform, sortingOrder: 0);
            var root = canvas.transform;
            var background = UiKit.Image(root, "Background", new Color(0.012f, 0.02f, 0.045f, 1f));
            UiKit.Stretch(background.rectTransform);

            var title = _title = UiKit.Text(root, "Title", string.Empty, 76, new Color(0.5f, 0.9f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1600f, 96f));
            var subtitle = _subtitle = UiKit.Text(root, "Subtitle", string.Empty, 32, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(1600f, 44f));
            _meta = UiKit.Text(root, "Profile", string.Empty, 20, UiKit.TextBody, TextAnchor.MiddleCenter);
            UiKit.Place(_meta.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -242f), new Vector2(1600f, 30f));
            // The icons' licence (CC BY 3.0) asks for credit where they're used.
            _credits = UiKit.Text(root, "Credits", string.Empty, 14, UiKit.TextDim, TextAnchor.MiddleCenter);
            UiKit.Place(_credits.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1600f, 20f));

            var panel = UiKit.Image(root, "Panel", UiKit.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(900f, 0f));
            UiKit.Vertical(panel, 12f, new RectOffset(40, 40, 28, 26));
            UiKit.FitContent(panel);
            _pageTitle = UiKit.Text(panel.transform, "Page", string.Empty, 30, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            _panel = panel.rectTransform;
            _scroll = new UiScrollList(panel.transform);
            _list = _scroll.Content;
            _hint = UiKit.Text(panel.transform, "Hint", string.Empty, 17, UiKit.TextDim, TextAnchor.MiddleCenter);
        }

        void ShowPage(Page page, bool keepSelection = false)
        {
            _page = page;
            if (!keepSelection)
                _selected = 0;

            for (int i = _list.childCount - 1; i >= 0; i--)
                Destroy(_list.GetChild(i).gameObject);
            _list.DetachChildren();
            if (!keepSelection)
                _list.anchoredPosition = Vector2.zero;
            _rows.Clear();
            _enabled.Clear();
            _title.text = Loc.T("hub.title");
            _subtitle.text = Loc.T("hub.subtitle");
            _credits.text = Loc.T("hub.credits");

            switch (page)
            {
                case Page.Main:
                    _pageTitle.text = string.Empty;
                    _mainKeys.Clear();
                    bool saved = RunSaveFile.TryLoad(out var save);
                    if (saved)
                        _mainKeys.Add("hub.continue");
                    _mainKeys.AddRange(MainOptions);
                    foreach (string key in _mainKeys)
                    {
                        string detail = key == "hub.continue" ? ContinueDetail(save)
                            : key == "hub.survival" ? SurvivalDetail()
                            : key == "hub.start" && saved ? Loc.T("hub.start.detail_saved")
                            : Loc.TOr(key + ".detail", null);
                        AddRow(Loc.T(key), null, detail, key == "hub.continue" ? UiKit.Accent : (Color?)null);
                    }
                    _hint.text = Loc.T("hub.main.hint");
                    break;

                case Page.Loadout:
                {
                    _pageTitle.text = Loc.T(_survival ? "hub.loadout.survival" : "hub.loadout");
                    var ship = content.Ships[_ship];
                    var primary = content.PrimaryWeapons[_primary];
                    var secondary = content.SecondaryWeapons[_secondary];
                    AddRow(Loc.T("hub.loadout.ship", ("name", ship.DisplayName)), null, Loc.T("hub.loadout.ship.detail",
                        ("hull", ship.MaxHull), ("shield", ship.MaxShield), ("speed", ship.MoveSpeed), ("trait", ship.TraitTitle), ("description", ship.TraitText)))
                        .SetIcon(ship.Icon, Color.Lerp(new Color(0.55f, 0.65f, 0.9f), Color.white, 0.2f) * ship.HullTint, 56f);
                    AddRow(Loc.T("hub.loadout.primary", ("name", primary.DisplayName)), null, PrimarySummary(primary)).SetIcon(primary.Icon, WeaponColor, 56f);
                    AddRow(Loc.T("hub.loadout.secondary", ("name", secondary.DisplayName)), null, Loc.T("hub.loadout.secondary.detail", ("seconds", secondary.Cooldown)))
                        .SetIcon(secondary.Icon, WeaponColor, 56f);
                    AddRow(Loc.T(_survival ? "hub.loadout.launch_survival" : "hub.loadout.launch"), null, null, UiKit.Accent);
                    _hint.text = Loc.T("hub.loadout.hint");
                    break;
                }

                case Page.Options:
                    _pageTitle.text = Loc.T("hub.options.title");
                    foreach (var option in SettingsOptions.All)
                    {
                        // Sliders drag, tickboxes toggle; click steps up, right click steps down (playtest 5).
                        var row = OptionRows.Create(_list, option);
                        int index = _rows.Count;
                        Register(row, true);
                        row.Pointer.RightClicked += () => ChangeOption(index, o => o.RightClick());
                    }
                    AddRow(Loc.T("common.back"));
                    _hint.text = Loc.T("hub.options.hint");
                    break;

                case Page.Lab:
                {
                    _pageTitle.text = Loc.T("hub.lab.title");
                    var meta = MetaProgression.Current;
                    foreach (var upgrade in content.MetaUpgrades)
                    {
                        int tier = upgrade.Effect == MetaUpgradeDefinition.EffectKind.Unlock ? (meta.IsUnlocked(upgrade.Id) ? 1 : 0) : meta.Tier(upgrade.Id);
                        bool maxed = tier >= upgrade.MaxTiers;
                        int cost = upgrade.CostOfTier(tier);
                        bool darkMatter = upgrade.Currency == Core.Meta.MetaCurrency.DarkMatter;
                        string costKey = darkMatter ? "hub.lab.cost.darkmatter" : "hub.lab.cost.cores";
                        string level = upgrade.MaxTiers > 1 ? Loc.T("hub.lab.tier", ("tier", tier), ("max", upgrade.MaxTiers)) : string.Empty;
                        var currencyIcon = content.Icons != null ? (darkMatter ? content.Icons.DarkMatter : content.Icons.Cores) : null;
                        var color = darkMatter ? DarkMatterColor : UiKit.Gold;
                        // A cost reads as a picture of the currency and a number (playtest 7: fewer words).
                        string note = maxed ? Loc.T("hub.lab.owned") : currencyIcon != null ? cost.ToString() : Loc.T(costKey, ("cost", cost));
                        var row = AddRow(upgrade.DisplayName + level, note, upgrade.Describe(), enabled: !maxed && meta.Balance(upgrade.Currency) >= cost, noteColor: color);
                        row.SetIcon(upgrade.Icon, color, 56f);
                        if (!maxed)
                            row.SetNoteIcon(currencyIcon, color);
                    }
                    AddRow(Loc.T("common.back"));
                    _hint.text = Loc.T("hub.lab.hint");
                    break;
                }
            }

            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _rows.Count - 1));
            // A page taller than the screen scrolls instead of running off the bottom (playtest 5).
            _scroll.Fit(_panel, UiKit.ReferenceSize.y - PanelTop - BottomMargin);
            if (_selected < _rows.Count)
                _scroll.ScrollTo(_rows[_selected].Rect);
        }

        UiRow AddRow(string label, string note = null, string detail = null, Color? accent = null, bool enabled = true, Color? noteColor = null)
        {
            var row = new UiRow(_list, label, note, detail, accent, noteColor);
            Register(row, enabled);
            return row;
        }

        void Register(UiRow row, bool enabled)
        {
            int index = _rows.Count;
            row.Pointer.Entered += () => _selected = index;
            row.Pointer.Clicked += () => Activate(index);
            _rows.Add(row);
            _enabled.Add(enabled);
        }

        string MetaLine()
        {
            var meta = MetaProgression.Current;
            return Loc.T("hub.profile", ("darkmatter", meta.DarkMatter), ("cores", meta.SingularityCores), ("won", meta.RunsWon), ("played", meta.RunsPlayed));
        }

        string ContinueDetail(RunSaveFile.Data save)
        {
            var ship = content.FindShip(save.pilot.ship);
            return Loc.T("hub.continue.detail", ("galaxy", save.galaxy + 1), ("galaxyName", Loc.T($"galaxy.{Mathf.Clamp(save.galaxy, 0, 2)}")),
                ("levels", save.levelsCleared), ("ship", ship != null ? ship.DisplayName : save.pilot.ship));
        }

        static string SurvivalDetail()
        {
            int best = MetaProgression.Current.BestSurvivalSeconds;
            return Loc.T("hub.survival.detail", ("best", best > 0 ? Loc.T("hub.survival.best", ("time", SurvivalController.FormatClock(best))) : string.Empty));
        }

        static string PrimarySummary(PrimaryWeaponDefinition weapon) => weapon.FireMode == PrimaryFireMode.Charge
            ? Loc.T("hub.weapon.charge", ("min", weapon.Damage), ("max", weapon.MaxChargeDamage))
            : Loc.T("hub.weapon.auto", ("damage", weapon.Damage),
                ("pellets", weapon.ProjectilesPerShot > 1 ? Loc.T("hub.weapon.pellets", ("count", weapon.ProjectilesPerShot)) : string.Empty),
                ("rate", weapon.ShotsPerSecond), ("magazine", weapon.MagazineSize));
    }
}
