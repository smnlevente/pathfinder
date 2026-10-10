using System.Collections;
using System.IO;
using NUnit.Framework;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Presentation.Ui;
using Pathfinder.Game.Run;
using Pathfinder.Game.Settings;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The pause menu and options (GDD §12.1).</summary>
    public class PauseMenuTests : SandboxTestBase
    {
        string _savePath;

        [SetUp]
        public void Isolate()
        {
            GameSettings.Persist = false; // never touch the player's real preferences
            GameSettings.ResetToDefaults();
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta-test-{System.Guid.NewGuid():N}.json");
            MetaProgression.SavePath = _savePath;
        }

        [TearDown]
        public void Restore()
        {
            GameSettings.ResetToDefaults();
            GameSettings.Persist = true;
            MetaProgression.SavePath = null;
            if (File.Exists(_savePath))
                File.Delete(_savePath);
        }

        [UnityTest]
        public IEnumerator Pausing_OpensTheMenu_AndResumeCloses()
        {
            yield return LoadRun();
            var flow = Object.FindAnyObjectByType<RunFlow>();
            var menu = Object.FindAnyObjectByType<PauseMenu>();

            flow.SetPaused(true);
            yield return null;
            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(GameClock.Paused, "The game holds still behind the pause menu.");
            Assert.AreEqual(PauseMenu.Page.Main, menu.CurrentPage);

            menu.ShowPage(PauseMenu.Page.Options);
            yield return null;
            menu.Back();
            Assert.AreEqual(PauseMenu.Page.Main, menu.CurrentPage, "Back from a page returns to the main page.");

            menu.Back();
            yield return null;
            Assert.IsFalse(menu.IsOpen, "Back on the main page resumes.");
            Assert.IsFalse(GameClock.Paused);
        }

        [UnityTest]
        public IEnumerator Options_ChangeSettings()
        {
            yield return LoadRun();
            var master = SettingsOptions.All[0];
            Assert.AreEqual("80%", master.Value());

            master.Change(-1, false);
            Assert.AreEqual(0.7f, GameSettings.MasterVolume, 0.001f, "Arrows step volumes by 10%.");
            master.Change(1, false);
            master.Change(1, false);
            master.Change(1, false);
            Assert.AreEqual(1f, GameSettings.MasterVolume, 0.001f, "Arrows stop at 100%.");
            master.Change(1, true);
            Assert.AreEqual(0f, GameSettings.MasterVolume, 0.001f, "Enter or click wraps past 100% to off.");
            Assert.AreEqual("OFF", master.Value());

            var shake = SettingsOptions.All[3];
            shake.Change(-1, false);
            Assert.AreEqual(0.75f, GameSettings.ScreenShake, 0.001f, "Screen shake steps by 25%.");
        }

        [UnityTest, Category("Screenshot")]
        public IEnumerator CapturePages()
        {
            string directory = Screenshots.Directory();
            yield return LoadRun();
            var menu = Object.FindAnyObjectByType<PauseMenu>();
            Object.FindAnyObjectByType<RunFlow>().SetPaused(true);
            yield return null;
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "pause_main.png"));
            menu.ShowPage(PauseMenu.Page.Options);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "pause_options.png"));
            menu.ShowPage(PauseMenu.Page.Controls);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "pause_controls.png"));
        }

        [UnityTest]
        public IEnumerator AbandoningARun_BanksItAsALoss()
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            yield return SceneManager.LoadSceneAsync(SceneNames.Run, LoadSceneMode.Single);
            yield return null;
            int runs = MetaProgression.Current.RunsPlayed;

            Object.FindAnyObjectByType<RunFlow>().SetPaused(true);
            yield return null;
            Object.FindAnyObjectByType<PauseMenu>().Leave();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != SceneNames.Hub && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(SceneNames.Hub, SceneManager.GetActiveScene().name);
            Assert.AreEqual(runs + 1, MetaProgression.Current.RunsPlayed, "An abandoned run still counts, and is banked.");
        }
    }
}
