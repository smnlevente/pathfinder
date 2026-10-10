using UnityEngine;

namespace Pathfinder.Game.Enemies
{
    /// <summary>An enemy that winds up a straight charge and shows its path first (GDD §12.4). Read by the telegraph visual.</summary>
    public interface IChargeTelegraph
    {
        bool ShowsCharge { get; }
        Vector2 ChargeStart { get; }
        Vector2 ChargeDirection { get; }
        float ChargeLength { get; }

        /// <summary>Wind-up progress, 0 to 1.</summary>
        float ChargeProgress { get; }
    }
}
