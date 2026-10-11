using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>
    /// One selectable row in a menu list: an accent stripe, a bold label with a right-aligned note (a price or a state),
    /// and an optional wrapping detail line, so long descriptions are never cut off. Hover selects it; click activates it.
    /// </summary>
    public sealed class UiRow
    {
        readonly Image _background;
        readonly Text _label, _note, _detail;
        readonly Color _noteColor;
        readonly RectTransform _header;
        Image _check, _icon, _noteIcon;
        Color _iconTint, _noteIconTint;

        public RectTransform Rect { get; }
        public UiPointer Pointer { get; }
        public string LabelText => _label.text;
        public string DetailText => _detail != null ? _detail.text : string.Empty;
        public string NoteText => _note.text;

        /// <summary>The row's slider, if it has one (see <see cref="AddSlider"/>).</summary>
        public UiSlider Slider { get; private set; }

        /// <summary>Whether the row's tickbox is ticked, if it has one (see <see cref="AddCheckbox"/>).</summary>
        public bool IsChecked => _check != null && _check.enabled;

        public UiRow(Transform parent, string label, string note = null, string detail = null, Color? accent = null, Color? noteColor = null)
        {
            _background = UiKit.Image(parent, "Row", UiKit.Row, raycast: true);
            Rect = _background.rectTransform;
            UiKit.Vertical(_background, 4f, new RectOffset(28, 24, 12, 13), TextAnchor.UpperLeft);

            var header = _header = UiKit.Node("Header", Rect);
            UiKit.Horizontal(header, 20f, null, TextAnchor.MiddleLeft);
            _label = UiKit.Text(header, "Label", label, 24, UiKit.TextBright, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Layout(_label, flexibleWidth: 1f);
            _noteColor = noteColor ?? UiKit.Gold;
            _note = UiKit.Text(header, "Note", note ?? string.Empty, 22, _noteColor, TextAnchor.MiddleRight, FontStyle.Bold);
            _note.horizontalOverflow = HorizontalWrapMode.Overflow;

            if (!string.IsNullOrEmpty(detail))
                _detail = UiKit.Text(Rect, "Detail", detail, 19, UiKit.TextBody);

            if (accent.HasValue)
            {
                var stripe = UiKit.Image(Rect, "Stripe", accent.Value);
                UiKit.Layout(stripe).ignoreLayout = true;
                var stripeRect = stripe.rectTransform;
                stripeRect.anchorMin = Vector2.zero;
                stripeRect.anchorMax = new Vector2(0f, 1f);
                stripeRect.pivot = new Vector2(0f, 0.5f);
                stripeRect.sizeDelta = new Vector2(7f, 0f);
                stripeRect.anchoredPosition = Vector2.zero;
            }

            Pointer = _background.gameObject.AddComponent<UiPointer>();
        }

        /// <summary>Adds a slider before the note, which then shows its value at a fixed width so the slider stays put.</summary>
        public UiSlider AddSlider(float width, float snap)
        {
            Slider = UiSlider.Create(_header, width, UiKit.Accent, snap);
            Slider.transform.SetSiblingIndex(_note.transform.GetSiblingIndex());
            UiKit.Layout(_note, preferredWidth: 86f);
            return Slider;
        }

        /// <summary>Adds a tickbox where the note was. Clicking the row toggles it (the owner decides what that does).</summary>
        public void AddCheckbox()
        {
            var box = UiKit.Image(_header, "Tickbox", new Color(1f, 1f, 1f, 0.16f));
            UiKit.Layout(box, preferredWidth: 30f, preferredHeight: 30f);
            box.transform.SetSiblingIndex(_note.transform.GetSiblingIndex());
            _check = UiKit.Image(box.transform, "Tick", UiKit.Accent);
            UiKit.Stretch(_check.rectTransform, 6f);
            _check.enabled = false;
            _note.text = string.Empty;
            _note.gameObject.SetActive(false);
        }

        public void SetChecked(bool on)
        {
            if (_check != null)
                _check.enabled = on;
        }

        /// <summary>Puts a picture before the label, e.g. an item's icon in its rarity colour (playtest 7: fewer words, more pictures).</summary>
        public void SetIcon(Sprite sprite, Color tint, float size = 46f)
        {
            if (sprite == null)
                return;
            _icon = UiKit.Image(_header, "Icon", tint, sprite);
            _icon.preserveAspect = true;
            UiKit.Layout(_icon, preferredWidth: size, preferredHeight: size);
            _icon.transform.SetAsFirstSibling();
            _iconTint = tint;
        }

        /// <summary>Puts a small picture before the note, e.g. the Scraps cog beside a price.</summary>
        public void SetNoteIcon(Sprite sprite, Color tint, float size = 28f)
        {
            if (sprite == null)
                return;
            _noteIcon = UiKit.Image(_header, "NoteIcon", tint, sprite);
            _noteIcon.preserveAspect = true;
            UiKit.Layout(_noteIcon, preferredWidth: size, preferredHeight: size);
            _noteIcon.transform.SetSiblingIndex(_note.transform.GetSiblingIndex());
            _noteIconTint = tint;
        }

        public void SetLabel(string text) => _label.text = text;

        public void SetNote(string text) => _note.text = text ?? string.Empty;

        public void SetState(bool selected, bool enabled)
        {
            _background.color = selected ? UiKit.RowSelected : UiKit.Row;
            _label.color = enabled ? UiKit.TextBright : UiKit.TextDim;
            _note.color = enabled ? _noteColor : UiKit.TextDim;
            if (_detail != null)
                _detail.color = enabled ? UiKit.TextBody : UiKit.TextDim;
            if (_icon != null)
                _icon.color = enabled ? _iconTint : Dimmed(_iconTint);
            if (_noteIcon != null)
                _noteIcon.color = enabled ? _noteIconTint : Dimmed(_noteIconTint);
        }

        static Color Dimmed(Color color) => new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, color.a * 0.7f);
    }
}
