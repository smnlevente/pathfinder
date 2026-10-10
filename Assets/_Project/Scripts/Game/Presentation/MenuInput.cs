using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Keyboard and gamepad navigation for the placeholder IMGUI menus, read from the UI action map: Navigate with
    /// key repeat, Submit and Cancel. It works while gameplay is paused. Mouse clicks are handled by the menus.
    /// </summary>
    public sealed class MenuInput
    {
        const float RepeatDelay = 0.4f;
        const float RepeatInterval = 0.12f;
        const float Threshold = 0.5f;

        readonly InputAction _navigate;
        readonly InputAction _submit;
        readonly InputAction _cancel;
        Vector2Int _heldDirection;
        float _repeatTimer;

        public MenuInput()
        {
            var map = InputSystem.actions?.FindActionMap("UI");
            if (map == null)
                return;

            map.Enable();
            _navigate = map.FindAction("Navigate");
            _submit = map.FindAction("Submit");
            _cancel = map.FindAction("Cancel");
        }

        public bool Submitted => _submit != null && _submit.WasPressedThisFrame();
        public bool Cancelled => _cancel != null && _cancel.WasPressedThisFrame();

        /// <summary>Call once per frame. Returns a step of -1, 0 or +1 per axis, with key repeat while held.</summary>
        public Vector2Int ReadStep()
        {
            if (_navigate == null)
                return Vector2Int.zero;

            Vector2 raw = _navigate.ReadValue<Vector2>();
            var direction = new Vector2Int(
                raw.x > Threshold ? 1 : raw.x < -Threshold ? -1 : 0,
                raw.y > Threshold ? 1 : raw.y < -Threshold ? -1 : 0);

            if (direction == Vector2Int.zero)
            {
                _heldDirection = Vector2Int.zero;
                return Vector2Int.zero;
            }

            if (direction != _heldDirection)
            {
                _heldDirection = direction;
                _repeatTimer = RepeatDelay;
                return direction;
            }

            _repeatTimer -= Time.unscaledDeltaTime;
            if (_repeatTimer > 0f)
                return Vector2Int.zero;

            _repeatTimer = RepeatInterval;
            return direction;
        }
    }
}
