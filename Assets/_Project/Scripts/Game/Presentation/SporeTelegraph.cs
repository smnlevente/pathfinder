using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Where a Hive Matriarch spore pod will land (playtest 7: the first marks were blurry textured quads that didn't
    /// read): the pod itself, lobbed in an arc from her to the spot; a crisp outline at the burst radius; a fill that
    /// grows from the centre to the outline as the pod falls; and a ring that closes in to meet it, like the Quarry
    /// Maw's eruption. In the last moment everything flashes: get out now. Lines are drawn as geometry, so they stay
    /// sharp at any size. The brain drives the progress on its tick; the view interpolates between ticks.
    /// </summary>
    public sealed class SporeTelegraph : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer fill;
        [SerializeField] LineRenderer outline;
        [SerializeField] LineRenderer closingRing;
        [SerializeField] Transform pod;
        [SerializeField, Min(8)] int segments = 96;
        [SerializeField, Min(1f), Tooltip("The closing ring starts this many times the burst radius.")] float closingStart = 1.8f;
        [SerializeField, Min(0f), Tooltip("How high the pod's arc peaks, in units.")] float arcHeight = 3f;

        Vector2 _from, _centre;
        float _radius = 1f;
        float _previous, _current, _shownAt = -1f;
        bool _locked;
        MaterialPropertyBlock _block;
        Color _fillColor;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _fillColor = fill.sharedMaterial.GetColor(BaseColorId);
            outline.useWorldSpace = closingRing.useWorldSpace = false;
            outline.loop = closingRing.loop = true;
            outline.positionCount = closingRing.positionCount = segments;
        }

        /// <summary>Starts the telegraph: a pod thrown from <paramref name="from"/> to burst at <paramref name="centre"/>.</summary>
        public void Launch(Vector2 from, Vector2 centre, float radius)
        {
            _from = from;
            _centre = centre;
            _radius = radius;
            transform.position = PlaneMath.ToWorld(centre);
            _previous = _current = 0f;
            _shownAt = Time.fixedTime;
            DrawCircle(outline, radius);
            Render(0f);
        }

        /// <summary>How far the pod has flown (0 at the throw, 1 at the burst). Locked: about to burst.</summary>
        public void Show(float progress, bool locked)
        {
            _previous = _current;
            _current = Mathf.Clamp01(progress);
            _locked = locked;
            _shownAt = Time.fixedTime;
        }

        void LateUpdate()
        {
            // Between two ticks, ease from the last value to the new one; while the game is paused, hold still.
            bool fresh = Mathf.Approximately(_shownAt, Time.fixedTime);
            Render(fresh ? Mathf.Lerp(_previous, _current, SimInterpolation.Alpha) : _current);
        }

        void Render(float progress)
        {
            float diameter = _radius * 2f * Mathf.Lerp(0.12f, 1f, progress);
            fill.transform.localScale = new Vector3(diameter, diameter, 1f);
            DrawCircle(closingRing, _radius * Mathf.Lerp(closingStart, 1f, progress));

            bool flash = _locked && Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
            float strength = flash ? 2.4f : Mathf.Lerp(0.45f, 1f, progress);
            _block.SetColor(BaseColorId, _fillColor * strength);
            fill.SetPropertyBlock(_block);
            // Line brightness rides on vertex alpha (vertex colours can't go above 1; the materials carry the HDR glow).
            SetAlpha(outline, flash ? 1f : 0.6f);
            SetAlpha(closingRing, Mathf.Lerp(0.35f, 0.95f, progress));

            // The pod flies in an arc and lands as the fill reaches the outline.
            Vector2 ground = Vector2.Lerp(_from, _centre, progress);
            float height = 0.5f + arcHeight * 4f * progress * (1f - progress);
            pod.position = new Vector3(ground.x, height, ground.y);
            pod.gameObject.SetActive(progress < 1f);
        }

        void DrawCircle(LineRenderer line, float radius)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius));
            }
        }

        static void SetAlpha(LineRenderer line, float alpha)
        {
            var color = new Color(1f, 1f, 1f, alpha);
            line.startColor = color;
            line.endColor = color;
        }
    }
}
