namespace Pathfinder.Game.Enemies
{
    /// <summary>
    /// An enemy that marks the floor its attack will cover before it lands (GDD §12.4): a Scorcher's flame cone, a
    /// Crusher's slam ring. Read by the area telegraph visual.
    /// </summary>
    public interface IAreaTelegraph
    {
        bool ShowsArea { get; }

        /// <summary>Reach from the enemy's centre, in units.</summary>
        float AreaRadius { get; }

        /// <summary>Full angle in degrees, centred on the enemy's heading. 360 is a circle.</summary>
        float AreaArc { get; }

        /// <summary>0 when the wind-up starts, 1 when the attack lands.</summary>
        float AreaProgress { get; }
    }
}
