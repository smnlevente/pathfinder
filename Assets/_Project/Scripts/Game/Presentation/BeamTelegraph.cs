using Pathfinder.Game.Enemies;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A thin, flickering line along a beam's locked path while it charges (GDD §12.4), thickening and steadying as the
    /// discharge nears. Used by the Arc Drone. A line, not a bullet, so it never reads as something to dodge around.
    /// </summary>
    public sealed class BeamTelegraph : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing IBeamTelegraph, usually the enemy's brain.")] MonoBehaviour source;
        [SerializeField] LineRenderer line;
        [SerializeField, Min(0f)] float startWidth = 0.1f;
        [SerializeField, Min(0f)] float endWidth = 0.32f;
        [SerializeField] float height = 0.1f;

        IBeamTelegraph _beam;

        void Awake()
        {
            _beam = source as IBeamTelegraph;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.enabled = false;
        }

        void LateUpdate()
        {
            bool shown = _beam != null && _beam.ShowsBeam;
            if (!shown)
            {
                line.enabled = false;
                return;
            }

            float progress = Mathf.Clamp01(_beam.BeamProgress);
            // Flickers fast and faint at first, then holds steady just before the bolt.
            bool flickerOn = progress > 0.8f || Mathf.Repeat(Time.time * Mathf.Lerp(10f, 24f, progress), 1f) < 0.55f;
            line.enabled = true;
            line.widthMultiplier = Mathf.Lerp(startWidth, endWidth, progress) * (flickerOn ? 1f : 0.35f);
            line.SetPosition(0, PlaneMath.ToWorld(_beam.BeamStart, height));
            line.SetPosition(1, PlaneMath.ToWorld(_beam.BeamEnd, height));
        }
    }
}
