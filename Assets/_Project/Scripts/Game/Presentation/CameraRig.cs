using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Top-down follow camera (GDD §7.1, §13.1). It follows the local pilot with a slight lead toward their
    /// aim, stays inside the arena, and shakes when they take damage or something big blows up. With several local co-op
    /// pilots (M8) it frames them all, wrecks included, pulling back as they spread out, as far as the whole arena: one
    /// shared screen, never split. Presentation only.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField, Range(10f, 89f)] float pitch = 60f;
        [SerializeField, Min(1f)] float distance = 24f;
        [SerializeField, Min(0f), Tooltip("How far the view leads toward the aim direction.")] float aimLead = 2f;
        [SerializeField, Min(0.01f)] float smoothTime = 0.12f;
        [SerializeField, Tooltip("How far the focus point may get from the arena edge, per axis.")] Vector2 edgeMargin = new Vector2(12f, 7f);

        [Header("Co-op")]
        [SerializeField, Min(0f), Tooltip("Room kept around the pilots when the view pulls back to fit them all.")] float fitMargin = 4.5f;
        [SerializeField, Min(0.01f)] float zoomSmoothTime = 0.4f;

        [Header("Shake")]
        [SerializeField, Min(0f)] float hullHitShake = 0.35f;
        [SerializeField, Min(0f)] float shieldHitShake = 0.12f;
        [SerializeField, Min(0f)] float killShake = 0.07f;
        [SerializeField, Min(0f)] float eliteKillShake = 0.2f;
        [SerializeField, Min(0f)] float bossKillShake = 0.7f;
        [SerializeField, Min(0f), Tooltip("A low rumble while the wormhole draws the ship in.")] float wormholePullShake = 0.1f;
        [SerializeField, Min(0f)] float shakeDecay = 2.5f;
        [SerializeField, Min(0f)] float shakeFrequency = 25f;

        static CameraRig s_active;

        Vector3 _focus;
        bool _hasFocus, _returning;
        Vector3 _velocity;
        float _shake;
        readonly System.Collections.Generic.List<PlayerShip> _subscribed = new System.Collections.Generic.List<PlayerShip>();
        readonly System.Collections.Generic.List<PlayerShip> _locals = new System.Collections.Generic.List<PlayerShip>();
        int _placements = -1;
        float _distance, _zoomVelocity, _targetDistance;
        Camera _camera;

        /// <summary>The point the camera looks at, before shake. For tests.</summary>
        public Vector3 Focus => _focus;

        /// <summary>How far back the camera is now, and how far it's heading: further when co-op pilots spread out. For tests.</summary>
        public float Distance => _distance > 0f ? _distance : distance;
        public float TargetDistance => _targetDistance > 0f ? _targetDistance : distance;

        /// <summary>
        /// Somewhere the camera should look instead of at the pilots, e.g. a boss making its entrance (M7). Null to follow
        /// the pilots again; the camera glides both ways.
        /// </summary>
        public static Vector3? Spotlight { get; set; }

        /// <summary>Kicks the camera, e.g. for an eruption. Kicks don't add up: the strongest wins. Safe without a rig.</summary>
        public static void Kick(float amount)
        {
            if (s_active != null)
                s_active._shake = Mathf.Max(s_active._shake, amount);
        }

        void OnEnable()
        {
            s_active = this;
            CombatEvents.Killed += OnKilled;
        }

        void OnDisable()
        {
            if (s_active == this)
                s_active = null;
            CombatEvents.Killed -= OnKilled;
        }

        void OnKilled(CombatEntity target, DamageInfo info)
        {
            if (target is Enemy enemy)
                Kick(enemy.Radius > 1f ? bossKillShake : enemy.IsElite ? eliteKillShake : killShake);
        }

        void LateUpdate()
        {
            if (!TryGetFocus(out var target))
                return;

            Vector3 desired = ClampToArena(Spotlight ?? target);
            // Pan slowly to a spotlight and back again; otherwise follow the ship briskly.
            if (Spotlight.HasValue)
                _returning = true;
            else if (_returning && (_focus - desired).sqrMagnitude < 1f)
                _returning = false;
            _focus = Vector3.SmoothDamp(_focus, desired, ref _velocity, _returning ? smoothTime * 3f : smoothTime);
            _distance = Mathf.SmoothDamp(_distance, _targetDistance, ref _zoomVelocity, zoomSmoothTime);

            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * _distance + ShakeOffset(), rotation);
        }

        bool TryGetFocus(out Vector3 focus)
        {
            focus = Vector3.zero;
            _locals.Clear();
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal)
                    _locals.Add(player);
            }

            Subscribe(_locals);
            var firstLocal = _locals.Count > 0 ? _locals[0] : null;
            // The rumble only while the pull is actually playing: never behind the map or a menu.
            if (firstLocal != null && firstLocal.IsPulled && !GameClock.Paused)
                Kick(wormholePullShake);
            if (_locals.Count == 0)
                return false;

            if (_locals.Count == 1)
            {
                // Alone: the ship, leading a little toward the aim.
                var player = firstLocal;
                focus = player.transform.position + PlaneMath.ToWorld(player.AimDirection * (player.IsAlive ? aimLead : 0f));
                _targetDistance = distance;
            }
            else
            {
                // Co-op: the middle of everyone, back far enough to see them all (M8).
                Vector3 min = _locals[0].transform.position, max = min;
                foreach (var player in _locals)
                {
                    min = Vector3.Min(min, player.transform.position);
                    max = Vector3.Max(max, player.transform.position);
                }
                focus = (min + max) * 0.5f;
                _targetDistance = Mathf.Clamp(DistanceToFit((max.x - min.x) * 0.5f + fitMargin, (max.z - min.z) * 0.5f + fitMargin), distance, MaxDistance());
            }

            // Cut, don't slide, when the ship is placed at a new level's entry (playtest 5).
            bool placed = firstLocal.Placements != _placements;
            _placements = firstLocal.Placements;
            if (!_hasFocus || placed)
            {
                _distance = _targetDistance;
                _zoomVelocity = 0f;
                _focus = ClampToArena(focus);
                _velocity = Vector3.zero;
                _hasFocus = true;
            }
            return true;
        }

        /// <summary>
        /// How far back the camera must be to see this far either side of its focus, and this far nearer and further. Near
        /// the bottom of the screen the view covers less floor than at the top, so that side sets the depth.
        /// </summary>
        float DistanceToFit(float halfWidth, float halfDepth)
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
            float fov = (_camera != null ? _camera.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad;
            float aspect = _camera != null ? _camera.aspect : 16f / 9f;
            float tilt = pitch * Mathf.Deg2Rad;
            float forWidth = halfWidth / (Mathf.Tan(fov) * aspect);
            float forDepth = halfDepth * Mathf.Sin(tilt + fov) / Mathf.Sin(fov);
            return Mathf.Max(forWidth, forDepth);
        }

        /// <summary>Far enough back to see the whole arena: co-op pilots can always see each other, so nothing holds them together.</summary>
        float MaxDistance() => world != null ? Mathf.Max(distance, DistanceToFit(world.ArenaSize.x * 0.5f + 1f, world.ArenaSize.y * 0.5f + 1f)) : distance * 1.8f;

        Vector3 ClampToArena(Vector3 point)
        {
            if (world == null)
                return point;

            // Pulled back, the view covers more, so the focus keeps further from the walls.
            float zoom = Mathf.Max(1f, Distance / distance);
            Vector2 half = Vector2.Max(world.ArenaSize * 0.5f - edgeMargin * zoom, Vector2.zero);
            return new Vector3(Mathf.Clamp(point.x, -half.x, half.x), 0f, Mathf.Clamp(point.z, -half.y, half.y));
        }

        /// <summary>Every local pilot's hits shake the screen.</summary>
        void Subscribe(System.Collections.Generic.List<PlayerShip> players)
        {
            for (int i = _subscribed.Count - 1; i >= 0; i--)
            {
                if (_subscribed[i] == null || !players.Contains(_subscribed[i]))
                {
                    if (_subscribed[i] != null)
                        _subscribed[i].Damaged -= OnPlayerDamaged;
                    _subscribed.RemoveAt(i);
                }
            }
            foreach (var player in players)
            {
                if (_subscribed.Contains(player))
                    continue;
                player.Damaged += OnPlayerDamaged;
                _subscribed.Add(player);
            }
        }

        void OnPlayerDamaged(CombatEntity entity, DamageInfo info, DamageResult result)
        {
            if (result.HullDamage > 0f)
                _shake = Mathf.Max(_shake, hullHitShake);
            else if (result.ShieldDamage > 0f)
                _shake = Mathf.Max(_shake, shieldHitShake);
        }

        Vector3 ShakeOffset()
        {
            if (_shake <= 0f)
                return Vector3.zero;

            float t = Time.unscaledTime * shakeFrequency;
            var offset = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, 0f, Mathf.PerlinNoise(0f, t) - 0.5f) * (2f * _shake * Settings.GameSettings.ScreenShake);
            _shake = Mathf.Max(0f, _shake - shakeDecay * Time.unscaledDeltaTime);
            return offset;
        }

        void OnDestroy()
        {
            _locals.Clear();
            Subscribe(_locals);
        }
    }
}
