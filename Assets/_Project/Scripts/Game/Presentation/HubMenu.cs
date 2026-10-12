using System.Collections.Generic;
using Pathfinder.Core.Run;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// The Hub (GDD §11.2, §12.1): start a run or Survival (GDD §3.2) after choosing a ship and loadout (GDD §5.4), or spend
    /// Dark Matter and Singularity Cores in the Pathfinder Lab. Locked weapons are skipped in the loadout until bought.
    /// Mouse, keyboard and gamepad all work. Local co-op (M8): on the loadout page, more players join with a controller of
    /// their own, and each picks their ship, weapons and colour (playtest 8).
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
        bool _survival; // the loadout page launches Survival rather than a run

        // Each local player's loadout, as indices into the content lists (M8). The page edits one player's at a time.
        readonly int[] _ships = new int[CoopRules.MaxPilots];
        readonly int[] _primaries = new int[CoopRules.MaxPilots];
        readonly int[] _secondaries = new int[CoopRules.MaxPilots];
        readonly List<string> _loadoutKeys = new List<string>(); // the loadout page's rows
        int _editing;
        // Listening for the controls of a new player, or of one already in (playtest 8: say who plays with what).
        enum Listening { None, Join, Controls }
        Listening _listening;
        int _listeningFor;
        float _listenStarted;
        float _twinsUntil;
        int _twinsFor = -1;
        readonly List<InputDevice> _starters = new List<InputDevice>(); // the controller that chose the row: not the one we want
        readonly Dictionary<InputDevice, int> _lastPress = new Dictionary<InputDevice, int>(); // each controller's latest press (frame)
        const int TwinFrames = 4;       // controllers pressing within this many frames of each other are one controller
        const float ListenDelay = 0.25f; // the press that chose the row, and its echo on a twin device, are not answers

        /// <summary>Which local player's loadout the page shows (M8). For tests.</summary>
        public int EditingPlayer => _editing;

        /// <summary>Whether the page is waiting for a new player to press a button on their controller. For tests.</summary>
        public bool WaitingForPlayer => _listening == Listening.Join;

        /// <summary>Whose controls the page is waiting for (a new player's index when joining), or -1. For tests.</summary>
        public int ListeningFor => _listening == Listening.None ? -1 : _listeningFor;

        /// <summary>The loadout page's rows by name ("pilot", "ship", "launch", "add"…). For tests.</summary>
        public IReadOnlyList<string> LoadoutRows => _loadoutKeys;

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
            LogDevices();
            InputSystem.onDeviceChange += OnDeviceChange;
            _input = new MenuInput();
            Build();
            ShowPage(Page.Main);
            Loc.Changed += OnLanguageChanged;
        }

        void OnDestroy()
        {
            Loc.Changed -= OnLanguageChanged;
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        void OnLanguageChanged() => ShowPage(_page, keepSelection: true);

        void Update()
        {
            TrackPresses();
            if (_listening != Listening.None)
            {
                TickListening();
                _meta.text = MetaLine();
                for (int i = 0; i < _rows.Count; i++)
                    _rows[i].SetState(i == _selected, _enabled[i]);
                return;
            }

            var step = _input.ReadStep();
            if (step.y != 0 && _rows.Count > 0)
            {
                _selected = (_selected - step.y + _rows.Count) % _rows.Count;
                _scroll.ScrollTo(_rows[_selected].Rect); // keep the selection in view on a long page
            }

            if (_page == Page.Loadout && step.x != 0 && IsCycleRow(_selected))
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
            if (_listening != Listening.None)
            {
                StopListening();
                return;
            }
            switch (_page)
            {
                case Page.Main:
                    ChooseMain(index);
                    break;
                case Page.Loadout:
                    switch (LoadoutKey(index))
                    {
                        case "launch":
                            if (_survival)
                                StartSurvival();
                            else
                                StartRun();
                            break;
                        case "add":
                            ListenForJoin();
                            break;
                        case "controls":
                            ListenForControls(_editing);
                            break;
                        case "remove":
                            RemovePlayer(_editing);
                            break;
                        default:
                            Cycle(index, 1);
                            break;
                    }
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
            _editing = 0;
            _listening = Listening.None;
            // Each player starts from what they flew last time this session (M8), within what's unlocked.
            for (int i = 0; i < LocalPlayers.Count; i++)
            {
                var loadout = LocalPlayers.LoadoutFor(i);
                if (!loadout.IsEmpty)
                {
                    _ships[i] = IndexOf(content.Ships, loadout.ShipId, _ships[i]);
                    _primaries[i] = IndexOf(content.PrimaryWeapons, loadout.PrimaryId, _primaries[i]);
                    _secondaries[i] = IndexOf(content.SecondaryWeapons, loadout.SecondaryId, _secondaries[i]);
                }
                _ships[i] = NextAvailable(content.Ships, _ships[i], 0);
                _primaries[i] = NextAvailable(content.PrimaryWeapons, _primaries[i], 0);
                _secondaries[i] = NextAvailable(content.SecondaryWeapons, _secondaries[i], 0);
            }
            ShowPage(Page.Loadout);
            _selected = Mathf.Max(0, _loadoutKeys.IndexOf("launch"));
        }

        /// <summary>Steps a loadout row ("pilot", "paint", "ship", "primary", "secondary") left or right. For tests and debug tools.</summary>
        public void Change(string row, int step)
        {
            int index = _loadoutKeys.IndexOf(row);
            if (_page == Page.Loadout && IsCycleRow(index))
                Cycle(index, step);
        }

        string LoadoutKey(int row) => row >= 0 && row < _loadoutKeys.Count ? _loadoutKeys[row] : string.Empty;

        bool IsCycleRow(int row) => LoadoutKey(row) is "pilot" or "paint" or "ship" or "primary" or "secondary";

        void Cycle(int row, int step)
        {
            switch (LoadoutKey(row))
            {
                case "pilot": _editing = Wrap(_editing + (step < 0 ? -1 : 1), LocalPlayers.Count); break;
                case "paint": LocalPlayers.CyclePaint(_editing, step); break;
                case "ship": _ships[_editing] = NextAvailable(content.Ships, _ships[_editing], step); break;
                case "primary": _primaries[_editing] = NextAvailable(content.PrimaryWeapons, _primaries[_editing], step); break;
                case "secondary": _secondaries[_editing] = NextAvailable(content.SecondaryWeapons, _secondaries[_editing], step); break;
            }
            ShowPage(Page.Loadout, keepSelection: true);
        }

        static int IndexOf<T>(T[] items, string id, int fallback) where T : ContentDefinition
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null && items[i].Id == id)
                    return i;
            }
            return fallback;
        }

        // ---------------------------------------------------------------- local co-op (M8)

        /// <summary>
        /// Waits for a new player to press a button on their controller, or a key for the keyboard and mouse. The controller
        /// that chose the row is ignored: it's someone else's. For tests and debug tools too.
        /// </summary>
        public void ListenForJoin()
        {
            if (LocalPlayers.Count >= CoopRules.MaxPilots)
                return;
            BeginListening(Listening.Join, LocalPlayers.Count);
        }

        /// <summary>Waits for a player to press a button on the controller they'll use, or a key for the keyboard and mouse.</summary>
        public void ListenForControls(int player)
        {
            if (player < 0 || player >= LocalPlayers.Count)
                return;
            BeginListening(Listening.Controls, player);
        }

        void BeginListening(Listening listening, int player)
        {
            _listening = listening;
            _listeningFor = player;
            _listenStarted = Time.unscaledTime;
            // Whatever pressed just now chose the row. A controller that pressed with it is the same controller.
            _starters.Clear();
            if (_input.SubmitDevice is Gamepad pad)
                _starters.Add(pad);
            foreach (var pair in _lastPress)
            {
                if (Time.frameCount - pair.Value <= TwinFrames && !_starters.Contains(pair.Key))
                    _starters.Add(pair.Key);
            }
            ShowPage(Page.Loadout, keepSelection: true);
        }

        void StopListening()
        {
            _listening = Listening.None;
            ShowPage(Page.Loadout, keepSelection: true);
        }

        /// <summary>Remembers when each controller last pressed a button, and catches a new player's controller's twins.</summary>
        void TrackPresses()
        {
            foreach (var pad in Gamepad.all)
            {
                if (!AnyButtonPressed(pad))
                    continue;
                _lastPress[pad] = Time.frameCount;
                // Shortly after a player gets a controller, another device pressing along with it is that controller too.
                if (_twinsFor >= 0 && _twinsFor < LocalPlayers.Count && Time.unscaledTime < _twinsUntil && !LocalPlayers.IsClaimed(pad)
                    && PressedWithAny(pad, LocalPlayers.Slots[_twinsFor].Gamepads))
                    LocalPlayers.AddTwin(_twinsFor, pad);
            }
        }

        bool PressedWithAny(InputDevice pad, IReadOnlyList<InputDevice> others)
        {
            if (!_lastPress.TryGetValue(pad, out int frame))
                return false;
            foreach (var other in others)
            {
                if (other != pad && _lastPress.TryGetValue(other, out int otherFrame) && Mathf.Abs(frame - otherFrame) <= TwinFrames)
                    return true;
            }
            return false;
        }

        void TickListening()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                StopListening();
                return;
            }

            bool settling = Time.unscaledTime - _listenStarted < ListenDelay;
            foreach (var pad in Gamepad.all)
            {
                if (!AnyButtonPressed(pad))
                    continue;
                // The controller that chose the row, or its twin echoing it: Circle/B or Select there stops listening.
                if (settling || _starters.Contains(pad) || PressedWithAny(pad, _starters))
                {
                    if (!_starters.Contains(pad))
                        _starters.Add(pad);
                    if (!settling && (pad.buttonEast.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame))
                    {
                        StopListening();
                        return;
                    }
                    continue;
                }
                AssignController(ControllerOf(pad));
                return;
            }

            if (!settling && keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                AssignKeyboard();
        }

        /// <summary>The pad and any other unclaimed pads that pressed with it: one physical controller.</summary>
        InputDevice[] ControllerOf(Gamepad pad)
        {
            var devices = new List<InputDevice> { pad };
            foreach (var other in Gamepad.all)
            {
                if (other != pad && !LocalPlayers.IsClaimed(other) && !_starters.Contains(other) && PressedWithAny(other, devices))
                    devices.Add(other);
            }
            return devices.ToArray();
        }

        void AssignController(InputDevice[] controller)
        {
            if (_listening == Listening.Join)
                AddPlayer(controller);
            else
            {
                LocalPlayers.Assign(_listeningFor, LocalPlayers.Controls.Gamepad, controller);
                FinishListening(_listeningFor);
            }
        }

        void AssignKeyboard()
        {
            if (_listening == Listening.Join)
                AddKeyboardPlayer();
            else
            {
                LocalPlayers.Assign(_listeningFor, LocalPlayers.Controls.KeyboardMouse);
                FinishListening(_listeningFor);
            }
        }

        void FinishListening(int player)
        {
            _listening = Listening.None;
            _twinsFor = player;
            _twinsUntil = Time.unscaledTime + 1.5f;
            ResolvePlayerOne();
            Audio.GameAudio.Play(Audio.SoundCue.UiConfirm);
            LogControls();
            ShowPage(Page.Loadout, keepSelection: true);
        }

        /// <summary>
        /// With company, player 1 can't keep playing with every device. They take the controller that chose the row, if one
        /// did; otherwise the keyboard and mouse if free; otherwise the controllers nobody else has.
        /// </summary>
        void ResolvePlayerOne()
        {
            if (!LocalPlayers.IsCoop || LocalPlayers.ControlsOf(0) != LocalPlayers.Controls.Any)
                return;
            var starterPads = _starters.FindAll(device => device is Gamepad && !LocalPlayers.IsClaimed(device));
            if (starterPads.Count > 0)
                LocalPlayers.Assign(0, LocalPlayers.Controls.Gamepad, starterPads.ToArray());
            else if (LocalPlayers.OwnerOf(Keyboard.current) < 0)
                LocalPlayers.Assign(0, LocalPlayers.Controls.KeyboardMouse);
        }

        static bool AnyButtonPressed(Gamepad gamepad) =>
            gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame
            || gamepad.buttonNorth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame
            || gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame
            || gamepad.leftTrigger.wasPressedThisFrame || gamepad.rightTrigger.wasPressedThisFrame;

        /// <summary>A new player joins with this controller (all its devices) and starts with player 1's loadout. Returns their index, or -1.</summary>
        public int AddPlayer(params InputDevice[] controller) => Joined(LocalPlayers.Join(controller, StarterPads()));

        /// <summary>A new player joins with the keyboard and mouse. Returns their index, or -1.</summary>
        public int AddKeyboardPlayer() => Joined(LocalPlayers.JoinWithKeyboard(StarterPads()));

        /// <summary>The controller that chose the row (with its twins): player 1's, if they were playing with anything.</summary>
        InputDevice[] StarterPads() => _starters.FindAll(device => device is Gamepad).ToArray();

        int Joined(int index)
        {
            _listening = Listening.None;
            if (index > 0)
            {
                _ships[index] = _ships[0];
                _primaries[index] = _primaries[0];
                _secondaries[index] = _secondaries[0];
                _editing = index;
                FinishListening(index);
                _selected = Mathf.Max(0, _loadoutKeys.IndexOf("pilot"));
            }
            else
            {
                ShowPage(Page.Loadout, keepSelection: true);
            }
            return index;
        }

        /// <summary>Who plays with what, in the player log: helps when a controller shows up as more than one device.</summary>
        static void LogControls()
        {
            var text = new System.Text.StringBuilder("[Controls]");
            for (int i = 0; i < LocalPlayers.Count; i++)
            {
                text.Append($" P{i + 1}={LocalPlayers.ControlsOf(i)}");
                foreach (var device in LocalPlayers.Slots[i].Gamepads)
                    text.Append($" '{device.displayName}' ({device.layout} #{device.deviceId})");
                text.Append(';');
            }
            Debug.Log(text.ToString());
        }

        static void LogDevices()
        {
            var text = new System.Text.StringBuilder("[Input] Devices:");
            foreach (var device in InputSystem.devices)
                text.Append($" '{device.displayName}' ({device.layout} #{device.deviceId});");
            Debug.Log(text.ToString());
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change is InputDeviceChange.Added or InputDeviceChange.Removed or InputDeviceChange.Reconnected or InputDeviceChange.Disconnected)
            {
                LogDevices();
                if (_page == Page.Loadout && _listening == Listening.None)
                    ShowPage(Page.Loadout, keepSelection: true); // a controller's name or "disconnected" changed
            }
        }

        /// <summary>A player other than player 1 leaves; those after them move up.</summary>
        public void RemovePlayer(int index)
        {
            if (index <= 0 || index >= LocalPlayers.Count)
                return;
            LocalPlayers.Leave(index);
            for (int i = index; i < CoopRules.MaxPilots - 1; i++)
            {
                _ships[i] = _ships[i + 1];
                _primaries[i] = _primaries[i + 1];
                _secondaries[i] = _secondaries[i + 1];
            }
            _editing = 0;
            ShowPage(Page.Loadout, keepSelection: true);
            _selected = Mathf.Max(0, _loadoutKeys.IndexOf("launch"));
        }

        PilotLoadout LoadoutOf(int player) =>
            new PilotLoadout(content.Ships[_ships[player]].Id, content.PrimaryWeapons[_primaries[player]].Id, content.SecondaryWeapons[_secondaries[player]].Id);

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
            // The run seed also seeds the arena, so the whole run replays from a single code. Every local player's pick is
            // kept for the spawner, and for next time (M8); player 1's is the run's own.
            for (int i = 0; i < LocalPlayers.Count; i++)
                LocalPlayers.SetLoadout(i, LoadoutOf(i));
            var run = RunSession.Begin(loadout: LoadoutOf(0));
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
                    _editing = Mathf.Clamp(_editing, 0, LocalPlayers.Count - 1);
                    _loadoutKeys.Clear();
                    bool coop = LocalPlayers.IsCoop;
                    var pilotColor = CoopRules.PilotColor(_editing);
                    if (coop)
                    {
                        // Whose loadout the rows below show (M8): step through the players.
                        _loadoutKeys.Add("pilot");
                        AddRow(Loc.T("hub.coop.pilot", ("player", PilotLabels.Name(_editing))), null,
                            Loc.T("hub.coop.pilot.detail", ("count", LocalPlayers.Count)), pilotColor);
                        // What they play with (playtest 8): choose, then press a button on it.
                        _loadoutKeys.Add("controls");
                        bool listening = _listening == Listening.Controls && _listeningFor == _editing;
                        AddRow(listening ? Loc.T("hub.coop.press", ("player", PilotLabels.Name(_editing)))
                                : Loc.T("hub.coop.controls", ("controls", LocalPlayers.DescribeControls(_editing))), null,
                            Loc.T(listening ? "hub.coop.press.detail" : "hub.coop.controls.detail", ("player", PilotLabels.Name(_editing))),
                            listening ? pilotColor : (Color?)null);
                    }
                    var ship = content.Ships[_ships[_editing]];
                    var primary = content.PrimaryWeapons[_primaries[_editing]];
                    var secondary = content.SecondaryWeapons[_secondaries[_editing]];
                    _loadoutKeys.Add("ship");
                    AddRow(Loc.T("hub.loadout.ship", ("name", ship.DisplayName)), null, Loc.T("hub.loadout.ship.detail",
                        ("hull", ship.MaxHull), ("shield", ship.MaxShield), ("speed", ship.MoveSpeed), ("trait", ship.TraitTitle), ("description", ship.TraitText)))
                        .SetIcon(ship.Icon, Color.Lerp(new Color(0.55f, 0.65f, 0.9f), Color.white, 0.2f) * ship.HullTint, 56f);
                    _loadoutKeys.Add("primary");
                    AddRow(Loc.T("hub.loadout.primary", ("name", primary.DisplayName)), null, PrimarySummary(primary)).SetIcon(primary.Icon, WeaponColor, 56f);
                    _loadoutKeys.Add("secondary");
                    AddRow(Loc.T("hub.loadout.secondary", ("name", secondary.DisplayName)), null, Loc.T("hub.loadout.secondary.detail", ("seconds", secondary.Cooldown)))
                        .SetIcon(secondary.Icon, WeaponColor, 56f);
                    // Their ship's colour (playtest 8: telling the players apart). Nobody else's: those are skipped.
                    var paint = CoopRules.Paint(_editing);
                    _loadoutKeys.Add("paint");
                    AddRow(Loc.T("hub.paint", ("name", Loc.T(paint.Key))), null, Loc.T(coop ? "hub.paint.detail_coop" : "hub.paint.detail"), paint.Ui)
                        .SetIcon(ship.Icon, paint.Ui, 56f);
                    _loadoutKeys.Add("launch");
                    AddRow(Loc.T(_survival ? "hub.loadout.launch_survival" : "hub.loadout.launch"), null,
                        coop ? Loc.T("hub.coop.launch.detail", ("count", LocalPlayers.Count)) : null, UiKit.Accent);
                    if (LocalPlayers.Count < CoopRules.MaxPilots)
                    {
                        _loadoutKeys.Add("add");
                        bool joining = _listening == Listening.Join;
                        bool canJoin = FreeControllers() > 0 || LocalPlayers.OwnerOf(Keyboard.current) < 0;
                        AddRow(joining ? Loc.T("hub.coop.press", ("player", PilotLabels.Name(LocalPlayers.Count)))
                                : Loc.T("hub.coop.add", ("player", PilotLabels.Name(LocalPlayers.Count))), null,
                            Loc.T(joining ? "hub.coop.press.detail" : canJoin ? "hub.coop.add.detail" : "hub.coop.add.no_controller",
                                ("player", PilotLabels.Name(LocalPlayers.Count))),
                            joining ? CoopRules.PilotColor(LocalPlayers.Count) : (Color?)null);
                    }
                    if (coop && _editing > 0)
                    {
                        _loadoutKeys.Add("remove");
                        AddRow(Loc.T("hub.coop.remove", ("player", PilotLabels.Name(_editing))), null, null, UiKit.Bad);
                    }
                    _hint.text = Loc.T(_listening != Listening.None ? "hub.coop.press.hint" : "hub.loadout.hint");
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
            string detail = Loc.T("hub.continue.detail", ("galaxy", save.galaxy + 1), ("galaxyName", Loc.T($"galaxy.{Mathf.Clamp(save.galaxy, 0, 2)}")),
                ("levels", save.levelsCleared), ("ship", ship != null ? ship.DisplayName : save.pilot.ship));
            // A co-op run (M8) says how many flew it: players who haven't joined this time start fresh, or sit it out.
            return save.pilots.Count > 1 ? detail + "\n" + Loc.T("hub.continue.players", ("count", save.pilots.Count), ("joined", LocalPlayers.Count)) : detail;
        }

        /// <summary>Controllers connected that no player has claimed yet.</summary>
        static int FreeControllers()
        {
            int free = 0;
            foreach (var gamepad in Gamepad.all)
            {
                if (!LocalPlayers.IsClaimed(gamepad))
                    free++;
            }
            return free;
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
