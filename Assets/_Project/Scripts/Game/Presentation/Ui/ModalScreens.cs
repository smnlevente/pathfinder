using System.Collections.Generic;
using UnityEngine;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>Tracks full-screen menus (map, dialogs, choice cards) that are open, so the HUD can step aside.</summary>
    public static class ModalScreens
    {
        static readonly HashSet<object> s_open = new HashSet<object>();
        static readonly HashSet<object> s_blocking = new HashSet<object>();
        static int s_blockingClosedFrame = -1;

        public static bool AnyOpen => s_open.Count > 0;

        /// <summary>
        /// A menu that uses Esc itself (a dialog, choice cards) is open, or closed this frame, so Esc mustn't open the pause
        /// menu: the press that closed the Shop must not also pause the game (playtest 4).
        /// </summary>
        public static bool BlocksPause => s_blocking.Count > 0 || Time.frameCount == s_blockingClosedFrame;

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
                if (s_blocking.Remove(screen))
                    s_blockingClosedFrame = Time.frameCount;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_open.Clear();
            s_blocking.Clear();
            s_blockingClosedFrame = -1;
        }
    }
}
