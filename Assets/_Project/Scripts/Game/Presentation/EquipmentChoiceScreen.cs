using System;
using System.Collections.Generic;
using Pathfinder.Core.Equipment;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Equipment choice (GDD §8.1, §12.2): a row of cards, plus a swap step when the item's category is full. Gameplay is
    /// paused while it is open. Skipping grants Scraps instead.
    /// </summary>
    public sealed class EquipmentChoiceScreen : MonoBehaviour
    {
        const float CardWidth = 390f;

        enum Step { Pick, Swap }

        sealed class Card
        {
            public Image Background;
            public Outline Frame;
            public Image Take;
        }

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

        Canvas _canvas;
        RectTransform _pickRoot, _cards, _swapRoot, _swapList;
        Text _pickTitle, _swapTitle, _swapSubtitle;
        UiRow _skipRow;
        readonly List<Card> _cardViews = new List<Card>();
        readonly List<UiRow> _swapRows = new List<UiRow>();

        public bool IsOpen { get; private set; }
        public IReadOnlyList<EquipmentDefinition> Options => _options;
        public bool IsSwapping => IsOpen && _step == Step.Swap;

        void Awake()
        {
            _input = new MenuInput();
            Build();
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Offers a choice of items. <paramref name="onClosed"/> receives true if an item was taken.</summary>
        public void Show(string title, IReadOnlyList<EquipmentDefinition> options, PlayerShip ship, int skipScraps, Action<bool> onClosed)
        {
            _title = title;
            _options = options;
            _ship = ship;
            _skipScraps = skipScraps;
            _allowSkip = true;
            _onClosed = onClosed;
            Open();
            EnterPick();
        }

        /// <summary>Goes straight to the swap step for one item, e.g. a Shop purchase into a full category.</summary>
        public void ShowSwap(EquipmentDefinition item, PlayerShip ship, Action<bool> onClosed)
        {
            _title = Loc.T("choice.replace_item");
            _options = new[] { item };
            _ship = ship;
            _allowSkip = false;
            _onClosed = onClosed;
            Open();
            EnterSwap(item);
        }

        /// <summary>Takes option <paramref name="index"/>. If its category is full, this moves to the swap step.</summary>
        public void Choose(int index)
        {
            if (!IsOpen || _step != Step.Pick || index < 0 || index >= _options.Count)
                return;

            var item = _options[index];
            if (_ship.PreviewEquip(item) == EquipOutcome.NeedsSwap)
            {
                EnterSwap(item);
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
            EnterPick();
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
            ModalScreens.SetOpen(this, true);
            _canvas.gameObject.SetActive(true);
        }

        void Close(bool took)
        {
            IsOpen = false;
            GameClock.Resume(this);
            ModalScreens.SetOpen(this, false);
            _canvas.gameObject.SetActive(false);
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
                else if (step.y < 0 && _allowSkip)
                    _selected = _options.Count;
                else if (step.y > 0 && _selected == _options.Count)
                    _selected = 0;

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
                int count = _swapRows.Count;
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

            if (IsOpen)
                RefreshSelection();
        }

        // ---------------------------------------------------------------- view

        void Build()
        {
            _canvas = UiKit.Canvas("Equipment Choice", transform, sortingOrder: 30);
            UiKit.Backdrop(_canvas.transform);

            // Pick: a title, a row of cards and a skip button.
            _pickRoot = UiKit.Stretch(UiKit.Node("Pick", _canvas.transform));
            _pickTitle = UiKit.Text(_pickRoot, "Title", string.Empty, 44, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_pickTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1600f, 60f));

            _cards = UiKit.Node("Cards", _pickRoot);
            UiKit.Place(_cards, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1800f, 0f));
            var cardLayout = UiKit.Horizontal(_cards, 32f, null, TextAnchor.UpperCenter);
            cardLayout.childForceExpandHeight = true;
            UiKit.FitContent(_cards);

            var skipHolder = UiKit.Node("Skip", _pickRoot);
            UiKit.Place(skipHolder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(520f, 0f));
            UiKit.Vertical(skipHolder, 0f);
            UiKit.FitContent(skipHolder);
            _skipRow = new UiRow(skipHolder, string.Empty, string.Empty, null, null, UiKit.Gold);
            _skipRow.Pointer.Entered += () => _selected = _options.Count;
            _skipRow.Pointer.Clicked += Skip;

            // Swap: a centred panel listing the equipped items in the category.
            var swapPanel = UiKit.Image(_canvas.transform, "Swap", UiKit.Panel);
            _swapRoot = UiKit.Place(swapPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 0f));
            UiKit.Vertical(swapPanel, 14f, new RectOffset(44, 44, 34, 30));
            UiKit.FitContent(swapPanel);
            _swapTitle = UiKit.Text(_swapRoot, "Title", string.Empty, 38, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            _swapSubtitle = UiKit.Text(_swapRoot, "Subtitle", string.Empty, 21, UiKit.TextBody, TextAnchor.UpperCenter);
            _swapList = UiKit.Node("List", _swapRoot);
            UiKit.Vertical(_swapList, 8f);
            UiKit.Text(_swapRoot, "Hint", Loc.T("choice.hint"), 17, UiKit.TextDim, TextAnchor.MiddleCenter);
        }

        void EnterPick()
        {
            _step = Step.Pick;
            _selected = 0;
            _pickRoot.gameObject.SetActive(true);
            _swapRoot.gameObject.SetActive(false);
            _pickTitle.text = _title;

            for (int i = _cards.childCount - 1; i >= 0; i--)
                Destroy(_cards.GetChild(i).gameObject);
            _cards.DetachChildren();
            _cardViews.Clear();
            for (int i = 0; i < _options.Count; i++)
                _cardViews.Add(BuildCard(_options[i], i));

            _skipRow.Rect.parent.gameObject.SetActive(_allowSkip);
            if (_allowSkip)
                _skipRow.SetLabel(Loc.T("choice.skip", ("amount", _skipScraps)));
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cards);
            RefreshSelection();
        }

        Card BuildCard(EquipmentDefinition item, int index)
        {
            var background = UiKit.Image(_cards, "Card", UiKit.Row, raycast: true);
            UiKit.Layout(background, preferredWidth: CardWidth, minHeight: 470f);
            UiKit.Vertical(background, 10f, new RectOffset(28, 28, 30, 24), TextAnchor.UpperLeft);
            var frame = background.gameObject.AddComponent<Outline>();
            frame.effectColor = UiKit.Frame;
            frame.effectDistance = new Vector2(3f, -3f);

            var stripe = UiKit.Image(background.transform, "Rarity", RarityColor(item.Rarity));
            UiKit.Layout(stripe).ignoreLayout = true;
            var stripeRect = stripe.rectTransform;
            stripeRect.anchorMin = new Vector2(0f, 1f);
            stripeRect.anchorMax = Vector2.one;
            stripeRect.pivot = new Vector2(0.5f, 1f);
            stripeRect.sizeDelta = new Vector2(0f, 9f);
            stripeRect.anchoredPosition = Vector2.zero;

            var outcome = _ship.PreviewEquip(item);
            var existing = _ship.Equipment.Find(item);
            int level = existing != null ? existing.Level + 1 : 1;
            string status = outcome == EquipOutcome.LeveledUp ? Loc.T("choice.level_up", ("from", Roman(existing.Level)), ("to", Roman(level)))
                : outcome == EquipOutcome.NeedsSwap ? Loc.T("choice.slots_full", ("category", CategoryName(item.Category).ToUpperInvariant()))
                : Loc.T("choice.new");

            UiKit.Text(background.transform, "Name", item.DisplayName, 30, UiKit.TextBright, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(background.transform, "Kind", Loc.T("choice.kind", ("rarity", RarityName(item.Rarity).ToUpperInvariant()), ("category", CategoryName(item.Category).ToUpperInvariant())),
                18, RarityColor(item.Rarity), TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(background.transform, "Status", status, 18, outcome == EquipOutcome.NeedsSwap ? UiKit.Gold : UiKit.Accent, TextAnchor.UpperLeft);
            var description = UiKit.Text(background.transform, "Description", item.Describe(level), 22, UiKit.TextBody);
            UiKit.Layout(description, flexibleWidth: 1f).flexibleHeight = 1f;

            var take = UiKit.Image(background.transform, "Take", UiKit.RowSelected);
            UiKit.Layout(take, preferredHeight: 56f);
            var takeLabel = UiKit.Text(take.transform, "Label", Loc.T("choice.take"), 24, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(takeLabel.rectTransform);

            var pointer = background.gameObject.AddComponent<UiPointer>();
            pointer.Entered += () => _selected = index;
            pointer.Clicked += () => Choose(index);
            return new Card { Background = background, Frame = frame, Take = take };
        }

        void EnterSwap(EquipmentDefinition item)
        {
            _step = Step.Swap;
            _pending = item;
            _selected = 0;
            _pickRoot.gameObject.SetActive(false);
            _swapRoot.gameObject.SetActive(true);
            _swapTitle.text = Loc.T("choice.replace", ("category", CategoryName(item.Category).ToUpperInvariant()));
            _swapSubtitle.text = $"{UiKit.Colored(item.DisplayName, RarityColor(item.Rarity))}: {item.Describe(1)}\n" +
                UiKit.Colored(Loc.T("choice.replaced_lost"), UiKit.TextDim);

            for (int i = _swapList.childCount - 1; i >= 0; i--)
                Destroy(_swapList.GetChild(i).gameObject);
            _swapList.DetachChildren();
            _swapRows.Clear();

            var equipped = _ship.Equipment.In(item.Category);
            for (int i = 0; i <= equipped.Count; i++)
            {
                bool back = i == equipped.Count;
                var row = back
                    ? new UiRow(_swapList, Loc.T(_allowSkip ? "common.back" : "common.cancel"))
                    : new UiRow(_swapList, $"{equipped[i].Item.DisplayName} {Roman(equipped[i].Level)}", null,
                        equipped[i].Item.Describe(equipped[i].Level), RarityColor(equipped[i].Item.Rarity));
                int index = i;
                row.Pointer.Entered += () => _selected = index;
                row.Pointer.Clicked += () =>
                {
                    if (back)
                        Back();
                    else
                        Swap(index);
                };
                _swapRows.Add(row);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_swapRoot);
            RefreshSelection();
        }

        void RefreshSelection()
        {
            if (_step == Step.Pick)
            {
                for (int i = 0; i < _cardViews.Count; i++)
                {
                    bool selected = i == _selected;
                    _cardViews[i].Background.color = selected ? new Color(0.09f, 0.15f, 0.21f, 1f) : UiKit.Row;
                    _cardViews[i].Frame.enabled = selected;
                    _cardViews[i].Take.color = selected ? new Color(0.16f, 0.45f, 0.58f, 1f) : new Color(0.1f, 0.16f, 0.22f, 1f);
                }
                _skipRow.SetState(_selected == _options.Count, true);
            }
            else
            {
                for (int i = 0; i < _swapRows.Count; i++)
                    _swapRows[i].SetState(i == _selected, true);
            }
        }

        public static string Roman(int level) => level switch { 1 => "I", 2 => "II", 3 => "III", _ => level.ToString() };

        public static string CategoryName(EquipmentCategory category) => Loc.T($"category.{category.ToString().ToLowerInvariant()}");

        public static string RarityName(Rarity rarity) => Loc.T($"rarity.{rarity.ToString().ToLowerInvariant()}");

        public static Color RarityColor(Rarity rarity) => rarity switch
        {
            Rarity.Rare => new Color(0.35f, 0.65f, 1f),
            Rarity.Epic => new Color(1f, 0.75f, 0.2f),
            _ => new Color(0.8f, 0.82f, 0.86f),
        };

        void OnDisable()
        {
            if (IsOpen)
            {
                IsOpen = false;
                GameClock.Resume(this);
                ModalScreens.SetOpen(this, false);
            }
        }
    }
}
