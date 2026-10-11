using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A Hive Matriarch spore cloud on the floor (playtest 7): a translucent green area with a crisp, breathing edge
    /// that marks exactly where it burns, churning fog, and spores drifting up out of it. It pops in when the pod bursts
    /// and fades out over its last moments, so the way out is clear and so is the moment it's gone. The brain sets the
    /// radius once and the cloud's age every tick.
    /// </summary>
    public sealed class SporeCloud : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Flat layers lying on the floor, authored at a radius of 1: sized to the cloud and faded out with it.")]
        Renderer[] areas = new Renderer[0];
        [SerializeField, Tooltip("Layers that slowly turn and breathe, alternating direction: the churning fog.")] Transform[] fogLayers = new Transform[0];
        [SerializeField] float fogSpin = 12f;
        [SerializeField] LineRenderer edge;
        [SerializeField] ParticleSystem motes;
        [SerializeField, Min(8)] int segments = 96;
        [SerializeField, Min(0.01f)] float popTime = 0.25f;
        [SerializeField, Min(0.01f), Tooltip("Seconds it takes to fade out at the end.")] float fadeTime = 0.8f;
        [SerializeField, Min(0f)] float edgeWidth = 0.1f;

        MaterialPropertyBlock _block;
        Color[] _baseColors;
        Vector3[] _fogScales = new Vector3[0];
        float _radius = 1f;
        float _age, _remaining = float.MaxValue;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseColors = new Color[areas.Length];
            for (int i = 0; i < areas.Length; i++)
                _baseColors[i] = areas[i].sharedMaterial.GetColor(BaseColorId);
            edge.useWorldSpace = false;
            edge.loop = true;
            edge.positionCount = segments;
        }

        /// <summary>Sizes the cloud to its burn radius.</summary>
        public void Begin(float radius)
        {
            _radius = radius;
            foreach (var area in areas)
            {
                float size = area.transform.localScale.x;
                area.transform.localScale = new Vector3(size * radius, size * radius, 1f); // authored at a radius of 1
            }
            _fogScales = new Vector3[fogLayers.Length];
            for (int i = 0; i < fogLayers.Length; i++)
                _fogScales[i] = fogLayers[i].localScale;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                edge.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius));
            }
            if (motes != null)
            {
                var shape = motes.shape;
                shape.radius = radius * 0.9f;
                var emission = motes.emission;
                emission.rateOverTimeMultiplier *= radius * radius; // the same density on any size
            }
        }

        /// <summary>How long the cloud has been down and how long it has left, in seconds.</summary>
        public void SetLife(float age, float remaining)
        {
            _age = age;
            _remaining = remaining;
            if (motes != null && remaining < fadeTime && motes.isEmitting)
                motes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void LateUpdate()
        {
            float pop = _age < popTime ? EaseOutBack(_age / popTime) : 1f;
            transform.localScale = new Vector3(pop, 1f, pop);

            // Turn the fog about the floor's normal (a flat quad's own Y would tip it out of the floor).
            for (int i = 0; i < fogLayers.Length && i < _fogScales.Length; i++)
            {
                float direction = i % 2 == 0 ? 1f : -1.4f;
                fogLayers[i].Rotate(Vector3.up, fogSpin * direction * Time.deltaTime, Space.World);
                float breath = 1f + 0.06f * Mathf.Sin(Time.time * (1.3f + 0.4f * i) + i);
                fogLayers[i].localScale = new Vector3(_fogScales[i].x * breath, _fogScales[i].y * breath, _fogScales[i].z);
            }

            float fade = Mathf.Clamp01(_remaining / fadeTime);
            for (int i = 0; i < areas.Length; i++)
            {
                _block.SetColor(BaseColorId, _baseColors[i] * new Color(1f, 1f, 1f, fade));
                areas[i].SetPropertyBlock(_block);
            }

            float breathe = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            edge.widthMultiplier = edgeWidth * (0.85f + 0.3f * breathe);
            var color = new Color(1f, 1f, 1f, fade * (0.65f + 0.35f * breathe));
            edge.startColor = color;
            edge.endColor = color;
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.70158f;
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }
    }
}
