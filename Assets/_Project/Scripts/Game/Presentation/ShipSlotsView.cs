using System.Collections.Generic;
using Pathfinder.Core.Equipment;
using Pathfinder.Game.Equipment;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>What a slot on the ship diagram is showing.</summary>
    public enum SlotState { Empty, Filled, Target, LevelUp, Swap, Replace, Maxed }

    /// <summary>
    /// The pilot's equipment slots drawn around their ship (playtest 7: "I have no idea how many slots of which things I
    /// have"): weapon mods at the nose, the secondary mod on the left wing, hull systems down the right side, the dash
    /// module at the engine. A filled slot shows its item's icon and level, an empty one a faint plus. While a menu points at
    /// an item (<see cref="EquipmentPreview"/>), the diagram shows where it would go: a free slot pulses green with the item
    /// in it, the item it would level up pulses green, a full category pulses gold (a swap), and in the swap step the slot
    /// that would be replaced pulses red. Each category keeps the colour its part of the ship has elsewhere in the HUD.
    /// </summary>
    public sealed class ShipSlotsView
    {
        const float SlotSize = 50f;
        const float ShipSize = 130f;
        const float Reach = 130f; // how far the side groups sit from the ship's centre

        public static readonly Color WeaponColor = new Color(0.55f, 0.85f, 1f);
        public static readonly Color SecondaryColor = new Color(1f, 0.75f, 0.35f);
        public static readonly Color DashColor = new Color(0.4f, 1f, 0.9f);
        public static readonly Color HullColor = new Color(1f, 0.45f, 0.4f);
        static readonly Color Good = new Color(0.45f, 1f, 0.6f);

        static readonly EquipmentCategory[] Categories = { EquipmentCategory.Weapon, EquipmentCategory.Secondary, EquipmentCategory.Hull, EquipmentCategory.Dash };

        sealed class Slot
        {
            public Image Ring, Icon, Ghost;
            public Text Level, Plus;
            public SlotState State;
        }

        sealed class Group
        {
            public CanvasGroup Fade;
            public Text Label;
            public readonly List<Slot> Slots = new List<Slot>();
        }

        readonly RectTransform _root;
        readonly Dictionary<EquipmentCategory, Group> _groups = new Dictionary<EquipmentCategory, Group>();

        public RectTransform Root => _root;

        public static Color CategoryColor(EquipmentCategory category) => category switch
        {
            EquipmentCategory.Weapon => WeaponColor,
            EquipmentCategory.Secondary => SecondaryColor,
            EquipmentCategory.Dash => DashColor,
            _ => HullColor,
        };

        public ShipSlotsView(RectTransform root)
        {
            _root = root;
            var ship = UiKit.Image(root, "Ship", Color.white, UiKit.Ship);
            ship.preserveAspect = true;
            Centre(ship.rectTransform, Vector2.zero, ShipSize);
        }

        /// <summary>How many slots a category shows, and what one of them is showing. For tests.</summary>
        public int SlotCount(EquipmentCategory category) => _groups.TryGetValue(category, out var group) ? group.Slots.Count : 0;
        public SlotState StateOf(EquipmentCategory category, int index) => _groups[category].Slots[index].State;

        public void Refresh(PlayerShip ship, EquipmentDefinition preview, int replaceIndex)
        {
            var outcome = preview != null ? ship.PreviewEquip(preview) : EquipOutcome.Equipped;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            foreach (var category in Categories)
            {
                var group = GroupFor(category, ship.Equipment.Capacity(category));
                var entries = ship.Equipment.In(category);
                bool previewHere = preview != null && preview.Category == category;
                group.Fade.alpha = preview == null || previewHere ? 1f : 0.35f;
                for (int i = 0; i < group.Slots.Count; i++)
                {
                    var entry = i < entries.Count ? entries[i] : null;
                    var state = entry != null ? SlotState.Filled : SlotState.Empty;
                    if (previewHere)
                    {
                        if (outcome == EquipOutcome.Equipped && i == entries.Count)
                            state = SlotState.Target;
                        else if (entry != null && entry.Item == preview)
                            state = outcome == EquipOutcome.AlreadyMaxed ? SlotState.Maxed : SlotState.LevelUp;
                        else if (outcome == EquipOutcome.NeedsSwap && entry != null)
                            state = replaceIndex < 0 ? SlotState.Swap : i == replaceIndex ? SlotState.Replace : SlotState.Filled;
                    }
                    Show(group.Slots[i], category, entry, state, preview, pulse);
                }
            }
        }

        void Show(Slot slot, EquipmentCategory category, EquipmentSlots<EquipmentDefinition>.Entry entry, SlotState state, EquipmentDefinition preview, float pulse)
        {
            slot.State = state;
            var color = CategoryColor(category);
            slot.Ring.color = state switch
            {
                SlotState.Target or SlotState.LevelUp => Color.Lerp(color, Good, 0.4f + 0.6f * pulse),
                SlotState.Swap => Color.Lerp(color, UiKit.Gold, 0.4f + 0.6f * pulse),
                SlotState.Replace or SlotState.Maxed => Color.Lerp(color, UiKit.Bad, state == SlotState.Maxed ? 1f : 0.4f + 0.6f * pulse),
                SlotState.Empty => new Color(color.r, color.g, color.b, 0.4f),
                _ => color,
            };
            float grow = state is SlotState.Target or SlotState.LevelUp or SlotState.Swap or SlotState.Replace ? 1f + 0.12f * pulse : 1f;
            slot.Ring.rectTransform.localScale = new Vector3(grow, grow, 1f);

            bool filled = entry != null;
            slot.Icon.enabled = filled;
            if (filled)
            {
                slot.Icon.sprite = entry.Item.Icon;
                var tint = Color.Lerp(EquipmentChoiceScreen.RarityColor(entry.Item.Rarity), Color.white, 0.25f);
                slot.Icon.color = state == SlotState.Replace ? new Color(tint.r, tint.g, tint.b, 0.3f) : tint;
                slot.Level.text = state == SlotState.LevelUp ? EquipmentChoiceScreen.Roman(entry.Level + 1) : EquipmentChoiceScreen.Roman(entry.Level);
                slot.Level.color = state == SlotState.LevelUp ? Good : Color.white;
            }
            slot.Level.enabled = filled;

            // The item being considered, shown faintly where it would go.
            bool ghost = state is SlotState.Target or SlotState.Replace && preview != null && preview.Icon != null;
            slot.Ghost.enabled = ghost;
            if (ghost)
            {
                slot.Ghost.sprite = preview.Icon;
                var tint = EquipmentChoiceScreen.RarityColor(preview.Rarity);
                slot.Ghost.color = new Color(tint.r, tint.g, tint.b, 0.45f + 0.4f * pulse);
            }
            slot.Plus.enabled = !filled && !ghost;
            slot.Plus.color = new Color(color.r, color.g, color.b, 0.55f);
        }

        Group GroupFor(EquipmentCategory category, int capacity)
        {
            if (_groups.TryGetValue(category, out var group) && group.Slots.Count == capacity)
                return group;
            if (group != null)
                Object.Destroy(group.Fade.gameObject);

            var node = UiKit.Node(category.ToString(), _root);
            UiKit.Stretch(node);
            group = new Group { Fade = node.gameObject.AddComponent<CanvasGroup>() };
            group.Fade.interactable = group.Fade.blocksRaycasts = false;
            var color = CategoryColor(category);
            for (int i = 0; i < capacity; i++)
                group.Slots.Add(BuildSlot(node, SlotPosition(category, i, capacity), color));

            var (labelPosition, anchor) = LabelPlace(category, capacity);
            group.Label = UiKit.Text(node, "Label", EquipmentChoiceScreen.CategoryName(category).ToUpperInvariant(), 16, color, anchor, FontStyle.Bold);
            group.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Centre(group.Label.rectTransform, labelPosition, 0f);
            group.Label.rectTransform.sizeDelta = new Vector2(170f, 22f);
            group.Label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            _groups[category] = group;
            return group;
        }

        /// <summary>Where a slot sits around the ship (the ship is at the centre, nose up).</summary>
        static Vector2 SlotPosition(EquipmentCategory category, int index, int capacity)
        {
            float spread = SlotSize + 10f;
            float along = (index - (capacity - 1) * 0.5f) * spread;
            return category switch
            {
                EquipmentCategory.Weapon => new Vector2(along, Reach * 0.82f),       // a row across the nose
                EquipmentCategory.Secondary => new Vector2(-Reach, 6f - along),      // on the left wing
                EquipmentCategory.Dash => new Vector2(along, -Reach * 0.82f),        // at the engine
                _ => new Vector2(Reach, 6f - along),                                 // down the right side
            };
        }

        static (Vector2 position, TextAnchor anchor) LabelPlace(EquipmentCategory category, int capacity)
        {
            float half = (capacity - 1) * 0.5f * (SlotSize + 10f);
            float gap = SlotSize * 0.5f + 14f;
            return category switch
            {
                EquipmentCategory.Weapon => (new Vector2(0f, Reach * 0.82f + gap), TextAnchor.MiddleCenter),
                EquipmentCategory.Secondary => (new Vector2(-Reach, 6f - half - gap), TextAnchor.MiddleCenter),
                EquipmentCategory.Dash => (new Vector2(0f, -Reach * 0.82f - gap), TextAnchor.MiddleCenter),
                _ => (new Vector2(Reach, 6f - half - gap), TextAnchor.MiddleCenter),
            };
        }

        static Slot BuildSlot(RectTransform parent, Vector2 position, Color color)
        {
            var background = UiKit.Image(parent, "Slot", new Color(0.02f, 0.03f, 0.06f, 0.7f), UiKit.Circle);
            Centre(background.rectTransform, position, SlotSize);
            var slot = new Slot
            {
                Ring = UiKit.Image(background.transform, "Ring", color, UiKit.Ring),
                Icon = UiKit.Image(background.transform, "Icon", Color.white),
                Ghost = UiKit.Image(background.transform, "Preview", Color.white),
                Plus = UiKit.Text(background.transform, "Plus", "+", 28, color, TextAnchor.MiddleCenter, FontStyle.Bold),
                Level = UiKit.Text(background.transform, "Level", string.Empty, 16, Color.white, TextAnchor.LowerRight, FontStyle.Bold),
            };
            Centre(slot.Ring.rectTransform, Vector2.zero, SlotSize + 8f);
            slot.Icon.preserveAspect = slot.Ghost.preserveAspect = true;
            UiKit.Stretch(slot.Icon.rectTransform, 9f);
            UiKit.Stretch(slot.Ghost.rectTransform, 9f);
            UiKit.Stretch(slot.Plus.rectTransform);
            UiKit.Stretch(slot.Level.rectTransform).offsetMax = new Vector2(3f, 2f);
            slot.Level.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            return slot;
        }

        static void Centre(RectTransform rect, Vector2 position, float size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            if (size > 0f)
                rect.sizeDelta = new Vector2(size, size);
        }
    }
}
