using System.Collections.Generic;
using Pathfinder.Core.Run;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// The Hub (GDD §11.2, §12.1): start a run after choosing a ship and loadout (GDD §5.4), spend Dark Matter and
    /// Singularity Cores in the Pathfinder Lab, or open the combat sandbox. Locked weapons are skipped in the loadout until
    /// bought. Mouse, keyboard and gamepad all work.
    /// </summary>
    public sealed class HubMenu : MonoBehaviour
    {
        static readonly string[] MainOptions = { "START RUN", "PATHFINDER LAB", "OPTIONS", "COMBAT SANDBOX", "QUIT" };

        static readonly string[] MainDetails =
        {
            "Choose a ship and weapons, then jump into the Shattered Expanse.",
            "Spend Dark Matter and Singularity Cores on permanent upgrades.",
            "Volume, screen shake, fullscreen, and the controls.",
            "Endless waves for practice and tuning.",
            null,
        };

        public enum Page { Main, Loadout, Lab, Options }

        [SerializeField] ContentDatabase content;

        MenuInput _input;
        Page _page;
        int _selected;
        int _ship, _primary, _secondary;

        Text _meta, _pageTitle, _hint;
        RectTransform _list;
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
        }

        void Update()
        {
            var step = _input.ReadStep();
            if (step.y != 0 && _rows.Count > 0)
                _selected = (_selected - step.y + _rows.Count) % _rows.Count;

            if (_page == Page.Loadout && step.x != 0 && _selected < 3)
                Cycle(_selected, step.x);
            else if (_page == Page.Options && step.x != 0 && _selected < SettingsOptions.All.Count)
            {
                SettingsOptions.All[_selected].Change(step.x, false);
                ShowPage(Page.Options, keepSelection: true);
            }
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
                    if (index == 3)
                        StartRun();
                    else
                        Cycle(index, 1);
                    break;
                case Page.Options:
                    if (index < SettingsOptions.All.Count)
                    {
                        SettingsOptions.All[index].Change(1, true);
                        ShowPage(Page.Options, keepSelection: true);
                    }
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

        void ChooseMain(int index)
        {
            switch (index)
            {
                case 0:
                    _primary = NextAvailable(content.PrimaryWeapons, _primary, 0);
                    _secondary = NextAvailable(content.SecondaryWeapons, _secondary, 0);
                    ShowPage(Page.Loadout);
                    _selected = 3;
                    break;
                case 1:
                    ShowPage(Page.Lab);
                    break;
                case 2:
                    ShowPage(Page.Options);
                    break;
                case 3:
                    SceneManager.LoadScene(SceneNames.Sandbox);
                    break;
                default:
                    Application.Quit();
                    break;
            }
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

        public void StartRun()
        {
            // The run seed also seeds the arena, so the whole run replays from a single code.
            var loadout = new PilotLoadout(content.Ships[_ship].Id, content.PrimaryWeapons[_primary].Id, content.SecondaryWeapons[_secondary].Id);
            var run = RunSession.Begin(loadout: loadout);
            CombatWorld.NextSeedOverride = run.Seed;
            SceneManager.LoadScene(SceneNames.Run);
        }

        // ---------------------------------------------------------------- view

        void Build()
        {
            var canvas = UiKit.Canvas("Hub", transform, sortingOrder: 0);
            var root = canvas.transform;
            var background = UiKit.Image(root, "Background", new Color(0.012f, 0.02f, 0.045f, 1f));
            UiKit.Stretch(background.rectTransform);

            var title = UiKit.Text(root, "Title", "PROJECT PATHFINDER", 76, new Color(0.5f, 0.9f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(1600f, 96f));
            var subtitle = UiKit.Text(root, "Subtitle", "NEW HOME", 32, new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(1600f, 44f));
            _meta = UiKit.Text(root, "Profile", string.Empty, 20, UiKit.TextBody, TextAnchor.MiddleCenter);
            UiKit.Place(_meta.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -242f), new Vector2(1600f, 30f));

            var panel = UiKit.Image(root, "Panel", UiKit.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(900f, 0f));
            UiKit.Vertical(panel, 12f, new RectOffset(40, 40, 28, 26));
            UiKit.FitContent(panel);
            _pageTitle = UiKit.Text(panel.transform, "Page", string.Empty, 30, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            _list = UiKit.Node("Rows", panel.transform);
            UiKit.Vertical(_list, 8f);
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
            _rows.Clear();
            _enabled.Clear();

            switch (page)
            {
                case Page.Main:
                    _pageTitle.text = string.Empty;
                    for (int i = 0; i < MainOptions.Length; i++)
                        AddRow(MainOptions[i], null, MainDetails[i]);
                    _hint.text = "Up / Down: choose     Enter: confirm";
                    break;

                case Page.Loadout:
                {
                    _pageTitle.text = "LOADOUT";
                    var ship = content.Ships[_ship];
                    var primary = content.PrimaryWeapons[_primary];
                    var secondary = content.SecondaryWeapons[_secondary];
                    AddRow($"SHIP:   ‹ {ship.DisplayName} ›", null,
                        $"Hull {ship.MaxHull:0}   Shield {ship.MaxShield:0}   Speed {ship.MoveSpeed:0.#}\n{ship.TraitName}: {ship.TraitDescription}");
                    AddRow($"PRIMARY:   ‹ {primary.DisplayName} ›", null, PrimarySummary(primary));
                    AddRow($"SECONDARY:   ‹ {secondary.DisplayName} ›", null, $"Cooldown {secondary.Cooldown:0} s");
                    AddRow("LAUNCH", null, null, UiKit.Accent);
                    _hint.text = "Up / Down: choose row     Left / Right or click: change     Esc: back";
                    break;
                }

                case Page.Options:
                    _pageTitle.text = "OPTIONS";
                    foreach (var option in SettingsOptions.All)
                        AddRow(option.Label, option.Value(), option.Detail, noteColor: UiKit.Accent);
                    AddRow("BACK");
                    _hint.text = "Up / Down: choose     Left / Right or click: change     Esc: back";
                    break;

                case Page.Lab:
                {
                    _pageTitle.text = "PATHFINDER LAB";
                    var meta = MetaProgression.Current;
                    foreach (var upgrade in content.MetaUpgrades)
                    {
                        int tier = upgrade.Effect == MetaUpgradeDefinition.EffectKind.Unlock ? (meta.IsUnlocked(upgrade.Id) ? 1 : 0) : meta.Tier(upgrade.Id);
                        bool maxed = tier >= upgrade.MaxTiers;
                        int cost = upgrade.CostOfTier(tier);
                        string currency = upgrade.Currency == Core.Meta.MetaCurrency.DarkMatter ? "DM" : "CORES";
                        string level = upgrade.MaxTiers > 1 ? $"   {tier} / {upgrade.MaxTiers}" : string.Empty;
                        AddRow(upgrade.DisplayName + level, maxed ? "OWNED" : $"{cost} {currency}", upgrade.Describe(),
                            enabled: !maxed && meta.Balance(upgrade.Currency) >= cost,
                            noteColor: upgrade.Currency == Core.Meta.MetaCurrency.DarkMatter ? new Color(0.65f, 0.55f, 1f) : UiKit.Gold);
                    }
                    AddRow("BACK");
                    _hint.text = "Up / Down: choose     Enter: buy     Esc: back";
                    break;
                }
            }

            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _rows.Count - 1));
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_list.parent);
        }

        void AddRow(string label, string note = null, string detail = null, Color? accent = null, bool enabled = true, Color? noteColor = null)
        {
            var row = new UiRow(_list, label, note, detail, accent, noteColor);
            int index = _rows.Count;
            row.Pointer.Entered += () => _selected = index;
            row.Pointer.Clicked += () => Activate(index);
            _rows.Add(row);
            _enabled.Add(enabled);
        }

        string MetaLine()
        {
            var meta = MetaProgression.Current;
            return $"DARK MATTER {meta.DarkMatter}    ·    SINGULARITY CORES {meta.SingularityCores}    ·    RUNS {meta.RunsWon} WON / {meta.RunsPlayed}";
        }

        static string PrimarySummary(PrimaryWeaponDefinition weapon) => weapon.FireMode == PrimaryFireMode.Charge
            ? $"Hold to charge: {weapon.Damage:0}–{weapon.MaxChargeDamage:0} damage, pierces everything"
            : $"{weapon.Damage:0}{(weapon.ProjectilesPerShot > 1 ? "×" + weapon.ProjectilesPerShot : "")} damage   {weapon.ShotsPerSecond:0.#}/s   magazine {weapon.MagazineSize}";
    }
}
