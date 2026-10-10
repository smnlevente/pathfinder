using Pathfinder.Core.Run;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Placeholder Hub (GDD §11.2, §12.1): start a run after choosing a ship and loadout (GDD §5.4), or open the combat
    /// sandbox. Everything is unlocked until the meta progression arrives in M5.
    /// </summary>
    public sealed class HubMenu : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        static readonly string[] MainOptions = { "START RUN", "COMBAT SANDBOX", "QUIT" };

        [SerializeField] ContentDatabase content;

        MenuInput _input;
        bool _choosingLoadout;
        int _selected;
        int _ship, _primary, _secondary;
        GUIStyle _title, _subtitle, _button, _label, _detail;

        void Awake() => _input = new MenuInput();

        void Update()
        {
            var step = _input.ReadStep();
            if (!_choosingLoadout)
            {
                if (step.y != 0)
                    _selected = (_selected - step.y + MainOptions.Length) % MainOptions.Length;
                if (_input.Submitted)
                    ChooseMain(_selected);
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
                _choosingLoadout = false;
        }

        void ChooseMain(int index)
        {
            switch (index)
            {
                case 0:
                    _choosingLoadout = true;
                    _selected = 3;
                    break;
                case 1:
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
                case 0: _ship = Wrap(_ship + step, content.Ships.Length); break;
                case 1: _primary = Wrap(_primary + step, content.PrimaryWeapons.Length); break;
                case 2: _secondary = Wrap(_secondary + step, content.SecondaryWeapons.Length); break;
            }
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

            GUI.Label(new Rect(0f, 150f, width, 90f), "PROJECT PATHFINDER", _title);
            GUI.Label(new Rect(0f, 240f, width, 40f), "NEW HOME", _subtitle);

            if (_choosingLoadout)
                DrawLoadout(width);
            else
                DrawMain(width);
        }

        void DrawMain(float width)
        {
            for (int i = 0; i < MainOptions.Length; i++)
            {
                var rect = new Rect(width * 0.5f - 200f, 420f + i * 80f, 400f, 60f);
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
            _button = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label.normal.textColor = Color.white;
            _detail = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _detail.normal.textColor = new Color(0.75f, 0.85f, 1f);
        }
    }
}
