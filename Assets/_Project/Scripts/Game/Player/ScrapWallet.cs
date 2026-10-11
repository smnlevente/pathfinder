namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Scraps held, and all gained this run. Each pilot has one; local co-op pilots share a single wallet (M8), so any of
    /// them can pick up, spend and see the same Scraps.
    /// </summary>
    public sealed class ScrapWallet
    {
        public int Scraps;
        public int TotalGained;
    }
}
