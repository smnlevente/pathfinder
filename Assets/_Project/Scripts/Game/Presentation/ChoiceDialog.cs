using System;
using System.Collections.Generic;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Modal menu for station interactions (Repair, the Shop) and Mystery events. Each option is a row with a label, a
    /// right-aligned note such as a price, and a wrapping detail line; options can be grouped under section headers, and
    /// long lists scroll. It pauses gameplay while open (single-player) and supports mouse, keyboard and gamepad.
    /// </summary>
    public sealed class ChoiceDialog : MonoBehaviour
    {
        const float PanelWidth = 980f;
        const float ListMaxHeight = 600f;

        public sealed class Option
        {
            public string Label;

            /// <summary>Optional second line: what the option does. Wraps, so it is never cut off.</summary>
            public string Detail;

            /// <summary>Optional right-aligned note, e.g. a price or "SOLD".</summary>
            public string Note;

            /// <summary>Optional header shown above this option, starting a new group.</summary>
            public string Section;

            /// <summary>Optional colour stripe, e.g. an item's rarity.</summary>
            public Color? Accent;

            public Color? NoteColor;
            public bool Enabled = true;

            /// <summary>Runs on selection. Return true to keep the dialog open and refresh it, e.g. after a purchase.</summary>
            public Func<bool> OnChoose;
        }

        string _title;
        Func<string> _body;
        Func<IReadOnlyList<Option>> _buildOptions;
        IReadOnlyList<Option> _options = Array.Empty<Option>();
        readonly List<UiRow> _rows = new List<UiRow>();
        int _selected;
        bool _scrollToSelection;
        MenuInput _input;

        Canvas _canvas;
        RectTransform _panel, _content, _viewport;
        Text _titleText, _bodyText;
        LayoutElement _listLayout;

        public bool IsOpen { get; private set; }
        public string Title => _title;
        public IReadOnlyList<Option> Options => _options;

        /// <summary>The rows as built, in option order. For tests and screenshots.</summary>
        public IReadOnlyList<UiRow> Rows => _rows;

        public int Selected => _selected;

        void Awake()
        {
            _input = new MenuInput();
            Build();
            _canvas.gameObject.SetActive(false);
        }

        public void Show(string title, Func<string> body, Func<IReadOnlyList<Option>> buildOptions)
        {
            _title = title;
            _body = body;
            _buildOptions = buildOptions;
            _selected = 0;
            IsOpen = true;
            GameClock.Pause(this);
            ModalScreens.SetOpen(this, true);
            _canvas.gameObject.SetActive(true);
            Refresh();
        }

        public void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= _options.Count || !_options[index].Enabled)
                return;

            bool keepOpen = _options[index].OnChoose?.Invoke() ?? false;
            if (!IsOpen)
                return; // the option opened another screen and closed this one
            if (keepOpen)
                Refresh();
            else
                Close();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            GameClock.Resume(this);
            ModalScreens.SetOpen(this, false);
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Rebuilds the options, e.g. after the pilot's Scraps changed outside the dialog.</summary>
        public void RefreshOptions()
        {
            if (IsOpen)
                Refresh();
        }

        void Refresh()
        {
            _options = _buildOptions?.Invoke() ?? Array.Empty<Option>();
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _options.Count - 1));
            _titleText.text = _title;
            UpdateBody();
            RebuildRows();
        }

        void UpdateBody()
        {
            string body = _body?.Invoke() ?? string.Empty;
            _bodyText.text = body;
            _bodyText.gameObject.SetActive(body.Length > 0);
        }

        void Update()
        {
            if (!IsOpen)
                return;

            UpdateBody();
            int step = -_input.ReadStep().y;
            if (step != 0 && _options.Count > 0)
            {
                _selected = (_selected + step + _options.Count) % _options.Count;
                _scrollToSelection = true;
            }

            if (_input.Submitted)
                Choose(_selected);
            else if (_input.Cancelled)
                Close();

            if (!IsOpen)
                return;
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetState(i == _selected, _options[i].Enabled);
            if (_scrollToSelection)
                ScrollToSelection();
        }

        // ---------------------------------------------------------------- view

        void Build()
        {
            _canvas = UiKit.Canvas("Choice Dialog", transform, sortingOrder: 20);
            UiKit.Backdrop(_canvas.transform);

            var panel = UiKit.Image(_canvas.transform, "Panel", UiKit.Panel);
            _panel = UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelWidth, 0f));
            UiKit.Vertical(panel, 16f, new RectOffset(44, 44, 34, 30));
            UiKit.FitContent(panel);

            _titleText = UiKit.Text(_panel, "Title", string.Empty, 40, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            _bodyText = UiKit.Text(_panel, "Body", string.Empty, 22, UiKit.TextBody, TextAnchor.UpperCenter);

            var list = UiKit.Node("List", _panel);
            _listLayout = UiKit.Layout(list, preferredHeight: 100f);
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            _viewport = UiKit.Stretch(UiKit.Image(list, "Viewport", Color.clear, raycast: true).rectTransform);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _content = UiKit.Node("Content", _viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            UiKit.Vertical(_content, 8f);
            UiKit.FitContent(_content);
            scroll.viewport = _viewport;
            scroll.content = _content;

            UiKit.Text(_panel, "Hint", Localization.Loc.T("dialog.hint"), 17, UiKit.TextDim, TextAnchor.MiddleCenter);
        }

        void RebuildRows()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            _content.DetachChildren();
            _rows.Clear();

            for (int i = 0; i < _options.Count; i++)
            {
                var option = _options[i];
                if (!string.IsNullOrEmpty(option.Section))
                {
                    var header = UiKit.Text(_content, "Section", option.Section.ToUpperInvariant(), 18, UiKit.TextDim, TextAnchor.LowerLeft, FontStyle.Bold);
                    UiKit.Layout(header, preferredHeight: i == 0 ? 26f : 40f);
                }

                var row = new UiRow(_content, option.Label, option.Note, option.Detail, option.Accent, option.NoteColor);
                int index = i;
                row.Pointer.Entered += () => _selected = index;
                row.Pointer.Clicked += () => Choose(index);
                row.SetState(i == _selected, option.Enabled);
                _rows.Add(row);
            }

            // Size the list to its content, up to a maximum; beyond that it scrolls.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _listLayout.preferredHeight = Mathf.Min(LayoutUtility.GetPreferredHeight(_content), ListMaxHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            _scrollToSelection = true;
        }

        void ScrollToSelection()
        {
            _scrollToSelection = false;
            if (_selected < 0 || _selected >= _rows.Count)
                return;

            var row = _rows[_selected].Rect;
            float top = -row.anchoredPosition.y - row.rect.height * (1f - row.pivot.y);
            float bottom = top + row.rect.height;
            float view = _viewport.rect.height;
            float offset = _content.anchoredPosition.y;
            if (top < offset)
                offset = top - 8f;
            else if (bottom > offset + view)
                offset = bottom - view + 8f;
            float max = Mathf.Max(0f, _content.rect.height - view);
            _content.anchoredPosition = new Vector2(0f, Mathf.Clamp(offset, 0f, max));
        }

        void OnDisable()
        {
            if (IsOpen)
                Close();
        }
    }
}
