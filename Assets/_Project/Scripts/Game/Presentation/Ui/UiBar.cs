using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>A horizontal gauge: a dark track with a coloured fill from the left, e.g. hull or a reload.</summary>
    public sealed class UiBar
    {
        static readonly Color Track = new Color(0f, 0f, 0f, 0.55f);

        readonly RectTransform _fill;
        readonly Image _fillImage;

        public RectTransform Rect { get; }
        public float Fill { get; private set; }

        public UiBar(Transform parent, string name, Color color)
        {
            var track = UiKit.Image(parent, name, Track);
            Rect = track.rectTransform;
            _fillImage = UiKit.Image(Rect, "Fill", color);
            _fill = _fillImage.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
        }

        public void Set(float fill, Color? color = null)
        {
            Fill = Mathf.Clamp01(fill);
            _fill.anchorMax = new Vector2(Fill, 1f);
            if (color.HasValue)
                _fillImage.color = color.Value;
        }
    }
}
