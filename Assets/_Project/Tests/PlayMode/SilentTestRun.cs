using NUnit.Framework;
using UnityEngine;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>
    /// Settings for the whole PlayMode run. The game is muted (playtest 5: batch test runs filled the room with music and
    /// explosions); sounds still play and are checked, they just aren't heard. Settings changes stay in memory, so the
    /// player's preferences are never touched, and the game ignores window focus, so clicking elsewhere while tests run
    /// in the Editor doesn't pause or silence them. Everything is restored afterwards.
    /// </summary>
    [SetUpFixture]
    public class SilentTestRun
    {
        float _volume = 1f;
        bool _persist, _respondToFocus;

        [OneTimeSetUp]
        public void Mute()
        {
            _volume = AudioListener.volume;
            AudioListener.volume = 0f;
            _persist = Pathfinder.Game.Settings.GameSettings.Persist;
            _respondToFocus = Pathfinder.Game.Settings.GameSettings.RespondToFocus;
            Pathfinder.Game.Settings.GameSettings.Persist = false;
            Pathfinder.Game.Settings.GameSettings.RespondToFocus = false;
        }

        [OneTimeTearDown]
        public void Unmute()
        {
            AudioListener.volume = _volume;
            Pathfinder.Game.Settings.GameSettings.Persist = _persist;
            Pathfinder.Game.Settings.GameSettings.RespondToFocus = _respondToFocus;
        }
    }
}
