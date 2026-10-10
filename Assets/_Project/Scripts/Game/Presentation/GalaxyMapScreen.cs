using System.Collections.Generic;
using Pathfinder.Core.Run;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Run;
using UnityEngine;
using UnityEngine.UI;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Galaxy map (GDD §12.3): the node graph from the entries at the bottom to the boss at the top. Every jump lane is
    /// drawn: the path walked so far is bright cyan, the lanes open from the current node are white, and the rest are
    /// faint. The selectable next nodes pulse. Choose with the mouse, or with left/right and Submit.
    /// </summary>
    public sealed class GalaxyMapScreen : MonoBehaviour
    {
        static readonly Vector2 AreaSize = new Vector2(860f, 780f);

        [SerializeField] RunDirector director;

        public static readonly Dictionary<MapNodeType, Color> TypeColors = new Dictionary<MapNodeType, Color>
        {
            { MapNodeType.Scraps, new Color(1f, 0.78f, 0.25f) },
            { MapNodeType.Equipment, new Color(0.35f, 0.85f, 1f) },
            { MapNodeType.DarkMatter, new Color(0.55f, 0.45f, 1f) },
            { MapNodeType.Repair, new Color(0.35f, 1f, 0.55f) },
            { MapNodeType.Mystery, new Color(0.85f, 0.85f, 0.85f) },
            { MapNodeType.Shop, new Color(1f, 0.9f, 0.5f) },
            { MapNodeType.Boss, new Color(1f, 0.3f, 0.25f) },
        };

        static readonly Dictionary<MapNodeType, string> TypeGlyphs = new Dictionary<MapNodeType, string>
        {
            { MapNodeType.Scraps, "S" }, { MapNodeType.Equipment, "E" }, { MapNodeType.DarkMatter, "DM" }, { MapNodeType.Repair, "+" },
            { MapNodeType.Mystery, "?" }, { MapNodeType.Shop, "$" }, { MapNodeType.Boss, "!" },
        };

        sealed class NodeView
        {
            public MapNode Node;
            public RectTransform Rect;
            public Image Fill, Halo;
            public float Size;
        }

        MenuInput _input;
        int _selected;
        bool _wasOpen;
        Canvas _canvas;
        RectTransform _area, _edges, _nodes;
        Text _title, _infoTitle, _infoBody, _footer;
        readonly List<NodeView> _views = new List<NodeView>();

        public bool IsOpen => director != null && director.State == RunDirector.Phase.Map;

        /// <summary>Number of jump lanes drawn. For tests.</summary>
        public int EdgeCount => _edges != null ? _edges.childCount : 0;

        void Awake()
        {
            _input = new MenuInput();
            Build();
            _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            bool open = IsOpen;
            if (open != _wasOpen)
            {
                _wasOpen = open;
                _canvas.gameObject.SetActive(open);
                ModalScreens.SetOpen(this, open, blocksPause: false); // the map is a good moment to pause
                if (open)
                    Rebuild();
            }
            if (!open)
                return;

            var available = director.Run.AvailableNodes;
            if (available.Count == 0)
                return;

            int step = _input.ReadStep().x;
            if (step != 0)
                _selected = (_selected + step + available.Count) % available.Count;
            _selected = Mathf.Clamp(_selected, 0, available.Count - 1);

            if (_input.Submitted)
            {
                director.ChooseNode(available[_selected]);
                return;
            }

            Animate(available);
        }

        // ---------------------------------------------------------------- view

        void Build()
        {
            _canvas = UiKit.Canvas("Galaxy Map", transform, sortingOrder: 10);
            var backdrop = UiKit.Backdrop(_canvas.transform);
            backdrop.color = new Color(0.01f, 0.015f, 0.04f, 0.97f);
            var root = _canvas.transform;

            _title = UiKit.Text(root, "Title", string.Empty, 40, UiKit.TextBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(1600f, 56f));
            var hint = UiKit.Text(root, "Hint", "Choose your next jump.   Left / Right + Enter, or click a pulsing node.", 19, UiKit.TextDim, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(1600f, 30f));

            _area = UiKit.Node("Map", root);
            UiKit.Place(_area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), AreaSize);
            _edges = UiKit.Stretch(UiKit.Node("Lanes", _area));
            _nodes = UiKit.Stretch(UiKit.Node("Nodes", _area));

            // Focused node details, right of the map.
            var info = UiKit.Image(root, "Info", UiKit.Panel);
            UiKit.Place(info.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(AreaSize.x * 0.5f + 70f, 0f), new Vector2(430f, 0f));
            UiKit.Vertical(info, 12f, new RectOffset(28, 28, 24, 26), TextAnchor.UpperLeft);
            UiKit.FitContent(info);
            _infoTitle = UiKit.Text(info.transform, "Title", string.Empty, 32, UiKit.TextBright, TextAnchor.UpperLeft, FontStyle.Bold);
            _infoBody = UiKit.Text(info.transform, "Body", string.Empty, 21, UiKit.TextBody);

            // Legend, left of the map.
            var legend = UiKit.Image(root, "Legend", UiKit.Panel);
            UiKit.Place(legend.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-AreaSize.x * 0.5f - 70f, 0f), new Vector2(300f, 0f));
            UiKit.Vertical(legend, 10f, new RectOffset(24, 24, 20, 20), TextAnchor.UpperLeft);
            UiKit.FitContent(legend);
            foreach (var pair in TypeColors)
            {
                var entry = UiKit.Node(pair.Key.ToString(), legend.transform);
                UiKit.Horizontal(entry, 14f, null, TextAnchor.MiddleLeft);
                var dot = UiKit.Image(entry, "Dot", pair.Value, UiKit.Circle);
                UiKit.Layout(dot, preferredWidth: 24f, preferredHeight: 24f);
                UiKit.Text(entry, "Name", LevelNames.Title(pair.Key), 20, UiKit.TextBody, TextAnchor.MiddleLeft);
            }

            _footer = UiKit.Text(root, "Footer", string.Empty, 20, UiKit.TextDim, TextAnchor.MiddleCenter);
            UiKit.Place(_footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1600f, 30f));
        }

        void Rebuild()
        {
            var run = director.Run;
            var map = run.Map;
            _selected = 0;
            _title.text = $"GALAXY {run.GalaxyIndex + 1} / {RunState.GalaxyCount}:  {director.GalaxyName.ToUpperInvariant()}";
            _footer.text = $"Levels cleared {run.LevelsCleared}     ·     Dark Matter {run.DarkMatter}     ·     Singularity Cores {run.SingularityCores}";

            Clear(_edges);
            Clear(_nodes);
            _views.Clear();
            var available = run.AvailableNodes;

            Vector2 Position(MapNode node) => new Vector2(
                (node.X - 0.5f) * AreaSize.x,
                (node.Row / (float)Mathf.Max(1, map.RowCount - 1) - 0.5f) * AreaSize.y);

            foreach (var node in map.Nodes)
            {
                foreach (var next in node.Next)
                {
                    bool walked = run.HasVisited(node) && run.HasVisited(next);
                    bool option = run.CurrentNode == node && Contains(available, next);
                    var color = walked ? new Color(0.35f, 0.9f, 1f, 1f) : option ? new Color(1f, 1f, 1f, 0.85f) : new Color(0.6f, 0.7f, 0.85f, 0.28f);
                    DrawLane(Position(node), Position(next), walked ? 6f : option ? 5f : 3f, color);
                }
            }

            foreach (var node in map.Nodes)
            {
                bool isAvailable = Contains(available, node);
                bool isVisited = run.HasVisited(node);
                bool isCurrent = run.CurrentNode == node;
                float size = node.Type == MapNodeType.Boss ? 80f : node.Type == MapNodeType.Shop ? 66f : 56f;

                var rect = UiKit.Node(node.Type.ToString(), _nodes);
                UiKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Position(node), new Vector2(size, size));

                var halo = UiKit.Image(rect, "Halo", isCurrent ? UiKit.Accent : Color.white, UiKit.Ring);
                UiKit.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size + 30f, size + 30f));
                halo.enabled = isCurrent || isAvailable;

                var color = TypeColors[node.Type];
                float alpha = isAvailable ? 1f : isVisited ? 0.8f : 0.4f;
                var fill = UiKit.Image(rect, "Fill", new Color(color.r, color.g, color.b, alpha), UiKit.Circle, raycast: isAvailable);
                UiKit.Stretch(fill.rectTransform);
                var glyph = UiKit.Text(rect, "Glyph", TypeGlyphs[node.Type], node.Type == MapNodeType.DarkMatter ? 20 : 26,
                    new Color(0.02f, 0.03f, 0.06f, alpha), TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(glyph.rectTransform);

                if (isAvailable)
                {
                    var pointer = fill.gameObject.AddComponent<UiPointer>();
                    var captured = node;
                    pointer.Entered += () => _selected = IndexOf(director.Run.AvailableNodes, captured);
                    pointer.Clicked += () => director.ChooseNode(captured);
                }

                _views.Add(new NodeView { Node = node, Rect = rect, Fill = fill, Halo = halo, Size = size });
            }
        }

        void DrawLane(Vector2 from, Vector2 to, float width, Color color)
        {
            var lane = UiKit.Image(_edges, "Lane", color);
            var rect = lane.rectTransform;
            Vector2 delta = to - from;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, width);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        void Animate(IReadOnlyList<MapNode> available)
        {
            var focus = available[_selected];
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            foreach (var view in _views)
            {
                bool isAvailable = Contains(available, view.Node);
                bool isSelected = view.Node == focus;
                float scale = isSelected ? 1.1f + 0.1f * pulse : isAvailable ? 1f + 0.04f * pulse : 1f;
                view.Rect.localScale = new Vector3(scale, scale, 1f);
                if (isAvailable)
                    view.Halo.color = new Color(1f, 1f, 1f, isSelected ? 0.6f + 0.4f * pulse : 0.25f);
            }

            _infoTitle.text = LevelNames.Title(focus.Type);
            _infoTitle.color = TypeColors[focus.Type];
            _infoBody.text = LevelNames.Description(focus.Type);
        }

        static void Clear(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
            parent.DetachChildren();
        }

        static bool Contains(IReadOnlyList<MapNode> nodes, MapNode node) => IndexOf(nodes, node) >= 0;

        static int IndexOf(IReadOnlyList<MapNode> nodes, MapNode node)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == node)
                    return i;
            }
            return -1;
        }

        void OnDisable() => ModalScreens.SetOpen(this, false);
    }
}
