using Pathfinder.Core.Run;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Placeholder Hub (GDD §11.2, §12.1): start a run after choosing a ship and loadout (GDD §5.4), spend Dark Matter and
    /// Singularity Cores in the Lab, or open the combat sandbox. Locked weapons are skipped in the loadout until bought.
    /// </summary>
    public sealed class HubMenu : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        static readonly string[] MainOptions = { "START RUN", "LAB", "COMBAT SANDBOX", "QUIT" };

        enum Page { Main, Loadout, Lab }

        [SerializeField] ContentDatabase content;

        MenuInput _input;
        Page _screen;
        int _selected;
        int _ship, _primary, _secondary;
        GUIStyle _title, _subtitle, _button, _label, _detail;

        void Awake() => _input = new MenuInput();

        void Update()
        {
            var step = _input.ReadStep();
            if (_screen == Page.Main)
            {
                if (step.y != 0)
                    _selected = (_selected - step.y + MainOptions.Length) % MainOptions.Length;
                if (_input.Submitted)
                    ChooseMain(_selected);
                return;
            }

            if (_screen == Page.Lab)
            {
                int count = content.MetaUpgrades.Length + 1;
                if (step.y != 0)
                    _selected = (_selected - step.y + count) % count;
                if (_input.Submitted)
                {
                    if (_selected < content.MetaUpgrades.Length)
                        MetaProgression.TryPurchase(content.MetaUpgrades[_selected]);
                    else
                        _screen = Page.Main;
                }
                else if (_input.Cancelled)
                {
                    _screen = Page.Main;
                }
                return;
            }

            // Loadout rows: ship, primary, secondary, launch. Left/right cycles the row, up/down moves between rows.
            if (step.y != 0)
                _selected = (_selected - step.y + 4) % 4;
            if (step.x != 0)
                Cycle(_selected, step.x);
            if (_input.Submitted && _selected == 3)
                StartRun();
            else if (_input.Cancelled)
                _screen = Page.Main;
        }

        void ChooseMain(int index)
        {
            switch (index)
            {
                case 0:
                    _screen = Page.Loadout;
                    _selected = 3;
                    _primary = NextAvailable(content.PrimaryWeapons, _primary, 0);
                    _secondary = NextAvailable(content.SecondaryWeapons, _secondary, 0);
                    break;
                case 1:
                    _screen = Page.Lab;
                    _selected = 0;
                    break;
                case 2:
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

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            GUI.Label(new Rect(0f, 110f, width, 90f), "PROJECT PATHFINDER", _title);
            GUI.Label(new Rect(0f, 200f, width, 40f), "NEW HOME", _subtitle);

            var meta = MetaProgression.Current;
            GUI.Label(new Rect(0f, 260f, width, 30f),
                $"DARK MATTER {meta.DarkMatter}   ·   SINGULARITY CORES {meta.SingularityCores}   ·   RUNS {meta.RunsWon} WON / {meta.RunsPlayed}", _detail);

            switch (_screen)
            {
                case Page.Loadout: DrawLoadout(width); break;
                case Page.Lab: DrawLab(width); break;
                default: DrawMain(width); break;
            }
        }

        void DrawLab(float width)
        {
            var meta = MetaProgression.Current;
            var upgrades = content.MetaUpgrades;
            for (int i = 0; i <= upgrades.Length; i++)
            {
                var rect = new Rect(width * 0.5f - 420f, 320f + i * 64f, 840f, 54f);
                string label;
                bool enabled = true;
                if (i == upgrades.Length)
                {
                    label = "BACK";
                }
                else
                {
                    var upgrade = upgrades[i];
                    int tier = upgrade.Effect == MetaUpgradeDefinition.EffectKind.Unlock ? (meta.IsUnlocked(upgrade.Id) ? 1 : 0) : meta.Tier(upgrade.Id);
                    bool maxed = tier >= upgrade.MaxTiers;
                    int cost = upgrade.CostOfTier(tier);
                    string currency = upgrade.Currency == Core.Meta.MetaCurrency.DarkMatter ? "DM" : "Cores";
                    string state = maxed ? "OWNED" : $"{cost} {currency}";
                    label = $"{upgrade.DisplayName}  [{tier}/{upgrade.MaxTiers}]   {upgrade.Describe()}   ·   {state}";
                    enabled = !maxed && meta.Balance(upgrade.Currency) >= cost;
                }

                GUI.enabled = enabled;
                GUI.color = _selected == i ? new Color(0.4f, 0.9f, 1f) : Color.white;
                if (GUI.Button(rect, label, _button))
                {
                    if (i == upgrades.Length)
                        _screen = Page.Main;
                    else
                        MetaProgression.TryPurchase(upgrades[i]);
                }
                if (rect.Contains(Event.current.mousePosition))
                    _selected = i;
            }
            GUI.enabled = true;
            GUI.color = Color.white;
        }

        void DrawMain(float width)
        {
            for (int i = 0; i < MainOptions.Length; i++)
            {
                var rect = new Rect(width * 0.5f - 200f, 360f + i * 80f, 400f, 60f);
                GUI.color = i == _selected ? new Color(0.4f, 0.9f, 1f) : Color.white;
                if (GUI.Button(rect, MainOptions[i], _button))
                    ChooseMain(i);
                if (rect.Contains(Event.current.mousePosition))
                    _selected = i;
            }
            GUI.color = Color.white;
        }

        void DrawLoadout(float width)
        {
            var ship = content.Ships[_ship];
            var primary = content.PrimaryWeapons[_primary];
            var secondary = content.SecondaryWeapons[_secondary];

            Row(width, 0, 340f, "SHIP", ship.DisplayName,
                $"Hull {ship.MaxHull:0}   Shield {ship.MaxShield:0}   Speed {ship.MoveSpeed:0.#}\n{ship.TraitName}: {ship.TraitDescription}");
            Row(width, 1, 500f, "PRIMARY", primary.DisplayName, PrimarySummary(primary));
            Row(width, 2, 620f, "SECONDARY", secondary.DisplayName, $"Cooldown {secondary.Cooldown:0} s");

            var launch = new Rect(width * 0.5f - 200f, 760f, 400f, 64f);
            GUI.color = _selected == 3 ? new Color(0.4f, 0.9f, 1f) : Color.white;
            if (GUI.Button(launch, "LAUNCH", _button))
                StartRun();
            if (launch.Contains(Event.current.mousePosition))
                _selected = 3;
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, 840f, width, 30f), "Up/down: choose row   ·   Left/right: change   ·   Cancel: back", _detail);
        }

        void Row(float width, int row, float y, string title, string value, string detail)
        {
            bool selected = _selected == row;
            GUI.color = selected ? new Color(0.4f, 0.9f, 1f) : Color.white;
            GUI.Label(new Rect(width * 0.5f - 420f, y, 200f, 40f), title, _label);
            if (GUI.Button(new Rect(width * 0.5f - 200f, y, 50f, 40f), "<", _button))
            {
                _selected = row;
                Cycle(row, -1);
            }
            GUI.Label(new Rect(width * 0.5f - 140f, y, 280f, 40f), value, _label);
            if (GUI.Button(new Rect(width * 0.5f + 150f, y, 50f, 40f), ">", _button))
            {
                _selected = row;
                Cycle(row, 1);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(width * 0.5f - 420f, y + 42f, 840f, 70f), detail, _detail);
        }

        static string PrimarySummary(PrimaryWeaponDefinition weapon) => weapon.FireMode == PrimaryFireMode.Charge
            ? $"Hold to charge: {weapon.Damage:0}–{weapon.MaxChargeDamage:0} damage, pierces everything"
            : $"{weapon.Damage:0}{(weapon.ProjectilesPerShot > 1 ? "×" + weapon.ProjectilesPerShot : "")} damage   {weapon.ShotsPerSecond:0.#}/s   magazine {weapon.MagazineSize}";

        void EnsureStyles()
        {
            if (_title != null)
                return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.5f, 0.9f, 1f);
            _subtitle = new GUIStyle(_title) { fontSize = 30 };
            _subtitle.normal.textColor = new Color(0.8f, 0.9f, 1f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label.normal.textColor = Color.white;
            _detail = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _detail.normal.textColor = new Color(0.75f, 0.85f, 1f);
        }
    }
}
