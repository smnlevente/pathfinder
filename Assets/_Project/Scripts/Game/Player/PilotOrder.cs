using System.Collections.Generic;
using Pathfinder.Game.Combat;

namespace Pathfinder.Game.Player
{
    /// <summary>The pilots in player order, for things each of them gets in turn (M8, local co-op).</summary>
    public static class PilotOrder
    {
        /// <summary>Every pilot, player 1 first, downed or not.</summary>
        public static List<PlayerShip> All()
        {
            var pilots = new List<PlayerShip>(CombatRegistry.Players);
            pilots.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            return pilots;
        }

        /// <summary>
        /// Every pilot, <paramref name="first"/> first and then the rest in player order: who picks equipment when, e.g.
        /// the one who opened a cache before the others.
        /// </summary>
        public static List<PlayerShip> StartingWith(PlayerShip first)
        {
            var pilots = All();
            if (first != null && pilots.Remove(first))
                pilots.Insert(0, first);
            return pilots;
        }
    }
}
