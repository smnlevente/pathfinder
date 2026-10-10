using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// Scene-level flow: the pause menu (single-player only), and what happens when the run ends. Interact restarts
    /// and Pause returns to the Hub.
    /// </summary>
    public sealed class RunFlow : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField, Tooltip("Optional. In the Run scene, victory also ends the run.")] RunDirector runDirector;

        InputAction _pause;
        InputAction _restart;

        public bool IsDefeat => world != null && world.IsOver;
        public bool IsVictory => runDirector != null && runDirector.Run != null && runDirector.Run.IsWon;
        public bool IsGameOver => IsDefeat || IsVictory;
        public bool IsPaused => GameClock.IsPausedBy(this);

        void OnEnable()
        {
            var map = InputSystem.actions?.FindActionMap("Gameplay");
            _pause = map?.FindAction("Pause");
            _restart = map?.FindAction("Interact");
        }

        void Update()
        {
            if (IsGameOver)
            {
                if (_restart != null && _restart.WasPressedThisFrame())
                    Restart();
                else if (_pause != null && _pause.WasPressedThisFrame())
                    ReturnToHub();
                return;
            }

            // Menus such as the galaxy map hold their own pause, so the pause key only toggles this one.
            if (_pause != null && _pause.WasPressedThisFrame())
            {
                if (IsPaused)
                    GameClock.Resume(this);
                else
                    GameClock.Pause(this);
            }
        }

        public void Restart()
        {
            RunSession.Clear();
            GameClock.ResumeAll();
            SceneManager.LoadScene(gameObject.scene.name);
        }

        public void ReturnToHub()
        {
            RunSession.Clear();
            GameClock.ResumeAll();
            SceneManager.LoadScene(SceneNames.Hub);
        }

        void OnDestroy() => GameClock.Resume(this);
    }
}
