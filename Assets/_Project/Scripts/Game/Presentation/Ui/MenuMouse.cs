using System.Collections.Generic;
using UnityEngine;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>
    /// Whether the mouse may work the menus (playtest 8). A menu that belongs to one local co-op player, such as player 2's
    /// Shop, listens only to that player's controller; if the mouse isn't theirs, the menu blocks it here, so player 1
    /// can't hover or click their way into someone else's shopping. While blocked, the cursor is hidden.
    /// </summary>
    public static class MenuMouse
    {
        static readonly HashSet<object> s_blockers = new HashSet<object>();

        public static bool Blocked => s_blockers.Count > 0;

        /// <summary>Blocks (or unblocks) the mouse while <paramref name="menu"/> is open.</summary>
        public static void Set(object menu, bool blocked)
        {
            if (blocked)
                s_blockers.Add(menu);
            else
                s_blockers.Remove(menu);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_blockers.Clear();
    }
}
