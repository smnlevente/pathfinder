using System.Collections.Generic;
using Pathfinder.Core.Run;
using Pathfinder.Game.Run;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Placeholder IMGUI galaxy map (GDD §12.3): a vertical node graph with type icons and the path walked so far. The
    /// selectable next nodes pulse. Choose with the mouse, or with left/right and Submit. The real UI comes with the
    /// UI pass.
    /// </summary>
    public sealed class GalaxyMapScreen : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        [SerializeField] RunDirector director;

        static readonly Dictionary<MapNodeType, Color> TypeColors = new Dictionary<MapNodeType, Color>
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

        MenuInput _input;
        int _selected;
        GUIStyle _title, _glyph, _info, _small;
        Texture2D _circle;

        public bool IsOpen => director != null && director.State == RunDirector.Phase.Map;

        void Awake()
        {
            _input = new MenuInput();
            _circle = MakeCircle(64);
        }

        void Update()
        {
            if (!IsOpen)
                return;

            var available = director.Run.AvailableNodes;
            if (available.Count == 0)
                return;

            int step = _input.ReadStep().x;
            if (step != 0)
                _selected = (_selected + step + available.Count) % available.Count;
            _selected = Mathf.Clamp(_selected, 0, available.Count - 1);

            if (_input.Submitted)
                director.ChooseNode(available[_selected]);
        }

        void OnGUI()
        {
            if (!IsOpen)
                return;

            GUI.depth = 0;
            EnsureStyles();
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            var run = director.Run;
            var map = run.Map;

            GUI.color = new Color(0.01f, 0.02f, 0.05f, 0.94f);
            GUI.DrawTexture(new Rect(0f, 0f, width, ReferenceHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0f, 24f, width, 50f), $"GALAXY {run.GalaxyIndex + 1} / {RunState.GalaxyCount}:  {director.GalaxyName.ToUpperInvariant()}", _title);
            GUI.Label(new Rect(0f, 72f, width, 30f), "Choose your next jump.   Left / right + Submit, or click a pulsing node.", _small);

            // Layout: rows from bottom (entries) to top (boss), lanes spread evenly.
            var area = new Rect(width * 0.5f - 420f, 130f, 840f, 820f);
            Vector2 Position(MapNode node) => new Vector2(area.x + node.X * area.width, area.yMax - node.Row / (float)(map.RowCount - 1) * area.height);

            var available = run.AvailableNodes;

            foreach (var node in map.Nodes)
            {
                foreach (var next in node.Next)
                {
                    bool walked = run.HasVisited(node) && run.HasVisited(next);
                    bool option = run.CurrentNode == node && Contains(available, next);
                    var color = walked ? new Color(0.4f, 0.9f, 1f, 0.9f) : option ? new Color(1f, 1f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.15f);
                    DrawLine(Position(node), Position(next), walked || option ? 4f : 2f, color);
                }
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var node = map.Nodes[i];
                bool isAvailable = Contains(available, node);
                bool isSelected = isAvailable && available[_selected] == node;
                bool isVisited = run.HasVisited(node);
                float size = node.Type == MapNodeType.Boss ? 74f : node.Type == MapNodeType.Shop ? 62f : 52f;
                if (isSelected)
                    size += 10f * pulse;

                var center = Position(node);
                var rect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
                var color = TypeColors[node.Type];
                float alpha = isAvailable ? 1f : isVisited ? 0.75f : 0.35f;

                if (isSelected)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.6f + 0.4f * pulse);
                    GUI.DrawTexture(new Rect(rect.x - 8f, rect.y - 8f, rect.width + 16f, rect.height + 16f), _circle);
                }

                GUI.color = new Color(color.r, color.g, color.b, alpha);
                GUI.DrawTexture(rect, _circle);
                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.Label(rect, TypeGlyphs[node.Type], _glyph);
                GUI.color = Color.white;

                if (isAvailable && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    director.ChooseNode(node);
                    return;
                }
                if (isAvailable && rect.Contains(Event.current.mousePosition))
                    _selected = IndexOf(available, node);
            }

            if (available.Count > 0)
            {
                var focus = available[Mathf.Clamp(_selected, 0, available.Count - 1)];
                string type = LevelNames.Title(focus.Type);
                GUI.Label(new Rect(width * 0.5f + 460f, 420f, Mathf.Max(200f, width * 0.5f - 500f), 240f),
                    $"{type}\n\n{LevelNames.Description(focus.Type)}", _info);
            }

            GUI.Label(new Rect(40f, ReferenceHeight - 80f, width - 80f, 30f),
                $"Levels cleared {run.LevelsCleared}   ·   Dark Matter {run.DarkMatter}   ·   Singularity Cores {run.SingularityCores}", _small);
            DrawLegend(width);
        }

        void DrawLegend(float width)
        {
            float x = width * 0.5f - 460f - 260f, y = 420f;
            foreach (var pair in TypeColors)
            {
                GUI.color = pair.Value;
                GUI.DrawTexture(new Rect(x, y, 22f, 22f), _circle);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 32f, y - 4f, 200f, 30f), LevelNames.Title(pair.Key), _small);
                y += 34f;
            }
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

        static void DrawLine(Vector2 a, Vector2 b, float width, Color color)
        {
            var matrix = GUI.matrix;
            Vector2 delta = b - a;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.color = color;
            GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.matrix = matrix;
        }

        static Texture2D MakeCircle(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false) { hideFlags = HideFlags.HideAndDontSave };
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d)));
                }
            }
            texture.Apply();
            return texture;
        }

        void EnsureStyles()
        {
            if (_title != null)
                return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;
            _glyph = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _glyph.normal.textColor = Color.white;
            _info = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, alignment = TextAnchor.UpperLeft };
            _info.normal.textColor = new Color(0.85f, 0.92f, 1f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = new Color(0.75f, 0.85f, 1f);
        }

        void OnDestroy()
        {
            if (_circle != null)
                Destroy(_circle);
        }
    }
}
