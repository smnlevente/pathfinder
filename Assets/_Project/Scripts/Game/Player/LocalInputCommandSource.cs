using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Turns local keyboard/mouse or gamepad input into <see cref="ShipCommand"/>s. Presses are caught every
    /// frame and held until the next simulation tick consumes them, so a quick tap is never lost between
    /// fixed updates. Each pilot has their own copy of the controls, listening only to their devices (M8, local co-op):
    /// player 1 alone uses everything; with company, player 1 keeps what nobody else claimed.
    /// </summary>
    public sealed class LocalInputCommandSource : MonoBehaviour, IShipCommandSource
    {
        const string GameplayMap = "Gameplay";
        const float StickDeadZone = 0.3f;

        enum AimMode { Pointer, Stick }

        InputAction _move, _aim, _aimPoint, _firePrimary, _fireSecondary, _dash, _reload, _interact;
        bool _dashLatched, _reloadLatched, _interactLatched, _secondaryLatched;
        AimMode _aimMode = AimMode.Pointer;
        Vector2 _stickAim;
        Camera _camera;
        InputActionAsset _actions;
        int _playerIndex, _devicesVersion = -1;
        bool _usesMouse = true;

        /// <summary>Which local player this ship belongs to, so it listens to their devices only.</summary>
        public int PlayerIndex => _playerIndex;

        /// <summary>Whether this pilot aims with the mouse (player 1) rather than a stick.</summary>
        public bool UsesMouse => _usesMouse && _aimMode == AimMode.Pointer;

        public void Bind(int playerIndex)
        {
            _playerIndex = playerIndex;
            ApplyDevices();
        }

        void OnEnable()
        {
            var shared = InputSystem.actions;
            if (shared == null)
            {
                Debug.LogError("[LocalInput] No project-wide input actions are assigned (Project Settings > Input System Package).", this);
                enabled = false;
                return;
            }

            // A copy of the project's controls, so this pilot can listen to their own devices (M8).
            _actions = Instantiate(shared);
            _actions.name = $"{shared.name} ({name})";
            var map = _actions.FindActionMap(GameplayMap, throwIfNotFound: true);
            _move = map.FindAction("Move", true);
            _aim = map.FindAction("Aim", true);
            _aimPoint = map.FindAction("AimPoint", true);
            _firePrimary = map.FindAction("FirePrimary", true);
            _fireSecondary = map.FindAction("FireSecondary", true);
            _dash = map.FindAction("Dash", true);
            _reload = map.FindAction("Reload", true);
            _interact = map.FindAction("Interact", true);
            map.Enable();
            ApplyDevices();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            if (_actions != null)
            {
                _actions.Disable();
                Destroy(_actions);
                _actions = null;
            }
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change) => _devicesVersion = -1;

        /// <summary>Listens to this pilot's devices: see <see cref="LocalPlayers.DevicesFor"/>.</summary>
        void ApplyDevices()
        {
            if (_actions == null)
                return;
            var devices = LocalPlayers.DevicesFor(_playerIndex);
            // Null must stay null (every device). Assigning a null array converts it to an empty device list first, and a
            // pilot alone then listened to nothing at all (playtest 8).
            if (devices == null)
                _actions.devices = null;
            else
                _actions.devices = devices;
            _devicesVersion = LocalPlayers.Version;

            _usesMouse = Mouse.current != null && (devices == null || System.Array.IndexOf(devices, Mouse.current) >= 0);
            // Without a mouse, keep the ship's current aim until the right stick is used.
            _aimMode = _usesMouse ? AimMode.Pointer : AimMode.Stick;
        }

        bool _pausedLastFrame;

        void Update()
        {
            if (_devicesVersion != LocalPlayers.Version)
                ApplyDevices();

            bool pausedLastFrame = _pausedLastFrame;
            _pausedLastFrame = GameClock.Paused;
            if (GameClock.Paused || pausedLastFrame)
            {
                // Presses made while paused, including the menu confirm that unpaused the game, must not fire on resume.
                _dashLatched = _reloadLatched = _interactLatched = _secondaryLatched = false;
                return;
            }

            _dashLatched |= _dash.WasPressedThisFrame();
            _reloadLatched |= _reload.WasPressedThisFrame();
            _interactLatched |= _interact.WasPressedThisFrame();
            _secondaryLatched |= _fireSecondary.WasPressedThisFrame();

            // Whichever aiming device was used last wins.
            Vector2 stick = _aim.ReadValue<Vector2>();
            if (stick.sqrMagnitude > StickDeadZone * StickDeadZone)
            {
                _aimMode = AimMode.Stick;
                _stickAim = stick.normalized;
            }
            else if (_usesMouse && Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 1f || Mouse.current.leftButton.wasPressedThisFrame))
            {
                _aimMode = AimMode.Pointer;
            }
        }

        public ShipCommand ReadCommand(PlayerShip ship)
        {
            if (!enabled)
                return default;

            var command = new ShipCommand
            {
                Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f),
                Aim = ReadAim(ship),
                FirePrimary = _firePrimary.IsPressed(),
                FireSecondary = _fireSecondary.IsPressed() || _secondaryLatched,
                Dash = _dashLatched,
                Reload = _reloadLatched,
                Interact = _interactLatched,
            };

            _dashLatched = _reloadLatched = _interactLatched = _secondaryLatched = false;
            return command;
        }

        Vector2 ReadAim(PlayerShip ship)
        {
            if (_aimMode == AimMode.Stick)
                return _stickAim;

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return Vector2.zero;

            // Aim where the cursor meets the plane that projectiles fly in.
            var ray = _camera.ScreenPointToRay(_aimPoint.ReadValue<Vector2>());
            var plane = new Plane(Vector3.up, new Vector3(0f, ProjectileSystem.VisualHeight, 0f));
            if (!plane.Raycast(ray, out float distance))
                return Vector2.zero;

            Vector2 toCursor = PlaneMath.ToPlane(ray.GetPoint(distance)) - ship.Position;
            return toCursor.sqrMagnitude > 0.01f ? toCursor.normalized : Vector2.zero;
        }
    }
}
