using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>An enemy that charges an instant beam along a locked line and shows the line first, e.g. an Arc Drone.</summary>
    public interface IBeamTelegraph
    {
        bool ShowsBeam { get; }
        Vector2 BeamStart { get; }
        Vector2 BeamEnd { get; }

        /// <summary>Charge progress, 0 to 1.</summary>
        float BeamProgress { get; }
    }
}
