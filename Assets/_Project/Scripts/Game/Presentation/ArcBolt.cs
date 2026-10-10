using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A jagged lightning bolt between two points: a white core inside a red-orange glow that crackles (re-jags a few
    /// times) and fades out in a fraction of a second, then removes itself. Presentation only.
    /// </summary>
    public sealed class ArcBolt : MonoBehaviour
    {
        [SerializeField] LineRenderer core;
        [SerializeField] LineRenderer glow;
        [SerializeField, Min(0.02f)] float lifetime = 0.2f;
        [SerializeField, Min(0.1f)] float segmentLength = 0.7f;
        [SerializeField, Min(0f), Tooltip("Sideways offset of each kink, in units.")] float jag = 0.35f;
        [SerializeField, Min(0.01f)] float crackleInterval = 0.05f;

        Vector3 _from, _to;
        float _age, _crackle;
        float _coreWidth, _glowWidth;

        void Awake()
        {
            _coreWidth = core.widthMultiplier;
            _glowWidth = glow.widthMultiplier;
            core.useWorldSpace = glow.useWorldSpace = true;
        }

        public void Strike(Vector3 from, Vector3 to)
        {
            _from = from;
            _to = to;
            transform.position = from;
            Jag();
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            _crackle += Time.deltaTime;
            if (_crackle >= crackleInterval)
            {
                _crackle = 0f;
                Jag();
            }

            float fade = 1f - _age / lifetime;
            core.widthMultiplier = _coreWidth * fade;
            glow.widthMultiplier = _glowWidth * Mathf.Sqrt(fade);
        }

        void Jag()
        {
            Vector3 path = _to - _from;
            float length = path.magnitude;
            int points = Mathf.Max(2, Mathf.CeilToInt(length / segmentLength) + 1);
            Vector3 side = length > 1e-4f ? Vector3.Cross(Vector3.up, path / length) : Vector3.right;

            core.positionCount = glow.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float t = i / (points - 1f);
                // The ends stay pinned to the drone and the target; the kinks in between are random each crackle.
                float offset = i == 0 || i == points - 1 ? 0f : Random.Range(-jag, jag);
                Vector3 point = _from + path * t + side * offset;
                core.SetPosition(i, point);
                glow.SetPosition(i, point);
            }
        }
    }
}
