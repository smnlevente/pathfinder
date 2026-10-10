namespace Pathfinder.Game.Boot
{
    /// <summary>Scene names as registered in Build Settings (Boot must stay at index 0).</summary>
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string Hub = "Hub";
        public const string Run = "Run";
        public const string Survival = "Survival";

        /// <summary>The endless combat playground, for tuning and tests. Not reachable from the Hub.</summary>
        public const string Sandbox = "Sandbox";
    }
}
