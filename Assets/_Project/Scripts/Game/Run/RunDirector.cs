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
        public string GalaxyName => Localization.Loc.T($"galaxy.{Mathf.Clamp(Run?.GalaxyIndex ?? 0, 0, RunState.GalaxyCount - 1)}");

        /// <summary>Raised whenever the phase changes.</summary>
        public event Action<RunDirector> PhaseChanged;

        float _startTime;
        string _lastHitBy;

        void Start()
        {
            // The Hub starts a session and seeds the world with it. Playing the Run scene directly starts one here.
            Run = RunSession.Current ?? RunSession.Begin(world.Seed);
            _startTime = Time.realtimeSinceStartup;
            CombatEvents.Damaged += OnAnyDamaged;
            world.AllPlayersDown += OnAllPlayersDown;
            level.Exited += OnLevelExited;
            EnterMap();
        }

        void OnDestroy()
        {
            CombatEvents.Damaged -= OnAnyDamaged;
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
                _phaseTimer -= Time.deltaTime; // the game never scales time; tests do, to run faster
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
            PlayLevelMusic();
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
            PlayLevelMusic();
        }

        /// <summary>Gives up the run from the pause menu: it counts as lost, and what it earned is banked.</summary>
        public void Abandon()
        {
            if (Run == null || Run.IsOver)
                return;
            Run.MarkLost();
            BankRun(won: false);
        }

        /// <summary>Each galaxy has its own track, the same on its map and in its levels so it never restarts between them; bosses have theirs.</summary>
        void PlayLevelMusic() =>
            Audio.GameAudio.PlayMusic(level.Context.Type == MapNodeType.Boss ? Audio.MusicTrack.Boss : Audio.SoundLibrary.ForGalaxy(level.Context.GalaxyIndex));

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

                // Every galaxy starts at full hull (M7).
                foreach (var player in CombatRegistry.Players)
                {
                    if (player.IsAlive)
                        player.Heal(player.Vitals.MaxHull);
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

            var pilot = CombatRegistry.Players.Count > 0 ? CombatRegistry.Players[0] : null;
            RunLog.Append(new RunLog.Entry
            {
                timeUtc = DateTime.UtcNow.ToString("u"),
                mode = "run",
                seed = RunLog.SeedCode(Run.Seed),
                ship = pilot != null ? pilot.Definition.Id : Run.Loadout.ShipId,
                primary = pilot != null ? pilot.PrimaryWeapon.Id : Run.Loadout.PrimaryId,
                secondary = pilot != null && pilot.SecondaryWeapon != null ? pilot.SecondaryWeapon.Id : Run.Loadout.SecondaryId,
                won = won,
                galaxyReached = Run.GalaxyIndex + 1,
                levelsCleared = Run.LevelsCleared,
                darkMatter = Run.DarkMatter,
                singularityCores = Run.SingularityCores,
                scrapsCollected = scraps,
                minutes = (Time.realtimeSinceStartup - _startTime) / 60f,
                killedBy = won ? string.Empty : _lastHitBy ?? "abandoned",
            });
        }

        /// <summary>Remembers what last hurt a pilot, for the run log's "killed by".</summary>
        void OnAnyDamaged(CombatEntity target, DamageInfo info, Pathfinder.Core.Combat.DamageResult result)
        {
            if (target is not PlayerShip || result.TotalDamage <= 0f || info.SourceTeam != Pathfinder.Core.Combat.Team.Enemy)
                return;
            _lastHitBy = "unknown";
            foreach (var entity in CombatRegistry.OnTeam(Pathfinder.Core.Combat.Team.Enemy))
            {
                if (entity.EntityId == info.SourceId && entity is Enemies.Enemy enemy)
                    _lastHitBy = enemy.Definition.DisplayName;
            }
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
            // Levels pick their own track once they've begun (the boss has its own). The map plays the galaxy's track;
            // once a galaxy is cleared, the next galaxy's track comes in under the "jumping" screen.
            if (phase != Phase.Level)
                Audio.GameAudio.PlayMusic(phase is Phase.Victory or Phase.Defeat ? Audio.MusicTrack.None
                    : Run != null ? Audio.SoundLibrary.ForGalaxy(Run.GalaxyIndex) : Audio.MusicTrack.Combat);
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
