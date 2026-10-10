using System.Collections.Generic;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Floor telegraph for a charge path (GDD §12.4): a lane of chevrons pointing along the path. They light up from the
    /// start as the wind-up completes, then all flash just before launch. Flat, segmented and pointed, so it can't be
    /// mistaken for a bullet. Positions are in world space, so it works whatever its parent's rotation and scale.
    /// </summary>
    public sealed class ChevronLane : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("A flat chevron quad pointing along its local +Z after the template's rotation. Kept disabled.")]
        Renderer chevronTemplate;
        [SerializeField, Min(0.2f)] float spacing = 1.1f;
        [SerializeField, Min(0.1f)] float size = 0.9f;
        [SerializeField, Min(0f)] float height = 0.05f;
        [SerializeField] Color dimColor = new Color(0.9f, 0.14f, 0.06f, 0.35f);
        [SerializeField] Color litColor = new Color(2.6f, 0.55f, 0.12f, 0.95f);
        [SerializeField, Range(0f, 1f), Tooltip("From this progress on, every chevron flashes: launch is imminent.")]
        float flashFrom = 0.85f;

        readonly List<Renderer> _chevrons = new List<Renderer>();
        MaterialPropertyBlock _block;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (chevronTemplate != null)
                chevronTemplate.enabled = false;
        }

        /// <summary>Shows the lane from <paramref name="start"/> along <paramref name="direction"/> on the plane.</summary>
        /// <param name="progress">Wind-up progress, 0 to 1.</param>
        public void Show(Vector2 start, Vector2 direction, float length, float progress)
        {
            if (chevronTemplate == null || direction.sqrMagnitude < 1e-6f)
            {
                Hide();
                return;
            }

            direction.Normalize();
            int count = Mathf.Max(1, Mathf.FloorToInt(length / spacing));
            while (_chevrons.Count < count)
                _chevrons.Add(Instantiate(chevronTemplate, transform));

            var rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y)) * chevronTemplate.transform.localRotation;
            float scale = size / Mathf.Max(transform.lossyScale.x, 0.01f);
            bool flash = progress >= flashFrom && Mathf.Repeat(Time.time * 16f, 1f) < 0.5f;

            for (int i = 0; i < _chevrons.Count; i++)
            {
                var chevron = _chevrons[i];
                bool visible = i < count;
                chevron.enabled = visible;
                if (!visible)
                    continue;

                float along = (i + 0.6f) * spacing;
                var t = chevron.transform;
                t.SetPositionAndRotation(new Vector3(start.x + direction.x * along, height, start.y + direction.y * along), rotation);
                t.localScale = new Vector3(scale, scale, 1f);

                bool lit = flash || (i + 1f) / count <= progress;
                _block.SetColor(BaseColorId, lit ? litColor : dimColor);
                chevron.SetPropertyBlock(_block);
            }
        }

        public void Hide()
        {
            foreach (var chevron in _chevrons)
                chevron.enabled = false;
        }
    }
}
