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

        public RectTransform Rect { get; }
        public UiPointer Pointer { get; }
        public string LabelText => _label.text;
        public string DetailText => _detail != null ? _detail.text : string.Empty;

        public UiRow(Transform parent, string label, string note = null, string detail = null, Color? accent = null, Color? noteColor = null)
        {
            _background = UiKit.Image(parent, "Row", UiKit.Row, raycast: true);
            Rect = _background.rectTransform;
            UiKit.Vertical(_background, 4f, new RectOffset(28, 24, 12, 13), TextAnchor.UpperLeft);

            var header = UiKit.Node("Header", Rect);
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

        public void SetLabel(string text) => _label.text = text;

        public void SetNote(string text) => _note.text = text ?? string.Empty;

        public void SetState(bool selected, bool enabled)
        {
            _background.color = selected ? UiKit.RowSelected : UiKit.Row;
            _label.color = enabled ? UiKit.TextBright : UiKit.TextDim;
            _note.color = enabled ? _noteColor : UiKit.TextDim;
            if (_detail != null)
                _detail.color = enabled ? UiKit.TextBody : UiKit.TextDim;
        }
    }
}
