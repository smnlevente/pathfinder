using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Screenshots of the run's level objects (GDD §10) and the galaxy map for visual review.</summary>
    public class RunScreenshotTests
    {
        RunDirector _director;
        PlayerShip _player;

        IEnumerator LoadRun(ulong seed)
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindAnyObjectByType<RunDirector>();
            _player = CombatRegistry.Players[0];
            _player.CommandSource = null;
            _player.GodMode = true;
        }

        [UnityTest, Category("Screenshot")]
        public IEnumerator CaptureLevelObjects()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun(21);

            // The in-game map is IMGUI, which batch mode can't capture, so draw the generated graph instead.
            Screenshots.SaveMapPreview(_director.Run, Path.Combine(directory, "run_map.png"));

            foreach (var type in new[] { MapNodeType.DarkMatter, MapNodeType.Repair, MapNodeType.Shop })
            {
                _director.StartTestLevel(type, row: 2);
                yield return null;
                var level = _director.Level;
                // Dark Matter: stand on a node so its harvest ring fills. Stations: stand between the station and the exit.
                _player.PlaceForLevel(type == MapNodeType.DarkMatter ? level.UnharvestedNodes.First() : new Vector2(2.5f, 5f));
                for (int i = 0; i < 30; i++)
                    yield return new WaitForFixedUpdate();
                yield return null;
                Screenshots.CaptureCamera(Path.Combine(directory, $"run_{type.ToString().ToLowerInvariant()}.png"));

                level.DebugTakeExit();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (_director.State != RunDirector.Phase.Map && Time.realtimeSinceStartup < deadline)
                    yield return null;
            }
        }
    }
}
