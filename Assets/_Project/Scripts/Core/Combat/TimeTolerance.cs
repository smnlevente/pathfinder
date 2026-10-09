namespace Pathfinder.Core.Combat
{
    internal static class TimeTolerance
    {
        /// <summary>
        /// Timers land on exact tick multiples (e.g. 0.1 s fire interval = 6 ticks at 60 Hz), where float
        /// rounding could otherwise shift an event by a whole tick.
        /// </summary>
        public const float Epsilon = 1e-5f;
    }
}
