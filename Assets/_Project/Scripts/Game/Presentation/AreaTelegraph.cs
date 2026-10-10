using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Marks the floor an attack is about to cover (GDD §12.4): a bright outline at the full reach, and a fill that grows
    /// out to it as the attack approaches, flickering just before it lands. A cone for a Scorcher's flames, a circle for a
    /// Crusher's slam or a Warden core's reboot. The meshes are built at unit radius from the source's arc. The renderers
    /// start disabled, so the entity's hit flash leaves them alone.
    /// </summary>
    public sealed class AreaTelegraph : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing IAreaTelegraph, usually the enemy's brain.")] MonoBehaviour source;
        [SerializeField] MeshFilter outline;
        [SerializeField, Tooltip("A child of this object, scaled by the wind-up progress.")] MeshFilter fill;
        [SerializeField, Range(8, 96)] int segments = 48;
        [SerializeField, Range(0.01f, 0.4f), Tooltip("Outline thickness as a fraction of the radius.")] float outlineWidth = 0.07f;
        [SerializeField, Range(0f, 1f)] float fillRimAlpha = 0.5f;
        [SerializeField, Range(0f, 1f)] float fillCentreAlpha = 0.12f;

        IAreaTelegraph _telegraph;
        Renderer _outlineRenderer, _fillRenderer;

        void Awake()
        {
            _telegraph = source as IAreaTelegraph;
            _outlineRenderer = outline.GetComponent<Renderer>();
            _fillRenderer = fill.GetComponent<Renderer>();
            float arc = _telegraph != null ? Mathf.Clamp(_telegraph.AreaArc, 1f, 360f) : 360f;
            outline.sharedMesh = BuildOutline(arc);
            fill.sharedMesh = BuildFill(arc);
            Show(false);
        }

        void LateUpdate()
        {
            bool shown = _telegraph != null && _telegraph.ShowsArea;
            Show(shown);
            if (!shown)
                return;

            // Sized in world units whatever the enemy's own scale (elites are bigger).
            float parentScale = transform.parent != null ? Mathf.Max(transform.parent.lossyScale.x, 0.01f) : 1f;
            transform.localScale = Vector3.one * (_telegraph.AreaRadius / parentScale);
            float progress = Mathf.Clamp01(_telegraph.AreaProgress);
            fill.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 1f, progress);
            _outlineRenderer.enabled = progress < 0.75f || Mathf.Repeat(Time.time * 14f, 1f) < 0.6f;
        }

        void Show(bool shown)
        {
            _outlineRenderer.enabled = shown;
            _fillRenderer.enabled = shown;
        }

        Mesh BuildFill(float arc)
        {
            int steps = Steps(arc);
            var vertices = new Vector3[steps * 3];
            var colors = new Color[vertices.Length];
            var triangles = new int[vertices.Length * 2];
            for (int i = 0; i < steps; i++)
            {
                int v = i * 3;
                vertices[v] = Vector3.zero;
                vertices[v + 1] = Edge(arc, i / (float)steps);
                vertices[v + 2] = Edge(arc, (i + 1) / (float)steps);
                colors[v] = new Color(1f, 1f, 1f, fillCentreAlpha);
                colors[v + 1] = colors[v + 2] = new Color(1f, 1f, 1f, fillRimAlpha);
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                // And the other winding, so it shows whatever the culling.
                triangles[t + 3] = v;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 1;
            }
            return Finish("Area Fill", vertices, colors, triangles);
        }

        Mesh BuildOutline(float arc)
        {
            int steps = Steps(arc);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            float inner = 1f - outlineWidth;

            // The rim: a band along the arc.
            for (int i = 0; i <= steps; i++)
            {
                Vector3 edge = Edge(arc, i / (float)steps);
                vertices.Add(edge * inner);
                vertices.Add(edge);
                if (i > 0)
                    AddQuad(triangles, vertices.Count - 4, vertices.Count - 3, vertices.Count - 1, vertices.Count - 2);
            }

            // A cone also gets its two straight sides.
            if (arc < 359f)
            {
                foreach (float t in new[] { 0f, 1f })
                {
                    Vector3 edge = Edge(arc, t);
                    Vector3 side = Vector3.Cross(Vector3.up, edge).normalized * (outlineWidth * 0.5f);
                    int start = vertices.Count;
                    vertices.Add(-side);
                    vertices.Add(edge - side);
                    vertices.Add(edge + side);
                    vertices.Add(side);
                    AddQuad(triangles, start, start + 1, start + 2, start + 3);
                }
            }

            var colors = new Color[vertices.Count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = Color.white;
            return Finish("Area Outline", vertices.ToArray(), colors, triangles.ToArray());
        }

        int Steps(float arc) => Mathf.Max(2, Mathf.CeilToInt(segments * arc / 360f));

        /// <summary>A point on the unit rim, <paramref name="t"/> of the way across the arc, which is centred on +Z.</summary>
        static Vector3 Edge(float arc, float t)
        {
            float heading = (t - 0.5f) * arc * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
        }

        /// <summary>Two triangles facing up, for corners given in a loop.</summary>
        static void AddQuad(System.Collections.Generic.List<int> triangles, int a, int b, int c, int d)
        {
            triangles.AddRange(new[] { a, b, c, a, c, d });
            // Both windings, so the quad shows whichever way the loop runs.
            triangles.AddRange(new[] { a, c, b, a, d, c });
        }

        static Mesh Finish(string name, Vector3[] vertices, Color[] colors, int[] triangles)
        {
            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < uvs.Length; i++)
                uvs[i] = new Vector2(0.5f, 0.5f); // the centre of the soft-dot texture: full strength
            var mesh = new Mesh { name = name, vertices = vertices, colors = colors, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
