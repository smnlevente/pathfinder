using System;
using Pathfinder.Core.Meta;
using Pathfinder.Core.Run;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Meta;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// Drives a run through its phases (GDD §3, §9): galaxy map, level, next galaxy, victory or defeat. It owns the
    /// <see cref="RunState"/> and starts each chosen node through the <see cref="LevelController"/>.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public sealed class RunDirector : MonoBehaviour
    {
        public enum Phase { Map, Level, GalaxyCleared, Victory, Defeat }

        public static readonly string[] GalaxyNames = { "The Shattered Expanse", "The Graveyard Drift", "The Veil Nebula" };

        const float GalaxyClearedDelay = 2.5f;

        [SerializeField] CombatWorld world;
        [SerializeField] LevelController level;

        float _phaseTimer;
        bool _testLevel;

        public RunState Run { get; private set; }
        public Phase State { get; private set; }

        /// <summary>What the finished run banked into the meta progression. Set when the run ends.</summary>
        public RunRewards? BankedRewards { get; private set; }
        public LevelController Level => level;
        public string GalaxyName => GalaxyNames[Mathf.Clamp(Run?.GalaxyIndex ?? 0, 0, GalaxyNames.Length - 1)];

        /// <summary>Raised whenever the phase changes.</summary>
        public event Action<RunDirector> PhaseChanged;

        void Start()
        {
            // The Hub starts a session and seeds the world with it. Playing the Run scene directly starts one here.
            Run = RunSession.Current ?? RunSession.Begin(world.Seed);
            world.AllPlayersDown += OnAllPlayersDown;
            level.Exited += OnLevelExited;
            EnterMap();
        }

        void OnDestroy()
        {
            GameClock.Resume(this);
            if (world != null)
                world.AllPlayersDown -= OnAllPlayersDown;
            if (level != null)
                level.Exited -= OnLevelExited;
        }

        void Update()
        {
            if (State == Phase.GalaxyCleared)
            {
                _phaseTimer -= Time.unscaledDeltaTime;
                if (_phaseTimer <= 0f)
                    EnterMap();
            }
        }

        /// <summary>Picks the next node from the galaxy map and starts its level.</summary>
        public bool ChooseNode(MapNode node)
        {
            if (State != Phase.Map || !Run.CanEnter(node))
                return false;

            Run.Enter(node);
            var type = node.Type;
            bool ambush = false;
            if (type == MapNodeType.Mystery)
            {
                var outcome = Run.ResolveMystery(node);
                type = outcome.Type;
                ambush = outcome.Ambush;
            }

            _testLevel = false;
            GameClock.Resume(this);
            SetPhase(Phase.Level);
            level.Begin(Run, new LevelContext(type, Run.LevelSeed(node), Run.GalaxyIndex, node.Row, node.Type == MapNodeType.Mystery, ambush));
            return true;
        }

        /// <summary>
        /// Plays a level of the given type outside the map, then returns to the map without advancing the run.
        /// For tests and debug tools.
        /// </summary>
        public void StartTestLevel(MapNodeType type, int row = 1)
        {
            _testLevel = true;
            GameClock.Resume(this);
            SetPhase(Phase.Level);
            var seed = Run.Seed.Derive("test-level").Derive((int)type);
            level.Begin(Run, new LevelContext(type, seed, Run.GalaxyIndex, row, fromMystery: false, ambush: false));
        }

        void OnLevelExited(LevelController source)
        {
            level.End(FirstLivingLocalPilot());
            if (_testLevel)
            {
                _testLevel = false;
                EnterMap();
                return;
            }

            Run.CompleteLevel();

            if (Run.CurrentNode != null && Run.CurrentNode.Type == MapNodeType.Boss)
            {
                Run.CompleteGalaxy();
                if (Run.IsWon)
                {
                    BankRun(won: true);
                    Audio.GameAudio.Play(Audio.SoundCue.BossDefeated);
                    SetPhase(Phase.Victory);
                    return;
                }

                _phaseTimer = GalaxyClearedDelay;
                SetPhase(Phase.GalaxyCleared);
                return;
            }

            EnterMap();
        }

        void OnAllPlayersDown()
        {
            Run.MarkLost();
            BankRun(won: false);
            Audio.GameAudio.Play(Audio.SoundCue.RunOver);
            GameClock.Resume(this);
            SetPhase(Phase.Defeat);
        }

        /// <summary>Banks Dark Matter, Cores and 10% of Scraps into the persistent profile and saves it (GDD §3.1, §11.1).</summary>
        void BankRun(bool won)
        {
            if (BankedRewards.HasValue)
                return;

            int scraps = 0;
            foreach (var player in CombatRegistry.Players)
                scraps += player.TotalScrapsGained;

            BankedRewards = MetaProgression.Current.BankRun(Run.DarkMatter, Run.SingularityCores, scraps, won, Run.GalaxyIndex + 1);
            MetaProgression.Save();
        }

        void EnterMap()
        {
            // The map is a menu: the simulation holds still behind it.
            GameClock.Pause(this);
            SetPhase(Phase.Map);
        }

        void SetPhase(Phase phase)
        {
            State = phase;
            PhaseChanged?.Invoke(this);
        }

        static PlayerShip FirstLivingLocalPilot()
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal && player.IsAlive)
                    return player;
            }
            return null;
        }
    }
}
