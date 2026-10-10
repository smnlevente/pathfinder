using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Where a burrowed Quarry Maw will burst out (GDD §7.5): a red glow and an outline at the eruption radius, and a
    /// second ring that closes in on the outline as the eruption nears, so its timing can be read at a glance. Once the
    /// spot locks, the glow flickers: dash now (playtest 6: walking away was always enough). Authored at a radius of 1;
    /// the boss sets the real radius and the progress.
    /// </summary>
    public sealed class EruptTelegraph : MonoBehaviour
    {
        [SerializeField] Transform closingRing;
        [SerializeField] Renderer glow;
        [SerializeField, Min(1f), Tooltip("Closing ring size at the start, relative to the eruption radius.")] float startScale = 2.6f;

        float _progress;
        bool _locked;
        Vector3 _ringBase;

        void Awake() => _ringBase = closingRing != null ? closingRing.localScale : Vector3.one;

        /// <summary>Sizes the telegraph and shows how close the eruption is (0 when it dives, 1 at impact).</summary>
        public void Show(float radius, float progress, bool locked)
        {
            transform.localScale = Vector3.one * radius;
            _progress = Mathf.Clamp01(progress);
            _locked = locked;
        }

        void LateUpdate()
        {
            if (closingRing != null)
                closingRing.localScale = _ringBase * Mathf.Lerp(startScale, 1f, _progress);
            if (glow != null)
                glow.enabled = !_locked || Mathf.Repeat(Time.time * 16f, 1f) < 0.6f;
        }
    }
}
