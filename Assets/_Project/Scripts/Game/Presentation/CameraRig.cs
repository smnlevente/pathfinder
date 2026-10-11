using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Top-down follow camera (GDD §7.1, §13.1). It follows the local pilots with a slight lead toward their
    /// aim, stays inside the arena, and shakes when they take damage or something big blows up. Presentation only.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] CombatWorld world;
        [SerializeField, Range(10f, 89f)] float pitch = 60f;
        [SerializeField, Min(1f)] float distance = 24f;
        [SerializeField, Min(0f), Tooltip("How far the view leads toward the aim direction.")] float aimLead = 2f;
        [SerializeField, Min(0.01f)] float smoothTime = 0.12f;
        [SerializeField, Tooltip("How far the focus point may get from the arena edge, per axis.")] Vector2 edgeMargin = new Vector2(12f, 7f);

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
        PlayerShip _subscribed;
        int _placements = -1;

        /// <summary>The point the camera looks at, before shake. For tests.</summary>
        public Vector3 Focus => _focus;

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

            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.SetPositionAndRotation(_focus - rotation * Vector3.forward * distance + ShakeOffset(), rotation);
        }

        bool TryGetFocus(out Vector3 focus)
        {
            focus = Vector3.zero;
            int count = 0;
            PlayerShip firstLocal = null;

            foreach (var player in CombatRegistry.Players)
            {
                if (!player.IsLocal)
                    continue;

                if (firstLocal == null)
                    firstLocal = player;
                focus += player.transform.position + PlaneMath.ToWorld(player.AimDirection * (player.IsAlive ? aimLead : 0f));
                count++;
            }

            SubscribeTo(firstLocal);
            // The rumble only while the pull is actually playing: never behind the map or a menu.
            if (firstLocal != null && firstLocal.IsPulled && !GameClock.Paused)
                Kick(wormholePullShake);
            if (count == 0)
                return false;

            focus /= count;
            // Cut, don't slide, when the ship is placed at a new level's entry (playtest 5).
            bool placed = firstLocal.Placements != _placements;
            _placements = firstLocal.Placements;
            if (!_hasFocus || placed)
            {
                _focus = ClampToArena(focus);
                _velocity = Vector3.zero;
                _hasFocus = true;
            }
            return true;
        }

        Vector3 ClampToArena(Vector3 point)
        {
            if (world == null)
                return point;

            Vector2 half = Vector2.Max(world.ArenaSize * 0.5f - edgeMargin, Vector2.zero);
            return new Vector3(Mathf.Clamp(point.x, -half.x, half.x), 0f, Mathf.Clamp(point.z, -half.y, half.y));
        }

        void SubscribeTo(PlayerShip player)
        {
            if (player == _subscribed)
                return;
            if (_subscribed != null)
                _subscribed.Damaged -= OnPlayerDamaged;

            _subscribed = player;
            if (_subscribed != null)
                _subscribed.Damaged += OnPlayerDamaged;
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

        void OnDestroy() => SubscribeTo(null);
    }
}
