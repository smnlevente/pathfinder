using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>A one-shot expanding, fading ring, e.g. the Nova Pulse shockwave. Presentation only.</summary>
    public sealed class RingPulse : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer ring;
        [SerializeField, Min(0.01f)] float duration = 0.3f;
        [SerializeField, Min(0f)] float startScale = 0.5f;
        [SerializeField, Min(0f)] float endScale = 9f;

        MaterialPropertyBlock _block;
        Color _baseColor;
        float _age;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _baseColor = ring.sharedMaterial.GetColor(BaseColorId);
            Apply(0f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(t);
        }

        void Apply(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            float scale = Mathf.Lerp(startScale, endScale, eased);
            transform.localScale = new Vector3(scale, 1f, scale); // horizontal only, so the ring stays at floor height
            _block.SetColor(BaseColorId, _baseColor * (1f - t));
            ring.SetPropertyBlock(_block);
        }
    }
}
