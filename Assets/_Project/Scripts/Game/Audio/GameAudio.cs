using System.Collections.Generic;
using Pathfinder.Core.Combat;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Audio
{
    /// <summary>
    /// Plays the game's sounds (GDD §13.2) from a pool of voices, positioned around the local ship: panned by which side
    /// of it a sound happens and quieter with distance, so the ship is the centre of the soundscape (playtest 4). It
    /// listens to combat events itself; other code calls <see cref="Play"/>. Each cue has a voice limit and a minimum
    /// interval, so a bullet storm stays a texture instead of a wall of noise. Presentation only: the simulation never
    /// waits on it, and every call is safe when no director is in the scene.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        static GameAudio s_instance;

        [SerializeField] SoundLibrary library;
        [SerializeField, Min(4)] int voices = 28;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Min(1f), Tooltip("Sideways distance from the ship at which sounds pan fully to one side.")] float panDistance = 14f;
        [SerializeField, Min(0f), Tooltip("Sounds this close to the ship play at full volume.")] float nearDistance = 6f;
        [SerializeField, Min(1f), Tooltip("Sounds this far from the ship play at the far volume.")] float farDistance = 32f;
        [SerializeField, Range(0f, 1f)] float farVolume = 0.3f;
        [SerializeField, Tooltip("What plays when the scene starts.")] MusicTrack startTrack;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.55f;
        [SerializeField, Min(0.01f)] float musicFade = 1f;

        readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Dictionary<SoundCue, float> _lastPlayed = new Dictionary<SoundCue, float>();
        readonly Dictionary<AudioSource, SoundCue> _playing = new Dictionary<AudioSource, SoundCue>();
        readonly HashSet<PlayerShip> _subscribed = new HashSet<PlayerShip>();

        /// <summary>Crossfades to a music track, or fades out for <see cref="MusicTrack.None"/>. Safe without a director.</summary>
        public static void PlayMusic(MusicTrack track)
        {
            if (s_instance != null)
                s_instance.SwitchMusic(track);
        }

        public static MusicTrack CurrentMusic => MusicPlayer.CurrentTrack;

        /// <summary>Plays a cue. A position pans it toward that side of the screen. Does nothing without a director.</summary>
        public static void Play(SoundCue cue, Vector3? position = null, float volume = 1f)
        {
            if (s_instance != null)
                s_instance.PlayInternal(cue, position, volume);
        }

        void Awake()
        {
            // One director per scene; the newest takes over, so a scene change never leaves the game silent.
            s_instance = this;
            for (int i = 0; i < voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sources.Add(source);
            }
        }

        void Start()
        {
            SwitchMusic(startTrack);

            // One line to diagnose silence: no output device, or everything turned down.
            if (AudioSettings.outputSampleRate <= 0)
                Debug.LogWarning("[Audio] Unity has no audio output device. Check the system's sound output and restart the Editor.");
            else if (Debug.isDebugBuild && !Application.isBatchMode)
                Debug.Log($"[Audio] {AudioSettings.outputSampleRate} Hz, music: {startTrack}, volume: master {Settings.GameSettings.MasterVolume:P0}, " +
                    $"music {Settings.GameSettings.MusicVolume:P0}, effects {Settings.GameSettings.EffectsVolume:P0}. Silent in the Editor? Check Mute Audio in the Game view.");
        }

        /// <summary>
        /// Music lives on the <see cref="MusicPlayer"/>, which outlasts the scene, so a track that is already playing
        /// carries on when a new scene asks for it (playtest 5: the Hub track restarted when a run began).
        /// </summary>
        void SwitchMusic(MusicTrack track)
        {
            var player = MusicPlayer.Instance;
            player.SetLevel(musicVolume * masterVolume, musicFade);
            player.Play(track, library != null ? library.Music(track) : null);
        }

        void OnEnable()
        {
            CombatEvents.Damaged += OnDamaged;
            CombatEvents.Killed += OnKilled;
            CombatEvents.ProjectileFired += OnProjectileFired;
            CombatEvents.Deflected += OnDeflected;
            CombatEvents.Telegraphed += OnTelegraphed;
        }

        void OnDisable()
        {
            CombatEvents.Damaged -= OnDamaged;
            CombatEvents.Killed -= OnKilled;
            CombatEvents.ProjectileFired -= OnProjectileFired;
            CombatEvents.Deflected -= OnDeflected;
            CombatEvents.Telegraphed -= OnTelegraphed;
            foreach (var ship in _subscribed)
            {
                if (ship != null)
                    Unsubscribe(ship);
            }
            _subscribed.Clear();
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        void Update()
        {
            MusicPlayer.Instance.SetLevel(musicVolume * masterVolume, musicFade);

            // Local pilots can spawn later (a new run, a respawn), so pick them up as they appear.
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal && _subscribed.Add(player))
                {
                    player.Fired += OnFired;
                    player.SecondaryActivated += OnSecondary;
                    player.Dashed += OnDashed;
                    player.ScrapsCollected += OnScraps;
                    player.SecondaryRecharged += OnSecondaryReady;
                }
            }
        }

        void Unsubscribe(PlayerShip ship)
        {
            ship.Fired -= OnFired;
            ship.SecondaryActivated -= OnSecondary;
            ship.Dashed -= OnDashed;
            ship.ScrapsCollected -= OnScraps;
            ship.SecondaryRecharged -= OnSecondaryReady;
        }

        // ---------------------------------------------------------------- event handlers

        void OnFired(PlayerShip ship)
        {
            var weapon = ship.PrimaryWeapon;
            var cue = weapon != null && weapon.FireMode == PrimaryFireMode.Charge ? SoundCue.ChargeShot
                : weapon != null && weapon.ProjectilesPerShot > 1 ? SoundCue.PlasmaShot
                : SoundCue.PlayerShot;
            Play(cue, ship.transform.position);
        }

        void OnSecondary(PlayerShip ship, SecondaryWeaponDefinition weapon) =>
            Play(weapon is NovaPulseDefinition ? SoundCue.NovaPulse : SoundCue.MissileLaunch, ship.transform.position);

        void OnDashed(PlayerShip ship) => Play(SoundCue.Dash, ship.transform.position);

        void OnSecondaryReady(PlayerShip ship) => Play(SoundCue.SecondaryReady, ship.transform.position);

        void OnScraps(PlayerShip ship, int amount) => Play(SoundCue.Pickup, ship.transform.position, 0.8f);

        void OnDamaged(CombatEntity target, DamageInfo info, DamageResult result)
        {
            if (target is PlayerShip ship)
            {
                if (!ship.IsLocal || result.Blocked)
                    return;
                Play(result.HullDamage > 0f ? SoundCue.HullHit : SoundCue.ShieldHit, ship.transform.position);
                return;
            }
            if (info.Kind != DamageKind.Burn)
                Play(SoundCue.HitEnemy, target.transform.position, 0.7f);
        }

        void OnKilled(CombatEntity target, DamageInfo info)
        {
            bool large = target is PlayerShip || (target is Enemy enemy && (enemy.IsElite || enemy.Radius > 1f));
            Play(large ? SoundCue.ExplosionLarge : SoundCue.ExplosionSmall, target.transform.position);
        }

        void OnProjectileFired(Team team, Vector2 position)
        {
            if (team == Team.Enemy)
                Play(SoundCue.EnemyShot, new Vector3(position.x, 0f, position.y), 0.6f);
        }

        void OnDeflected(Vector2 position) => Play(SoundCue.Deflect, new Vector3(position.x, 0f, position.y));

        void OnTelegraphed(Enemy enemy)
        {
            // Each type has its own wind-up; the boss's are the generic warning (its roar is played by its brain).
            var cue = enemy.TryGetComponent(out LancerBrain _) ? SoundCue.TelegraphLancer
                : enemy.TryGetComponent(out GunnerBrain _) ? SoundCue.TelegraphGunner
                : enemy.TryGetComponent(out SpinnerBrain _) ? SoundCue.TelegraphSpinner
                : enemy.TryGetComponent(out CarrierBrain _) ? SoundCue.TelegraphCarrier
                : enemy.TryGetComponent(out ScorcherBrain _) ? SoundCue.TelegraphScorcher
                : enemy.TryGetComponent(out ArcDroneBrain _) ? SoundCue.TelegraphArc
                : enemy.TryGetComponent(out CrusherBrain _) ? SoundCue.TelegraphCrusher
                : SoundCue.Telegraph;
            Play(cue, enemy.transform.position, enemy.IsElite ? 1.15f : 1f);
        }

        // ---------------------------------------------------------------- playback

        void PlayInternal(SoundCue cue, Vector3? position, float volume)
        {
            var entry = library != null ? library.Find(cue) : null;
            float level = masterVolume * Settings.GameSettings.MasterVolume * Settings.GameSettings.EffectsVolume;
            if (entry == null || entry.Clip == null || level <= 0f)
                return;

            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(cue, out float last) && now - last < entry.MinInterval)
                return;

            int sameCue = 0;
            AudioSource free = null, oldest = null;
            float oldestTime = float.MinValue;
            foreach (var source in _sources)
            {
                if (!source.isPlaying)
                {
                    free ??= source;
                    continue;
                }
                if (_playing.TryGetValue(source, out var playingCue) && playingCue == cue)
                {
                    sameCue++;
                    if (source.time > oldestTime)
                    {
                        oldestTime = source.time;
                        oldest = source;
                    }
                }
            }

            // At the voice limit, the oldest copy of this cue makes way; otherwise use a free voice.
            var voice = sameCue >= entry.MaxVoices ? oldest : free;
            if (voice == null)
                return;

            _lastPlayed[cue] = now;
            _playing[voice] = cue;
            voice.clip = entry.Clip;
            Vector3 listener = ListenerPosition();
            voice.volume = entry.Volume * volume * level * (position.HasValue ? Attenuation(position.Value, listener) : 1f);
            voice.pitch = 1f + Random.Range(-entry.PitchJitter, entry.PitchJitter); // presentation only: Unity's RNG is fine
            voice.panStereo = position.HasValue ? Mathf.Clamp((position.Value.x - listener.x) / panDistance, -0.85f, 0.85f) : 0f;
            voice.Play();
        }

        /// <summary>The local ship is the centre of the soundscape; without one (menus, after death) the camera is.</summary>
        static Vector3 ListenerPosition()
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal && player.IsAlive)
                    return player.transform.position;
            }
            var camera = Camera.main;
            return camera != null ? camera.transform.position : Vector3.zero;
        }

        float Attenuation(Vector3 position, Vector3 listener)
        {
            float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(listener.x, listener.z));
            return Mathf.Lerp(1f, farVolume, Mathf.InverseLerp(nearDistance, farDistance, distance));
        }
    }
}
