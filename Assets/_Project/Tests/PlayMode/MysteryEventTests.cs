using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pathfinder.Core.Random;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Pathfinder.Tests.PlayMode
{
    /// <summary>Mystery events (GDD §10.5): narrative choices with upsides and downsides.</summary>
    public class MysteryEventTests
    {
        string _savePath;
        RunDirector _director;
        PlayerShip _player;
        ChoiceDialog _dialog;
        ContentDatabase _content;

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

        IEnumerator LoadRun(ulong seed)
        {
            GameClock.ResumeAll();
            RunSession.Clear();
            CombatWorld.NextSeedOverride = new Seed(seed);
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;
            _director = Object.FindAnyObjectByType<RunDirector>();
            _dialog = Object.FindAnyObjectByType<ChoiceDialog>();
            _player = CombatRegistry.Players[0];
            _player.CommandSource = null;
            _content = Resources.FindObjectsOfTypeAll<ContentDatabase>()[0];
        }

        MysteryEventDefinition Event(string id) => _content.Events.First(e => e.Id == id);

        static IEnumerator WaitTicks(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator EventLevel_ShowsAChoice_ThenReturnsToTheMap()
        {
            yield return LoadRun(41);
            Assert.GreaterOrEqual(_content.Events.Length, 10, "The slice ships with ten events.");

            _director.StartTestLevel(MapNodeType.Mystery);
            yield return null;
            var level = _director.Level;
            Assert.IsNotNull(level.Event, "A Mystery event level plays an event.");
            Assert.IsTrue(_dialog.IsOpen);
            Assert.AreEqual(level.Event.DisplayName.ToUpperInvariant(), _dialog.Title);
            Assert.GreaterOrEqual(_dialog.Options.Count, 2, "Every event is a choice.");
            Assert.IsTrue(_dialog.Options.All(o => !string.IsNullOrEmpty(o.Detail)), "Each choice spells out what it does.");

            // Take a choice that neither fights nor offers equipment, then continue.
            level.DebugShowEvent(Event("event.earth-echo"));
            _dialog.Choose(0);
            Assert.IsTrue(_dialog.IsOpen, "The result is shown before leaving.");
            Assert.AreEqual("Continue", _dialog.Options[0].Label);
            _dialog.Choose(0);
            yield return WaitTicks(3);

            Assert.AreEqual(RunDirector.Phase.Map, _director.State, "After the event, the run moves on.");
        }

        [UnityTest]
        public IEnumerator Outcomes_ApplyToThePilot()
        {
            yield return LoadRun(42);
            var freighter = Event("event.derelict-freighter");
            int scraps = _player.Scraps;
            float hull = _player.Vitals.Hull;

            var result = MysteryEvents.Apply(freighter.Choices[0], _player, _director.Run, new Pcg32(1, 1));
            Assert.AreEqual(scraps + 45, _player.Scraps);
            Assert.AreEqual(hull - 15f, _player.Vitals.Hull, 0.01f, "Hull costs skip the shield.");
            Assert.AreEqual(2, result.Lines.Count, "The result lists what changed.");

            var singularity = Event("event.singularity");
            float maxHull = _player.Vitals.MaxHull;
            MysteryEvents.Apply(singularity.Choices[0], _player, _director.Run, new Pcg32(1, 1));
            Assert.AreEqual(30, _director.Run.DarkMatter);
            Assert.AreEqual(maxHull - 10f, _player.Vitals.MaxHull, 0.01f);
        }

        [UnityTest]
        public IEnumerator EventCosts_NeverKill()
        {
            yield return LoadRun(43);
            _player.DrainHull(_player.Vitals.Hull - 5f);
            var well = Event("event.gravity-well");

            MysteryEvents.Apply(well.Choices[0], _player, _director.Run, new Pcg32(1, 1));
            Assert.AreEqual(1f, _player.Vitals.Hull, 0.01f, "An event can hurt, but never end a run.");
            Assert.IsTrue(_player.IsAlive);
        }

        [UnityTest]
        public IEnumerator Requirements_DisableUnaffordableChoices()
        {
            yield return LoadRun(44);
            var toll = Event("event.remnant-toll");
            _player.LoseScraps(_player.Scraps);
            Assert.IsFalse(MysteryEvents.IsAvailable(toll.Choices[0], _player), "Paying the toll needs 35 Scraps.");
            Assert.IsTrue(MysteryEvents.IsAvailable(toll.Choices[1], _player), "Running the blockade is always possible.");
            _player.AddScraps(40);
            Assert.IsTrue(MysteryEvents.IsAvailable(toll.Choices[0], _player));

            var trader = Event("event.scrap-trader");
            Assert.IsFalse(MysteryEvents.IsAvailable(trader.Choices[0], _player), "Trading needs an item to trade.");
        }

        [UnityTest]
        public IEnumerator Ambush_StartsAFight_ThenOpensTheExit()
        {
            yield return LoadRun(45);
            _player.GodMode = true;
            _director.StartTestLevel(MapNodeType.Mystery);
            yield return null;
            var level = _director.Level;
            level.DebugShowEvent(Event("event.remnant-toll"));

            _dialog.Choose(1); // run the blockade
            Assert.AreEqual("Fight", _dialog.Options[0].Label);
            _dialog.Choose(0);
            yield return WaitTicks(5);

            Assert.IsFalse(_dialog.IsOpen);
            Assert.IsFalse(level.ExitOpen, "The exit stays shut until the ambush is beaten.");
            StringAssert.Contains("ambush", level.Objective.ToLowerInvariant());

            level.DebugCompleteObjective();
            Assert.IsTrue(level.ExitOpen);
        }

        [UnityTest]
        public IEnumerator Events_DontRepeatWithinARun()
        {
            yield return LoadRun(46);
            var run = _director.Run;
            foreach (var definition in _content.Events.Skip(1))
                run.MarkEventSeen(definition.Id);

            var picked = MysteryEvents.Pick(_content.Events, run, new Pcg32(3, 3));
            Assert.AreEqual(_content.Events[0], picked, "Only the unseen event is left.");

            run.MarkEventSeen(_content.Events[0].Id);
            Assert.IsNotNull(MysteryEvents.Pick(_content.Events, run, new Pcg32(3, 3)), "Once all are seen, any can return.");
        }
    }
}
