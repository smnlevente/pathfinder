using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>
    /// Builds the game's uGUI screens from code (GDD §12): a 1920×1080 reference canvas that scales to fit, a small
    /// colour theme, and helpers for text, panels and layout. Screens build their hierarchy once and refresh it.
    /// </summary>
    public static class UiKit
    {
        public const int UiLayer = 5;
        public static readonly Vector2 ReferenceSize = new Vector2(1920f, 1080f);

        // Theme. Cyan is the player's colour (GDD §12.4), so it marks the current selection.
        public static readonly Color BackdropColor = new Color(0.01f, 0.02f, 0.05f, 0.9f);
        public static readonly Color Panel = new Color(0.035f, 0.05f, 0.085f, 0.97f);
        public static readonly Color Row = new Color(0.075f, 0.1f, 0.15f, 1f);
        public static readonly Color RowSelected = new Color(0.1f, 0.27f, 0.37f, 1f);
        public static readonly Color Frame = new Color(0.35f, 0.85f, 1f, 1f);
        public static readonly Color TextBright = Color.white;
        public static readonly Color TextBody = new Color(0.8f, 0.88f, 0.98f);
        public static readonly Color TextDim = new Color(0.5f, 0.57f, 0.67f);
        public static readonly Color Accent = new Color(0.35f, 0.85f, 1f);
        public static readonly Color Good = new Color(0.45f, 1f, 0.6f);
        public static readonly Color Bad = new Color(1f, 0.48f, 0.42f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        static Font s_font;
        static Sprite s_circle, s_ring;

        public static Font Font => s_font != null ? s_font : s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>A soft-edged filled circle, for map nodes and pips.</summary>
        public static Sprite Circle => s_circle != null ? s_circle : s_circle = MakeRadialSprite(128, d => Mathf.Clamp01((1f - d) * 40f));

        /// <summary>A thin ring, for selection halos.</summary>
        public static Sprite Ring => s_ring != null ? s_ring : s_ring = MakeRadialSprite(128, d => Mathf.Clamp01((0.06f - Mathf.Abs(d - 0.9f)) * 40f));

        /// <summary>A screen-space overlay canvas that scales the 1920×1080 layout to fit any screen.</summary>
        public static Canvas Canvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Mouse clicks need an event system. Keyboard and gamepad menus read input themselves.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var image = Node(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycast;
            return image;
        }

        public static Text Text(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var label = Node(name, parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.lineSpacing = 1.05f;
            return label;
        }

        /// <summary>Fills the parent, minus an inset on every side.</summary>
        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        /// <summary>Positions a rect relative to an anchor point of its parent, in reference pixels.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static VerticalLayoutGroup Vertical(Component target, float spacing, RectOffset padding = null, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            var group = target.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset();
            group.childAlignment = alignment;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return group;
        }

        public static HorizontalLayoutGroup Horizontal(Component target, float spacing, RectOffset padding = null, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var group = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding ?? new RectOffset();
            group.childAlignment = alignment;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return group;
        }

        /// <summary>Sizes the rect to its content's preferred height (and width, if asked).</summary>
        public static ContentSizeFitter FitContent(Component target, bool width = false)
        {
            var fitter = target.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = width ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return fitter;
        }

        public static LayoutElement Layout(Component target, float preferredWidth = -1f, float preferredHeight = -1f,
            float flexibleWidth = -1f, float minHeight = -1f)
        {
            if (!target.TryGetComponent(out LayoutElement element))
                element = target.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = preferredWidth;
            element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            element.minHeight = minHeight;
            return element;
        }

        /// <summary>A full-screen dimmed backdrop that also swallows clicks meant for the world.</summary>
        public static Image Backdrop(Transform parent)
        {
            var image = Image(parent, "Backdrop", BackdropColor, raycast: true);
            Stretch(image.rectTransform);
            return image;
        }

        public static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

        /// <summary>Wraps text in a rich-text colour tag.</summary>
        public static string Colored(string text, Color color) => $"<color=#{Hex(color)}>{text}</color>";

        static Sprite MakeRadialSprite(int size, System.Func<float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(d)));
                }
            }
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
