using System.Collections;
using System.IO;
using NUnit.Framework;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>The Hub (GDD §11.2, §12.1): pages, launching a run, and screenshots of each page.</summary>
    public class HubTests
    {
        string _savePath;

        [SetUp]
        public void UseTemporarySave()
        {
            _savePath = Path.Combine(Application.temporaryCachePath, $"meta-test-{System.Guid.NewGuid():N}.json");
            MetaProgression.SavePath = _savePath;
        }

        [TearDown]
        public void DeleteTemporarySave()
        {
            MetaProgression.SavePath = null;
            if (File.Exists(_savePath))
                File.Delete(_savePath);
        }

        static IEnumerator LoadHub()
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            yield return SceneManager.LoadSceneAsync(SceneNames.Hub, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OptionsPage_FitsA1080pScreen_AndScrollsToItsLastRow()
        {
            yield return LoadHub();
            var menu = Object.FindAnyObjectByType<HubMenu>();
            menu.Open(HubMenu.Page.Options);
            yield return null;
            yield return null;

            // The panel starts 300 units from the top of the 1080-unit canvas (playtest 5: Back ran off the bottom).
            Assert.LessOrEqual(menu.Panel.rect.height, 1080f - 300f - 30f + 1f, "The options panel fits on a 1080p screen.");
            var back = menu.Rows[menu.Rows.Count - 1].Rect;
            menu.List.ScrollTo(back);
            yield return null;
            Assert.IsTrue(menu.List.IsVisible(back), "Scrolling brings the last row (Back) into view.");
        }

        [UnityTest]
        public IEnumerator StartRun_LaunchesTheRunWithTheChosenLoadout()
        {
            yield return LoadHub();
            var menu = Object.FindAnyObjectByType<HubMenu>();
            Assert.AreEqual(HubMenu.Page.Main, menu.CurrentPage);

            menu.StartRun();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != SceneNames.Run && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;

            Assert.AreEqual(SceneNames.Run, SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, CombatRegistry.Players.Count);
            Assert.AreEqual(RunSession.Current.Loadout.ShipId, CombatRegistry.Players[0].Definition.Id, "The run flies the ship picked in the Hub.");
        }

        /// <summary>Survival took the combat sandbox's place in the Hub (playtest 6): same loadout page, its own launch.</summary>
        [UnityTest]
        public IEnumerator Survival_ChoosesALoadoutAndLaunches()
        {
            yield return LoadHub();
            var menu = Object.FindAnyObjectByType<HubMenu>();
            Assert.AreEqual(Pathfinder.Game.Localization.Loc.T("hub.survival"), menu.Rows[1].LabelText, "Survival is second, under Start Run.");

            menu.OpenLoadout(survival: true);
            Assert.AreEqual(HubMenu.Page.Loadout, menu.CurrentPage);
            Assert.AreEqual(Pathfinder.Game.Localization.Loc.T("hub.loadout.launch_survival"), menu.Rows[System.Linq.Enumerable.ToList(menu.LoadoutRows).IndexOf("launch")].LabelText);

            menu.StartSurvival();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (SceneManager.GetActiveScene().name != SceneNames.Survival && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;

            Assert.AreEqual(SceneNames.Survival, SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindAnyObjectByType<SurvivalController>());
            Assert.AreEqual(RunSession.Current.Loadout.ShipId, CombatRegistry.Players[0].Definition.Id, "Survival flies the ship picked in the Hub.");
        }

        [UnityTest, Category("Screenshot")]
        public IEnumerator CapturePages()
        {
            string directory = Screenshots.Directory();
            MetaProgression.Current.BankRun(darkMatter: 120, singularityCores: 2, scrapsCollected: 0, won: false, galaxyReached: 1);
            yield return LoadHub();
            var menu = Object.FindAnyObjectByType<HubMenu>();

            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "hub_main.png"));
            menu.Open(HubMenu.Page.Loadout);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "hub_loadout.png"));
            menu.Open(HubMenu.Page.Lab);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "hub_lab.png"));
            menu.Open(HubMenu.Page.Options);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "hub_options.png"));
            menu.List.ScrollTo(menu.Rows[menu.Rows.Count - 1].Rect);
            yield return null;
            Screenshots.CaptureWithUi(Path.Combine(directory, "hub_options_scrolled.png"));
        }
    }
}
