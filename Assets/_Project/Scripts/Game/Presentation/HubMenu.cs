using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Placeholder Hub menu (GDD §11.2): start a run or open the combat sandbox. Hub upgrades and the walkable lab come
    /// with M5.
    /// </summary>
    public sealed class HubMenu : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        static readonly string[] Options = { "START RUN", "COMBAT SANDBOX", "QUIT" };

        MenuInput _input;
        int _selected;
        GUIStyle _title, _subtitle, _button;

        void Awake() => _input = new MenuInput();

        void Update()
        {
            int step = -_input.ReadStep().y;
            if (step != 0)
                _selected = (_selected + step + Options.Length) % Options.Length;
            if (_input.Submitted)
                Choose(_selected);
        }

        public void StartRun()
        {
            // The run seed also seeds the arena, so the whole run replays from a single code.
            var run = RunSession.Begin();
            CombatWorld.NextSeedOverride = run.Seed;
            SceneManager.LoadScene(SceneNames.Run);
        }

        void Choose(int index)
        {
            switch (index)
            {
                case 0: StartRun(); break;
                case 1: SceneManager.LoadScene(SceneNames.Sandbox); break;
                default: Application.Quit(); break;
            }
        }

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            GUI.Label(new Rect(0f, 220f, width, 90f), "PROJECT PATHFINDER", _title);
            GUI.Label(new Rect(0f, 310f, width, 40f), "NEW HOME", _subtitle);

            for (int i = 0; i < Options.Length; i++)
            {
                var rect = new Rect(width * 0.5f - 200f, 460f + i * 80f, 400f, 60f);
                GUI.color = i == _selected ? new Color(0.4f, 0.9f, 1f) : Color.white;
                if (GUI.Button(rect, Options[i], _button))
                    Choose(i);
                if (rect.Contains(Event.current.mousePosition))
                    _selected = i;
            }
            GUI.color = Color.white;
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(0.5f, 0.9f, 1f);
            _subtitle = new GUIStyle(_title) { fontSize = 30 };
            _subtitle.normal.textColor = new Color(0.8f, 0.9f, 1f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
        }
    }
}
