using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Visual state for a level object: a Dark Matter node filling up as it is harvested, or a station dimming once it
    /// has been used. Presentation only.
    /// </summary>
    public sealed class ObjectiveMarker : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("Scales up with progress, e.g. a harvest ring.")] Transform progressIndicator;
        [SerializeField] Renderer[] dimWhenSpent = new Renderer[0];

        Vector3 _indicatorScale;
        MaterialPropertyBlock _block;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (progressIndicator != null)
            {
                _indicatorScale = progressIndicator.localScale;
                progressIndicator.localScale = Vector3.zero;
            }
        }

        public void SetProgress(float progress)
        {
            if (progressIndicator != null)
                progressIndicator.localScale = _indicatorScale * Mathf.Clamp01(progress);
        }

        public void SetSpent()
        {
            SetProgress(0f);
            foreach (var renderer in dimWhenSpent)
            {
                var color = renderer.sharedMaterial.GetColor(BaseColorId);
                _block.SetColor(BaseColorId, color * 0.25f);
                renderer.SetPropertyBlock(_block);
            }
        }
    }
}
