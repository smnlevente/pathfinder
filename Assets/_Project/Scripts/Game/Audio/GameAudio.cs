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
    /// Plays the game's sounds (GDD §13.2) from a pool of 2D voices, panned by where the sound happens relative to the
    /// camera. It listens to combat events itself; other code calls <see cref="Play"/>. Each cue has a voice limit and a
    /// minimum interval, so a bullet storm stays a texture instead of a wall of noise. Presentation only: the simulation
    /// never waits on it, and every call is safe when no director is in the scene.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        static GameAudio s_instance;

        [SerializeField] SoundLibrary library;
        [SerializeField, Min(4)] int voices = 28;
        [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
        [SerializeField, Min(1f), Tooltip("Horizontal distance from the camera at which sounds pan fully to one side.")] float panDistance = 20f;
        [SerializeField, Tooltip("What plays when the scene starts.")] MusicTrack startTrack;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.55f;
        [SerializeField, Min(0.01f)] float musicFade = 1f;

        readonly List<AudioSource> _sources = new List<AudioSource>();
        readonly Dictionary<SoundCue, float> _lastPlayed = new Dictionary<SoundCue, float>();
        readonly Dictionary<AudioSource, SoundCue> _playing = new Dictionary<AudioSource, SoundCue>();
        readonly HashSet<PlayerShip> _subscribed = new HashSet<PlayerShip>();
        AudioSource _musicA, _musicB;
        MusicTrack _track;

        /// <summary>Crossfades to a music track, or fades out for <see cref="MusicTrack.None"/>. Safe without a director.</summary>
        public static void PlayMusic(MusicTrack track)
        {
            if (s_instance != null)
                s_instance.SwitchMusic(track);
        }

        public static MusicTrack CurrentMusic => s_instance != null ? s_instance._track : MusicTrack.None;

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

            _musicA = MusicSource();
            _musicB = MusicSource();
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

        AudioSource MusicSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        void SwitchMusic(MusicTrack track)
        {
            var clip = library != null ? library.Music(track) : null;
            if (track == _track && (_musicA.clip == clip || clip == null))
                return;
            _track = track;

            // The current source fades out; the other one starts the new track and fades in.
            (_musicA, _musicB) = (_musicB, _musicA);
            _musicA.clip = clip;
            _musicA.volume = 0f;
            if (clip != null)
                _musicA.Play();
            else
                _musicA.Stop();
        }

        void FadeMusic()
        {
            float step = Time.unscaledDeltaTime / musicFade;
            float target = musicVolume * masterVolume * Settings.GameSettings.MasterVolume * Settings.GameSettings.MusicVolume;
            _musicA.volume = Mathf.MoveTowards(_musicA.volume, _musicA.clip != null ? target : 0f, step * Mathf.Max(target, 0.01f));
            _musicB.volume = Mathf.MoveTowards(_musicB.volume, 0f, step * Mathf.Max(target, 0.01f));
            if (_musicB.isPlaying && _musicB.volume <= 0f)
                _musicB.Stop();
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
            FadeMusic();

            // Local pilots can spawn later (a new run, a respawn), so pick them up as they appear.
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal && _subscribed.Add(player))
                {
                    player.Fired += OnFired;
                    player.SecondaryActivated += OnSecondary;
                    player.Dashed += OnDashed;
                    player.ScrapsCollected += OnScraps;
                }
            }
        }

        void Unsubscribe(PlayerShip ship)
        {
            ship.Fired -= OnFired;
            ship.SecondaryActivated -= OnSecondary;
            ship.Dashed -= OnDashed;
            ship.ScrapsCollected -= OnScraps;
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

        void OnTelegraphed(Enemy enemy) => Play(enemy.Radius > 1f ? SoundCue.Roar : SoundCue.Telegraph, enemy.transform.position);

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
            voice.volume = entry.Volume * volume * level;
            voice.pitch = 1f + Random.Range(-entry.PitchJitter, entry.PitchJitter); // presentation only: Unity's RNG is fine
            voice.panStereo = position.HasValue ? Pan(position.Value) : 0f;
            voice.Play();
        }

        float Pan(Vector3 position)
        {
            var camera = Camera.main;
            if (camera == null)
                return 0f;
            return Mathf.Clamp((position.x - camera.transform.position.x) / panDistance, -0.8f, 0.8f);
        }
    }
}
