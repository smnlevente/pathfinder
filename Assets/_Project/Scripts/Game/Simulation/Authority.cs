namespace Pathfinder.Game.Simulation
{
    /// <summary>
    /// The authority decides gameplay outcomes: damage, deaths, spawns, loot and random rolls (GDD §14.4).
    /// Single-player is always the authority. With netcode, this becomes "is host/server".
    /// </summary>
    public static class Authority
    {
        public static bool IsAuthority => true;
    }
}
