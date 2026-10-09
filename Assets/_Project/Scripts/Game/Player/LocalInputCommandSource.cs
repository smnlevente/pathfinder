using Pathfinder.Game.Combat;
using Pathfinder.Game.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Turns local keyboard/mouse or gamepad input into <see cref="ShipCommand"/>s. Presses are caught every
    /// frame and held until the next simulation tick consumes them, so a quick tap is never lost between
    /// fixed updates.
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

        void OnEnable()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("[LocalInput] No project-wide input actions are assigned (Project Settings > Input System Package).", this);
                enabled = false;
                return;
            }

            var map = actions.FindActionMap(GameplayMap, throwIfNotFound: true);
            _move = map.FindAction("Move", true);
            _aim = map.FindAction("Aim", true);
            _aimPoint = map.FindAction("AimPoint", true);
            _firePrimary = map.FindAction("FirePrimary", true);
            _fireSecondary = map.FindAction("FireSecondary", true);
            _dash = map.FindAction("Dash", true);
            _reload = map.FindAction("Reload", true);
            _interact = map.FindAction("Interact", true);
            map.Enable();

            // Without a mouse, keep the ship's current aim until the right stick is used.
            _aimMode = Mouse.current != null ? AimMode.Pointer : AimMode.Stick;
        }

        void Update()
        {
            if (GameClock.Paused)
            {
                // Presses made while paused must not fire the moment the game resumes.
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
            else if (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 1f || Mouse.current.leftButton.wasPressedThisFrame))
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
