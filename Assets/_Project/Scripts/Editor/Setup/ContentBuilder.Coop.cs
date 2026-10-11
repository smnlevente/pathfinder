using Pathfinder.Game.Presentation;
using UnityEngine;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        // M8: local co-op. Each pilot wears a ring in their colour; the colour itself comes from CoopRules at runtime, so
        // the ring is drawn white and tinted per pilot.

        /// <summary>A thin ring on the floor under a pilot's ship, and the arc that fills around a wreck being revived.</summary>
        static GameObject BuildPilotRing()
        {
            var lineCore = Load<Texture2D>(TexturesDir + "/T_LineCore.png");
            var material = Additive("M_FX_PilotRing", new Color(1.7f, 1.7f, 1.7f), lineCore);
            var root = new GameObject("FX_PilotRing");
            var ring = Line("Ring", root.transform, material, 0.13f);
            ring.useWorldSpace = false;
            ring.loop = true;
            var arc = Line("ReviveArc", root.transform, material, 0.3f);
            arc.useWorldSpace = false;
            arc.enabled = false;
            SetFields(root.AddComponent<PilotMarker>(), ("ring", ring), ("reviveArc", arc));
            return SavePrefab(root, "FX");
        }
    }
}
