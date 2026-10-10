using System;
using System.Collections.Generic;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Placeholder modal for station interactions such as Repair or the Shop. It pauses gameplay while open (single-player)
    /// and supports mouse, keyboard and gamepad. The real UI arrives with the UI pass.
    /// </summary>
    public sealed class ChoiceDialog : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        public sealed class Option
        {
            public string Label;
            public bool Enabled = true;

            /// <summary>Runs on selection. Return true to keep the dialog open and refresh it, e.g. after a purchase.</summary>
            public Func<bool> OnChoose;
        }

        string _title;
        Func<string> _body;
        Func<IReadOnlyList<Option>> _buildOptions;
        IReadOnlyList<Option> _options = Array.Empty<Option>();
        int _selected;
        MenuInput _input;
        GUIStyle _titleStyle, _bodyStyle, _optionStyle;

        public bool IsOpen { get; private set; }
        public string Title => _title;
        public IReadOnlyList<Option> Options => _options;

        void Awake() => _input = new MenuInput();

        public void Show(string title, Func<string> body, Func<IReadOnlyList<Option>> buildOptions)
        {
            _title = title;
            _body = body;
            _buildOptions = buildOptions;
            _selected = 0;
            IsOpen = true;
            GameClock.Pause(this);
            Refresh();
        }

        public void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= _options.Count || !_options[index].Enabled)
                return;

            bool keepOpen = _options[index].OnChoose?.Invoke() ?? false;
            if (keepOpen)
                Refresh();
            else
                Close();
        }

        public void Close()
        {
            IsOpen = false;
            GameClock.Resume(this);
        }

        void Refresh()
        {
            _options = _buildOptions?.Invoke() ?? Array.Empty<Option>();
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _options.Count - 1));
        }

        void Update()
        {
            if (!IsOpen)
                return;

            int step = -_input.ReadStep().y;
            if (step != 0 && _options.Count > 0)
                _selected = (_selected + step + _options.Count) % _options.Count;

            if (_input.Submitted)
                Choose(_selected);
            else if (_input.Cancelled)
                Close();
        }

        void OnGUI()
        {
            if (!IsOpen)
                return;

            GUI.depth = -10;
            EnsureStyles();
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            const float panelWidth = 620f;
            float panelHeight = 170f + _options.Count * 56f;
            var panel = new Rect((width - panelWidth) * 0.5f, (ReferenceHeight - panelHeight) * 0.5f, panelWidth, panelHeight);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x, panel.y + 20f, panel.width, 44f), _title, _titleStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 70f, panel.width - 60f, 60f), _body?.Invoke() ?? string.Empty, _bodyStyle);

            for (int i = 0; i < _options.Count; i++)
            {
                var option = _options[i];
                var rect = new Rect(panel.x + 40f, panel.y + 140f + i * 56f, panel.width - 80f, 46f);
                GUI.enabled = option.Enabled;
                GUI.color = i == _selected ? new Color(0.4f, 0.9f, 1f) : Color.white;
                if (GUI.Button(rect, option.Label, _optionStyle))
                {
                    _selected = i;
                    Choose(i);
                }
            }
            GUI.enabled = true;
            GUI.color = Color.white;
        }

        void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = Color.white;
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _bodyStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);
            _optionStyle = new GUIStyle(GUI.skin.button) { fontSize = 22 };
        }

        void OnDisable()
        {
            if (IsOpen)
                Close();
        }
    }
}
