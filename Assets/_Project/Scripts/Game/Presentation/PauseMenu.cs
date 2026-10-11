using System;
using System.Collections.Generic;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// The pause menu (single-player): resume, options, the controls, and leaving. It shows while <see cref="RunFlow"/>
    /// holds the pause. Esc or Start backs out of a page or resumes. Abandoning a run banks it as a loss.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public enum Page { Main, Options, Controls }

        [SerializeField] RunFlow runFlow;
        [SerializeField, Tooltip("Optional. In a run, leaving counts the run as lost and banks what it earned.")] RunDirector runDirector;
        [SerializeField, Tooltip("Optional. In Survival, leaving banks what was earned so far.")] SurvivalController survival;

        sealed class Row
        {
            public UiRow View;
            public Action Activate;
            public Action<int> Step;
        }

        MenuInput _input;
        InputAction _pause;
        Canvas _canvas;
        Text _title, _hint;
        RectTransform _list, _panel;
        UiScrollList _scroll;

        /// <summary>The list that scrolls the page's rows. For tests.</summary>
        public UiScrollList List => _scroll;
        readonly List<Row> _rows = new List<Row>();
        int _selected;
        int _openedFrame;

        public bool IsOpen { get; private set; }
        public Page CurrentPage { get; private set; }

        void Awake()
        {
            _input = new MenuInput();
            _pause = InputSystem.actions?.FindActionMap("Gameplay")?.FindAction("Pause");
            Build();
            _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            bool paused = runFlow != null && runFlow.IsPaused;
            if (paused != IsOpen)
            {
                IsOpen = paused;
                _canvas.gameObject.SetActive(paused);
                ModalScreens.SetOpen(this, paused);
                if (paused)
                {
                    _openedFrame = Time.frameCount;
                    ShowPage(Page.Main);
                }
            }
            if (!IsOpen || Time.frameCount == _openedFrame) // the key that opened it mustn't also close it
                return;

            var step = _input.ReadStep();
            if (step.y != 0 && _rows.Count > 0)
            {
                _selected = (_selected - step.y + _rows.Count) % _rows.Count;
                _scroll.ScrollTo(_rows[_selected].View.Rect);
            }
            if (step.x != 0 && _selected < _rows.Count)
                _rows[_selected].Step?.Invoke(step.x);

            if (_input.Submitted && _selected < _rows.Count)
                _rows[_selected].Activate?.Invoke();
            else if (_input.Cancelled || (_pause != null && _pause.WasPressedThisFrame()))
                Back();

            if (!IsOpen)
                return;
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].View.SetState(i == _selected, true);
        }

        public void Resume() => runFlow.SetPaused(false);

        /// <summary>The row with this label on the current page, or null. For tests.</summary>
        public UiRow FindRow(string label) => _rows.Find(row => row.View.LabelText == label)?.View;

        /// <summary>Leaves for the Hub. A run in progress is lost, and what it earned is banked.</summary>
        public void Leave()
        {
            runDirector?.Abandon();
            survival?.Abandon();
            runFlow.ReturnToHub();
        }

        /// <summary>Back to the Hub without ending the run: it continues from the galaxy map it was last saved at.</summary>
        public void SaveAndQuit() => runFlow.ReturnToHub();

        public void Back()
        {
            if (CurrentPage == Page.Main)
                Resume();
            else
                ShowPage(Page.Main);
        }

        /// <summary>Opens a page directly. For tests and debug tools.</summary>
        public void ShowPage(Page page)
        {
            CurrentPage = page;
            _selected = 0;
            for (int i = _list.childCount - 1; i >= 0; i--)
                Destroy(_list.GetChild(i).gameObject);
            _list.DetachChildren();
            _list.anchoredPosition = Vector2.zero;
            _rows.Clear();

            switch (page)
            {
                case Page.Main:
                    _title.text = Loc.T("pause.title");
                    AddRow(Loc.T("pause.resume"), null, null, Resume);
                    AddRow(Loc.T("pause.options"), null, Loc.T("pause.options.detail"), () => ShowPage(Page.Options));
                    AddRow(Loc.T("pause.controls"), null, null, () => ShowPage(Page.Controls));
                    // A run can be saved and continued later (M7); abandoning still counts it as lost.
                    if (runDirector != null)
                        AddRow(Loc.T("pause.save_quit"), null, Loc.T("pause.save_quit.detail"), SaveAndQuit);
                    string leave = runDirector != null ? "pause.abandon" : survival != null ? "pause.survival_leave" : "pause.quit";
                    AddRow(Loc.T(leave), null, Loc.T(leave + ".detail"), Leave);
                    _hint.text = Loc.T("pause.hint");
                    break;

                case Page.Options:
                    _title.text = Loc.T("pause.options.title");
                    foreach (var option in SettingsOptions.All)
                    {
                        // Sliders drag, tickboxes toggle; click steps up, right click steps down (playtest 5).
                        var captured = option;
                        var view = OptionRows.Create(_list, option);
                        Register(view, () => ChangeOption(captured, view, captured.Click), step => ChangeOption(captured, view, () => captured.Change(step, false)));
                        view.Pointer.RightClicked += () => ChangeOption(captured, view, captured.RightClick);
                    }
                    AddRow(Loc.T("common.back"), null, null, () => ShowPage(Page.Main));
                    _hint.text = Loc.T("pause.options.hint");
                    break;

                case Page.Controls:
                    _title.text = Loc.T("pause.controls.title");
                    foreach (var (action, keyboard, gamepad) in SettingsOptions.Controls)
                        AddRow(action, keyboard, gamepad, null);
                    AddRow(Loc.T("common.back"), null, null, () => ShowPage(Page.Main));
                    _hint.text = Loc.T("pause.controls.hint");
                    break;
            }
            // Centred, with a margin above and below; a longer page scrolls.
            _scroll.Fit(_panel, UiKit.ReferenceSize.y - 80f);
        }

        /// <summary>Changes an option. A new language rebuilds the page, so every row is in the new language.</summary>
        void ChangeOption(SettingOption option, UiRow view, Action change)
        {
            string language = Loc.Language;
            change();
            if (Loc.Language != language)
            {
                int selected = _selected;
                ShowPage(Page.Options);
                _selected = selected;
            }
            else
            {
                OptionRows.Refresh(view, option);
            }
        }

        UiRow AddRow(string label, string note, string detail, Action activate, Action<int> step = null) =>
            Register(new UiRow(_list, label, note, detail, null, UiKit.Accent), activate, step);

        UiRow Register(UiRow view, Action activate, Action<int> step)
        {
            int index = _rows.Count;
            view.Pointer.Entered += () => _selected = index;
            view.Pointer.Clicked += () => activate?.Invoke();
            _rows.Add(new Row { View = view, Activate = activate, Step = step });
            return view;
        }

        void Build()
        {
            _canvas = UiKit.Canvas("Pause Menu", transform, sortingOrder: 40);
            UiKit.Backdrop(_canvas.transform);
            var panel = UiKit.Image(_canvas.transform, "Panel", UiKit.Panel);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 0f));
            UiKit.Vertical(panel, 14f, new RectOffset(40, 40, 30, 26));
            UiKit.FitContent(panel);
            _title = UiKit.Text(panel.transform, "Title", "PAUSED", 40, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            _panel = panel.rectTransform;
            _scroll = new UiScrollList(panel.transform);
            _list = _scroll.Content;
            _hint = UiKit.Text(panel.transform, "Hint", string.Empty, 17, UiKit.TextDim, TextAnchor.MiddleCenter);
        }

        void OnDisable() => ModalScreens.SetOpen(this, false);
    }
}
