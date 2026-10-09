using UnityEngine;

namespace Pathfinder.Game.Simulation
{
    /// <summary>
    /// Gameplay runs on the fixed tick, while rendering runs every frame. Visuals lerp between the previous
    /// and current tick by this factor, so movement looks smooth at any frame rate.
    /// </summary>
    public static class SimInterpolation
    {
        public static float Alpha =>
            Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
    }
}
