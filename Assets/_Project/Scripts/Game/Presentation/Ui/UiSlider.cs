using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>
    /// A horizontal slider for a 0–1 setting: a thin track that fills up to a round handle. Press anywhere on it to jump
    /// there, then drag; it snaps to whole steps (5% by default). It takes its own left clicks, so the row it sits in
    /// doesn't also react, and passes right clicks up to the row (playtest 5: the options were click-to-cycle only).
    /// </summary>
    public sealed class UiSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerClickHandler
    {
        RectTransform _bar, _fill, _handle;
        float _snap = 0.05f;

        public float Value { get; private set; }

        /// <summary>Raised when the player moves the handle, with the new value.</summary>
        public event Action<float> Dragged;

        public static UiSlider Create(Transform parent, float width, Color color, float snap = 0.05f)
        {
            // An invisible, taller hit area around a thin track, so the slider is easy to grab.
            var hit = UiKit.Image(parent, "Slider", Color.clear, raycast: true);
            UiKit.Layout(hit, preferredWidth: width, preferredHeight: 30f);

            var bar = UiKit.Image(hit.transform, "Track", new Color(1f, 1f, 1f, 0.16f)).rectTransform;
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(-18f, 8f); // inset, so the handle stays inside at both ends
            bar.anchoredPosition = Vector2.zero;

            var fill = UiKit.Image(bar, "Fill", color).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;

            var handle = UiKit.Image(bar, "Handle", Color.white, UiKit.Circle).rectTransform;
            handle.sizeDelta = new Vector2(22f, 22f);

            var slider = hit.gameObject.AddComponent<UiSlider>();
            slider._bar = bar;
            slider._fill = fill;
            slider._handle = handle;
            slider._snap = snap;
            slider.Set(0f);
            return slider;
        }

        /// <summary>Shows a value without raising <see cref="Dragged"/>.</summary>
        public void Set(float value)
        {
            Value = Mathf.Clamp01(value);
            _fill.anchorMax = new Vector2(Value, 1f);
            _handle.anchorMin = _handle.anchorMax = new Vector2(Value, 0.5f);
            _handle.anchoredPosition = Vector2.zero;
        }

        /// <summary>Moves the handle to a point along the track (0 = left end, 1 = right end), as a press or drag does.</summary>
        public void SetFromPointer(float normalized)
        {
            float value = Mathf.Clamp01(normalized);
            if (_snap > 0f)
                value = Mathf.Round(value / _snap) * _snap;
            if (Mathf.Approximately(value, Value))
                return;
            Set(value);
            Dragged?.Invoke(Value);
        }

        public void OnPointerDown(PointerEventData eventData) => Track(eventData);

        public void OnDrag(PointerEventData eventData) => Track(eventData);

        public void OnPointerClick(PointerEventData eventData)
        {
            // Left clicks were handled on press. A right click belongs to the row: one step down.
            if (eventData.button == PointerEventData.InputButton.Right && transform.parent != null)
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        void Track(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_bar, eventData.position, eventData.pressEventCamera, out var local))
                SetFromPointer(Mathf.InverseLerp(_bar.rect.xMin, _bar.rect.xMax, local.x));
        }
    }
}
