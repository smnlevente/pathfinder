using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Shows a Lancer's charge path while it winds up (GDD §12.4: red lines show charge paths). The renderer is a
    /// flat quad pointing along the Lancer's local +Z.
    /// </summary>
    public sealed class TelegraphLine : MonoBehaviour
    {
        [SerializeField] LancerBrain lancer;
        [SerializeField] Renderer line;
        [SerializeField, Min(0.05f)] float width = 0.45f;

        void LateUpdate()
        {
            bool show = lancer.Current == LancerBrain.State.WindUp;
            line.enabled = show;
            if (!show)
                return;

            // The line extends from the hull to the end of the charge, and thickens just before launch.
            float length = lancer.ChargeDistance / Mathf.Max(lancer.transform.localScale.z, 0.01f); // elites are scaled up
            float pulse = 1f + 0.25f * Mathf.Sin(Time.time * 30f);
            var t = line.transform;
            t.localPosition = new Vector3(0f, 0.04f, length * 0.5f);
            t.localScale = new Vector3(width * pulse, length, 1f);
        }
    }
}
