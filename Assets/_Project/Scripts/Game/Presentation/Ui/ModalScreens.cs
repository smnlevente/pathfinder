using System.Collections.Generic;
using UnityEngine;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>Tracks full-screen menus (map, dialogs, choice cards) that are open, so the HUD can step aside.</summary>
    public static class ModalScreens
    {
        static readonly HashSet<object> s_open = new HashSet<object>();
        static readonly HashSet<object> s_blocking = new HashSet<object>();

        public static bool AnyOpen => s_open.Count > 0;

        /// <summary>A menu that uses Esc itself (a dialog, choice cards) is open, so Esc mustn't open the pause menu.</summary>
        public static bool BlocksPause => s_blocking.Count > 0;

        public static void SetOpen(object screen, bool open, bool blocksPause = true)
        {
            if (open)
            {
                s_open.Add(screen);
                if (blocksPause)
                    s_blocking.Add(screen);
            }
            else
            {
                s_open.Remove(screen);
                s_blocking.Remove(screen);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_open.Clear();
            s_blocking.Clear();
        }
    }
}
