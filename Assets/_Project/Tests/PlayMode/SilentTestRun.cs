using NUnit.Framework;
using UnityEngine;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>
    /// Mutes the game for the whole PlayMode run (playtest 5: batch test runs filled the room with music and explosions).
    /// Sounds still play and are checked; they just aren't heard. The previous volume comes back afterwards.
    /// </summary>
    [SetUpFixture]
    public class SilentTestRun
    {
        float _volume = 1f;

        [OneTimeSetUp]
        public void Mute()
        {
            _volume = AudioListener.volume;
            AudioListener.volume = 0f;
        }

        [OneTimeTearDown]
        public void Unmute() => AudioListener.volume = _volume;
    }
}
