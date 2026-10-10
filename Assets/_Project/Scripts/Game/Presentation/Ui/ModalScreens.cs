using System.Collections.Generic;
using UnityEngine;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>Tracks full-screen menus (map, dialogs, choice cards) that are open, so the HUD can step aside.</summary>
    public static class ModalScreens
    {
        static readonly HashSet<object> s_open = new HashSet<object>();

        public static bool AnyOpen => s_open.Count > 0;

        public static void SetOpen(object screen, bool open)
        {
            if (open)
                s_open.Add(screen);
            else
                s_open.Remove(screen);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_open.Clear();
    }
}
