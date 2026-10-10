using UnityEngine;
using UnityEngine.EventSystems;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>
    /// Keeps a full-height rect no wider than 16:9, centred in its parent, so HUD panels anchored to its corners stay
    /// near the action on ultrawide screens instead of drifting to the far edges (playtest 6). On 16:9 and narrower it is
    /// simply the whole screen. Refits before every canvas render, so a resolution change or a capture is always right.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : UIBehaviour
    {
        [SerializeField, Min(1f)] float maxAspect = 16f / 9f;

        protected override void OnEnable()
        {
            base.OnEnable();
            Fit();
            Canvas.willRenderCanvases += Fit;
        }

        protected override void OnDisable()
        {
            Canvas.willRenderCanvases -= Fit;
            base.OnDisable();
        }

        void Fit()
        {
            if (!(transform.parent is RectTransform parent))
                return;
            var rect = (RectTransform)transform;
            float width = Mathf.Min(parent.rect.width, parent.rect.height * maxAspect);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (!Mathf.Approximately(rect.sizeDelta.x, width) || rect.sizeDelta.y != 0f || rect.anchoredPosition != Vector2.zero)
            {
                rect.sizeDelta = new Vector2(width, 0f);
                rect.anchoredPosition = Vector2.zero;
            }
        }
    }
}
