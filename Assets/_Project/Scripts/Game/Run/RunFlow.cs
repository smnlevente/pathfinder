using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pathfinder.Game.Run
{
    /// <summary>
    /// Level-level flow for M1: pause (single-player only) and restart after the ship is destroyed. M5
    /// replaces this with the real run-end flow (summary, back to the Hub).
    /// </summary>
    public sealed class RunFlow : MonoBehaviour
    {
        [SerializeField] CombatWorld world;

        InputAction _pause;
        InputAction _restart;

        public bool IsGameOver => world != null && world.IsOver;

        void OnEnable()
        {
            var map = InputSystem.actions?.FindActionMap("Gameplay");
            _pause = map?.FindAction("Pause");
            _restart = map?.FindAction("Interact");
        }

        void Update()
        {
            if (_pause != null && _pause.WasPressedThisFrame() && !IsGameOver)
                GameClock.Paused = !GameClock.Paused;

            if (IsGameOver && _restart != null && _restart.WasPressedThisFrame())
                Restart();
        }

        public void Restart()
        {
            GameClock.Paused = false;
            SceneManager.LoadScene(gameObject.scene.name);
        }

        void OnDestroy() => GameClock.Paused = false;
    }
}
