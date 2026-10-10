using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>
    /// A vertical list of menu rows that sizes itself to its content up to a height limit, and scrolls beyond it, with a
    /// slim scrollbar that only shows when needed (playtest 5: the Hub's options page ran off a 1080p screen). The mouse
    /// wheel scrolls it; menus call <see cref="ScrollTo"/> so keyboard and gamepad selection stays in view.
    /// </summary>
    public sealed class UiScrollList
    {
        const float MinHeight = 120f;

        readonly LayoutElement _layout;
        readonly ScrollRect _scroll;

        public RectTransform Viewport { get; }

        /// <summary>Add rows here.</summary>
        public RectTransform Content { get; }

        public UiScrollList(Transform parent, float spacing = 8f)
        {
            var list = UiKit.Node("List", parent);
            _layout = UiKit.Layout(list, preferredHeight: MinHeight);
            _scroll = list.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;

            Viewport = UiKit.Stretch(UiKit.Image(list, "Viewport", Color.clear, raycast: true).rectTransform);
            Viewport.gameObject.AddComponent<RectMask2D>();
            Content = UiKit.Node("Content", Viewport);
            Content.anchorMin = new Vector2(0f, 1f);
            Content.anchorMax = Vector2.one;
            Content.pivot = new Vector2(0.5f, 1f);
            Content.sizeDelta = Vector2.zero;
            UiKit.Vertical(Content, spacing);
            UiKit.FitContent(Content);
            _scroll.viewport = Viewport;
            _scroll.content = Content;

            // A thin bar on the right, shown only when the list is longer than the space it has.
            var bar = UiKit.Image(list, "Scrollbar", new Color(1f, 1f, 1f, 0.06f), raycast: true);
            var barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = Vector2.one;
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(8f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            var area = UiKit.Stretch(UiKit.Node("Sliding Area", barRect));
            var handle = UiKit.Image(area, "Handle", new Color(UiKit.Accent.r, UiKit.Accent.g, UiKit.Accent.b, 0.6f), raycast: true);
            UiKit.Stretch(handle.rectTransform);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            _scroll.verticalScrollbarSpacing = 6f;
        }

        /// <summary>Whether the rows are taller than the space the list has.</summary>
        public bool Scrolls => LayoutUtility.GetPreferredHeight(Content) > _layout.preferredHeight + 0.5f;

        public void Clear()
        {
            for (int i = Content.childCount - 1; i >= 0; i--)
                Object.Destroy(Content.GetChild(i).gameObject);
            Content.DetachChildren();
            Content.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// Sizes the list after its rows change: as tall as its rows, but never so tall that <paramref name="panel"/> (which
        /// holds the list and fits its content) grows past <paramref name="panelMaxHeight"/>.
        /// </summary>
        public void Fit(RectTransform panel, float panelMaxHeight)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            float rows = LayoutUtility.GetPreferredHeight(Content);
            _layout.preferredHeight = rows;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            float overhead = LayoutUtility.GetPreferredHeight(panel) - rows;
            _layout.preferredHeight = Mathf.Min(rows, Mathf.Max(MinHeight, panelMaxHeight - overhead));
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        }

        /// <summary>Scrolls just enough to show the row, e.g. the one the keyboard selected.</summary>
        public void ScrollTo(RectTransform row)
        {
            if (row == null)
                return;
            float top = -row.anchoredPosition.y - row.rect.height * (1f - row.pivot.y);
            float bottom = top + row.rect.height;
            float view = Viewport.rect.height;
            float offset = Content.anchoredPosition.y;
            if (top < offset)
                offset = top - 8f;
            else if (bottom > offset + view)
                offset = bottom - view + 8f;
            float max = Mathf.Max(0f, Content.rect.height - view);
            Content.anchoredPosition = new Vector2(0f, Mathf.Clamp(offset, 0f, max));
        }

        /// <summary>Whether the whole row is inside the visible part of the list. For tests.</summary>
        public bool IsVisible(RectTransform row)
        {
            var rowCorners = new Vector3[4];
            var viewCorners = new Vector3[4];
            row.GetWorldCorners(rowCorners);
            Viewport.GetWorldCorners(viewCorners);
            return rowCorners[0].y >= viewCorners[0].y - 0.5f && rowCorners[1].y <= viewCorners[1].y + 0.5f;
        }
    }
}
