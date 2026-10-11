using System;
using Pathfinder.Game.Enemies;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Asks the HUD to introduce a boss as it arrives (M7): the fight holds while the camera turns to it and its name card
    /// slides across. Levels and Survival call <see cref="Play"/>; the HUD plays the card. Tests switch it off, except the
    /// one that checks it.
    /// </summary>
    public static class BossIntro
    {
        public static bool Enabled { get; set; } = true;

        /// <summary>Raised when a boss should be introduced.</summary>
        public static event Action<Enemy> Requested;

        public static void Play(Enemy boss)
        {
            if (Enabled && boss != null)
                Requested?.Invoke(boss);
        }
    }
}
