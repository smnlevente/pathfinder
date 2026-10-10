using System;
using System.Collections.Generic;
using Pathfinder.Core.Equipment;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Placeholder IMGUI equipment choice (GDD §8.1, §12.2): a row of cards, plus a swap step when the category is full.
    /// Gameplay is paused while it is open. Skipping grants Scraps instead.
    /// </summary>
    public sealed class EquipmentChoiceScreen : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        enum Step { Pick, Swap }

        IReadOnlyList<EquipmentDefinition> _options = Array.Empty<EquipmentDefinition>();
        PlayerShip _ship;
        Action<bool> _onClosed;
        string _title;
        int _skipScraps;
        bool _allowSkip;
        Step _step;
        EquipmentDefinition _pending;
        int _selected;
        MenuInput _input;
        GUIStyle _title28, _name, _body, _small, _button;

        public bool IsOpen { get; private set; }
        public IReadOnlyList<EquipmentDefinition> Options => _options;
        public bool IsSwapping => IsOpen && _step == Step.Swap;

        void Awake() => _input = new MenuInput();

        /// <summary>Offers a choice of items. <paramref name="onClosed"/> receives true if an item was taken.</summary>
        public void Show(string title, IReadOnlyList<EquipmentDefinition> options, PlayerShip ship, int skipScraps, Action<bool> onClosed)
        {
            _title = title;
            _options = options;
            _ship = ship;
            _skipScraps = skipScraps;
            _allowSkip = true;
            _onClosed = onClosed;
            _step = Step.Pick;
            _selected = 0;
            Open();
        }

        /// <summary>Goes straight to the swap step for one item, e.g. a Shop purchase into a full category.</summary>
        public void ShowSwap(EquipmentDefinition item, PlayerShip ship, Action<bool> onClosed)
        {
            _title = "REPLACE WHICH ITEM?";
            _options = new[] { item };
            _ship = ship;
            _allowSkip = false;
            _onClosed = onClosed;
            _pending = item;
            _step = Step.Swap;
            _selected = 0;
            Open();
        }

        /// <summary>Takes option <paramref name="index"/>. If its category is full, this moves to the swap step.</summary>
        public void Choose(int index)
        {
            if (!IsOpen || _step != Step.Pick || index < 0 || index >= _options.Count)
                return;

            var item = _options[index];
            if (_ship.PreviewEquip(item) == EquipOutcome.NeedsSwap)
            {
                _pending = item;
                _step = Step.Swap;
                _selected = 0;
                return;
            }

            _ship.Equip(item);
            Close(true);
        }

        /// <summary>Replaces the item in slot <paramref name="slotIndex"/> of the pending item's category.</summary>
        public void Swap(int slotIndex)
        {
            if (!IsSwapping)
                return;

            _ship.ReplaceEquipment(slotIndex, _pending);
            Close(true);
        }

        /// <summary>Leaves the swap step. A Shop swap is cancelled outright, a cache swap returns to the cards.</summary>
        public void Back()
        {
            if (!IsSwapping)
                return;
            if (!_allowSkip)
            {
                Close(false);
                return;
            }
            _step = Step.Pick;
            _selected = 0;
        }

        public void Skip()
        {
            if (!IsOpen || !_allowSkip)
                return;
            _ship.AddScraps(_skipScraps);
            Close(false);
        }

        void Open()
        {
            IsOpen = true;
            GameClock.Pause(this);
        }

        void Close(bool took)
        {
            IsOpen = false;
            GameClock.Resume(this);
            var callback = _onClosed;
            _onClosed = null;
            callback?.Invoke(took);
        }

        void Update()
        {
            if (!IsOpen)
                return;

            var step = _input.ReadStep();
            if (_step == Step.Pick)
            {
                int count = _options.Count + (_allowSkip ? 1 : 0);
                if (step.x != 0)
                    _selected = (_selected + step.x + count) % count;
                if (_input.Submitted)
                {
                    if (_selected < _options.Count)
                        Choose(_selected);
                    else
                        Skip();
                }
            }
            else
            {
                int count = _ship.Equipment.In(_pending.Category).Count + 1;
                if (step.y != 0)
                    _selected = (_selected - step.y + count) % count;
                if (_input.Submitted)
                {
                    if (_selected < count - 1)
                        Swap(_selected);
                    else
                        Back();
                }
                else if (_input.Cancelled)
                {
                    Back();
                }
            }
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

            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0f, 0f, width, ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (_step == Step.Pick)
                DrawCards(width);
            else
                DrawSwap(width);
        }

        void DrawCards(float width)
        {
            GUI.Label(new Rect(0f, 150f, width, 50f), _title, _title28);

            const float cardWidth = 340f, cardHeight = 420f, gap = 30f;
            float total = _options.Count * cardWidth + (_options.Count - 1) * gap;
            float x = (width - total) * 0.5f;

            for (int i = 0; i < _options.Count; i++)
            {
                var item = _options[i];
                var rect = new Rect(x + i * (cardWidth + gap), 240f, cardWidth, cardHeight);
                bool selected = _selected == i;

                GUI.color = selected ? new Color(0.25f, 0.35f, 0.45f, 0.95f) : new Color(0.1f, 0.12f, 0.16f, 0.95f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = RarityColor(item.Rarity);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 8f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                var outcome = _ship.PreviewEquip(item);
                var existing = _ship.Equipment.Find(item);
                int level = existing != null ? existing.Level + 1 : 1;
                string status = outcome == EquipOutcome.LeveledUp ? $"LEVEL {Roman(existing.Level)} → {Roman(level)}"
                    : outcome == EquipOutcome.NeedsSwap ? $"NEW · {item.Category.ToString().ToUpperInvariant()} SLOTS FULL: SWAP"
                    : "NEW";

                GUI.Label(new Rect(rect.x + 20f, rect.y + 24f, rect.width - 40f, 40f), item.DisplayName, _name);
                GUI.Label(new Rect(rect.x + 20f, rect.y + 66f, rect.width - 40f, 26f), $"{item.Rarity.ToString().ToUpperInvariant()} {CategoryName(item.Category)}", _small);
                GUI.Label(new Rect(rect.x + 20f, rect.y + 96f, rect.width - 40f, 26f), status, _small);
                GUI.Label(new Rect(rect.x + 20f, rect.y + 140f, rect.width - 40f, 220f), item.Describe(level), _body);

                if (GUI.Button(new Rect(rect.x + 30f, rect.yMax - 70f, rect.width - 60f, 50f), "TAKE", _button))
                {
                    _selected = i;
                    Choose(i);
                    return;
                }
                if (rect.Contains(Event.current.mousePosition))
                    _selected = i;
            }

            if (_allowSkip)
            {
                var skipRect = new Rect(width * 0.5f - 200f, 700f, 400f, 54f);
                GUI.color = _selected == _options.Count ? new Color(0.4f, 0.9f, 1f) : Color.white;
                if (GUI.Button(skipRect, $"SKIP (+{_skipScraps} SCRAPS)", _button))
                    Skip();
                GUI.color = Color.white;
            }
        }

        void DrawSwap(float width)
        {
            GUI.Label(new Rect(0f, 200f, width, 50f), $"REPLACE WHICH {CategoryName(_pending.Category).ToUpperInvariant()}?", _title28);
            GUI.Label(new Rect(0f, 250f, width, 30f), $"{_pending.DisplayName}: {_pending.Describe(1)}   ·   The replaced item is lost.", _small);

            var equipped = _ship.Equipment.In(_pending.Category);
            for (int i = 0; i <= equipped.Count; i++)
            {
                var rect = new Rect(width * 0.5f - 320f, 320f + i * 70f, 640f, 56f);
                bool back = i == equipped.Count;
                string label = back ? (_allowSkip ? "BACK" : "CANCEL")
                    : $"{equipped[i].Item.DisplayName} {Roman(equipped[i].Level)}:  {equipped[i].Item.Describe(equipped[i].Level)}";
                GUI.color = _selected == i ? new Color(0.4f, 0.9f, 1f) : Color.white;
                if (GUI.Button(rect, label, _button))
                {
                    if (back)
                        Back();
                    else
                        Swap(i);
                    GUI.color = Color.white;
                    return;
                }
                if (rect.Contains(Event.current.mousePosition))
                    _selected = i;
            }
            GUI.color = Color.white;
        }

        public static string Roman(int level) => level switch { 1 => "I", 2 => "II", 3 => "III", _ => level.ToString() };

        public static string CategoryName(EquipmentCategory category) => category switch
        {
            EquipmentCategory.Dash => "Dash Module",
            EquipmentCategory.Weapon => "Weapon Mod",
            EquipmentCategory.Secondary => "Secondary Mod",
            _ => "Hull System",
        };

        public static Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Rare => new Color(0.35f, 0.65f, 1f),
            Rarity.Epic => new Color(1f, 0.75f, 0.2f),
            _ => new Color(0.8f, 0.82f, 0.86f),
        };

        void EnsureStyles()
        {
            if (_title28 != null)
                return;

            _title28 = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _title28.normal.textColor = Color.white;
            _name = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = true };
            _name.normal.textColor = Color.white;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            _body.normal.textColor = new Color(0.85f, 0.92f, 1f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _small.normal.textColor = new Color(0.7f, 0.8f, 0.9f);
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
        }

        void OnDisable()
        {
            if (IsOpen)
            {
                IsOpen = false;
                GameClock.Resume(this);
            }
        }
    }
}
