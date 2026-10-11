using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Keyboard and gamepad navigation for the menus, read from the UI action map: Navigate with key repeat, Submit and
    /// Cancel, each with its menu sound. It works while gameplay is paused. Mouse clicks are handled by the menus. A menu
    /// can listen to one local co-op player's devices only (M8): see <see cref="RestrictTo"/>.
    /// </summary>
    public sealed class MenuInput
    {
        const float RepeatDelay = 0.4f;
        const float RepeatInterval = 0.12f;
        const float Threshold = 0.5f;

        readonly InputAction _sharedNavigate, _sharedSubmit, _sharedCancel;
        InputAction _navigate, _submit, _cancel;
        InputActionAsset _own;
        Vector2Int _heldDirection;
        float _repeatTimer;

        public MenuInput()
        {
            var map = InputSystem.actions?.FindActionMap("UI");
            if (map == null)
                return;

            map.Enable();
            _navigate = _sharedNavigate = map.FindAction("Navigate");
            _submit = _sharedSubmit = map.FindAction("Submit");
            _cancel = _sharedCancel = map.FindAction("Cancel");
        }

        /// <summary>
        /// Listens to these devices only, e.g. the co-op player whose equipment choice is open (M8); null for every device
        /// again. The first restriction makes this menu its own copy of the controls.
        /// </summary>
        public void RestrictTo(InputDevice[] devices)
        {
            _heldDirection = Vector2Int.zero;
            if (devices == null)
            {
                _navigate = _sharedNavigate;
                _submit = _sharedSubmit;
                _cancel = _sharedCancel;
                return;
            }
            if (_own == null)
            {
                if (InputSystem.actions == null)
                    return;
                _own = Object.Instantiate(InputSystem.actions);
                _own.FindActionMap("UI")?.Enable();
            }
            _own.devices = devices;
            var map = _own.FindActionMap("UI");
            _navigate = map?.FindAction("Navigate");
            _submit = map?.FindAction("Submit");
            _cancel = map?.FindAction("Cancel");
        }

        /// <summary>Frees this menu's own copy of the controls, if it made one. Call when the menu goes away.</summary>
        public void Dispose()
        {
            if (_own == null)
                return;
            _own.Disable();
            Object.Destroy(_own);
            _own = null;
            RestrictTo(null);
        }

        public bool Submitted
        {
            get
            {
                bool pressed = _submit != null && _submit.WasPressedThisFrame();
                if (pressed)
                    SubmitDevice = _submit.activeControl?.device;
                return Sounded(pressed, Audio.SoundCue.UiConfirm);
            }
        }

        /// <summary>The device of the latest Submit, e.g. so the Hub knows which controller is player 1's (M8).</summary>
        public InputDevice SubmitDevice { get; private set; }
        public bool Cancelled => Sounded(_cancel != null && _cancel.WasPressedThisFrame(), Audio.SoundCue.UiBack);

        static bool Sounded(bool pressed, Audio.SoundCue cue)
        {
            if (pressed)
                Audio.GameAudio.Play(cue);
            return pressed;
        }

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
                Audio.GameAudio.Play(Audio.SoundCue.UiMove);
                return direction;
            }

            _repeatTimer -= Time.unscaledDeltaTime;
            if (_repeatTimer > 0f)
                return Vector2Int.zero;

            _repeatTimer = RepeatInterval;
            Audio.GameAudio.Play(Audio.SoundCue.UiMove);
            return direction;
        }
    }
}
