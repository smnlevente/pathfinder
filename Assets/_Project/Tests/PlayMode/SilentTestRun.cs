using NUnit.Framework;
using UnityEngine;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>
    /// Settings for the whole PlayMode run. The game is muted (playtest 5: batch test runs filled the room with music and
    /// explosions); sounds still play and are checked, they just aren't heard. Settings changes stay in memory, so the
    /// player's preferences are never touched, and the game ignores window focus, so clicking elsewhere while tests run
    /// in the Editor doesn't pause or silence them. The game clock runs faster (see <see cref="Speed"/>). Everything is
    /// restored afterwards.
    /// </summary>
    [SetUpFixture]
    public class SilentTestRun
    {
        float _volume = 1f, _timeScale = 1f;
        bool _persist, _respondToFocus;

        /// <summary>
        /// How fast the game clock runs during the PlayMode tests: <c>PATHFINDER_TEST_SPEED</c>, 4 by default. The simulation
        /// steps at a fixed 60 Hz whatever the frame rate, so a faster clock only packs more ticks into each real second:
        /// tests count ticks, and the same seed plays out the same. Set it to 1 to watch tests at normal speed.
        /// </summary>
        public static float Speed { get; private set; } = 1f;

        [OneTimeSetUp]
        public void Mute()
        {
            string speed = System.Environment.GetEnvironmentVariable("PATHFINDER_TEST_SPEED");
            Speed = float.TryParse(speed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) && parsed > 0f
                ? parsed : 4f;
            _timeScale = Time.timeScale;
            Time.timeScale = Speed;

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
            Time.timeScale = _timeScale;
            AudioListener.volume = _volume;
            Pathfinder.Game.Settings.GameSettings.Persist = _persist;
            Pathfinder.Game.Settings.GameSettings.RespondToFocus = _respondToFocus;
        }
    }
}
