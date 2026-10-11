using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Where something is about to hit the floor: the Quarry Maw's eruption, a Hive Matriarch spore pod. A crisp outline
    /// at the blast radius, a fill that grows from the centre to the outline as the moment nears, and a ring that closes
    /// in to meet it; in the last moment (once the spot locks) it all flashes: get out now. Optionally, the thing that will
    /// land (a spore pod) is seen arcing in from where it was thrown. Rings are drawn as geometry, so they stay sharp at
    /// any size (playtest 7: the first marks were small textures stretched over the arena and read as blurry). The brain
    /// drives the centre and progress on its tick; the view interpolates between ticks and holds still while paused.
    /// </summary>
    public sealed class ImpactTelegraph : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer fill;
        [SerializeField] LineRenderer outline;
        [SerializeField] LineRenderer closingRing;
        [SerializeField, Tooltip("Optional: what will land, flown in an arc from where it was thrown.")] Transform projectile;
        [SerializeField, Min(8)] int segments = 96;
        [SerializeField, Min(1f), Tooltip("The closing ring starts this many times the blast radius.")] float closingStart = 1.8f;
        [SerializeField, Min(0f), Tooltip("How high the projectile's arc peaks, in units.")] float arcHeight = 3f;
        [SerializeField, Min(1f)] float flashRate = 14f;

        Vector2 _from;
        Vector2 _previousCentre, _centre;
        float _radius = 1f;
        float _previous, _current, _shownAt = -1f;
        bool _locked;
        MaterialPropertyBlock _block;
        Color _fillColor;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _fillColor = fill.sharedMaterial.GetColor(BaseColorId);
            foreach (var line in new[] { outline, closingRing })
            {
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = segments;
            }
        }

        /// <summary>
        /// Starts the telegraph for a blast of <paramref name="radius"/> at <paramref name="centre"/>; a projectile, if
        /// there is one, flies in from <paramref name="from"/>.
        /// </summary>
        public void Launch(Vector2 from, Vector2 centre, float radius)
        {
            _from = from;
            _previousCentre = _centre = centre;
            _radius = radius;
            _previous = _current = 0f;
            _locked = false;
            _shownAt = Time.fixedTime;
            DrawCircle(outline, radius);
            Render(centre, 0f);
        }

        /// <summary>Moves the blast (the Maw's follows the ship until it locks) and shows how close it is: 0 to 1 at impact.</summary>
        public void Show(Vector2 centre, float progress, bool locked)
        {
            _previousCentre = _centre;
            _centre = centre;
            _previous = _current;
            _current = Mathf.Clamp01(progress);
            _locked = locked;
            _shownAt = Time.fixedTime;
        }

        void LateUpdate()
        {
            // Between two ticks, ease from the last state to the new one; while the game is paused, hold still.
            bool fresh = Mathf.Approximately(_shownAt, Time.fixedTime);
            float alpha = fresh ? SimInterpolation.Alpha : 1f;
            Render(Vector2.Lerp(_previousCentre, _centre, alpha), Mathf.Lerp(_previous, _current, alpha));
        }

        void Render(Vector2 centre, float progress)
        {
            transform.position = PlaneMath.ToWorld(centre);

            float diameter = _radius * 2f * Mathf.Lerp(0.12f, 1f, progress);
            fill.transform.localScale = new Vector3(diameter, diameter, 1f);
            DrawCircle(closingRing, _radius * Mathf.Lerp(closingStart, 1f, progress));

            bool flash = _locked && Mathf.Repeat(Time.time * flashRate, 1f) < 0.5f;
            _block.SetColor(BaseColorId, _fillColor * (flash ? 2.4f : Mathf.Lerp(0.45f, 1f, progress)));
            fill.SetPropertyBlock(_block);
            // Line brightness rides on vertex alpha (vertex colours can't go above 1; the materials carry the HDR glow).
            SetAlpha(outline, flash ? 1f : 0.6f);
            SetAlpha(closingRing, Mathf.Lerp(0.35f, 0.95f, progress));

            if (projectile != null)
            {
                Vector2 ground = Vector2.Lerp(_from, centre, progress);
                float height = 0.5f + arcHeight * 4f * progress * (1f - progress);
                projectile.position = new Vector3(ground.x, height, ground.y);
                projectile.gameObject.SetActive(progress < 1f);
            }
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
