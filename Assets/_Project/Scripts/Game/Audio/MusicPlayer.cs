using UnityEngine;

namespace Pathfinder.Game.Audio
{
    /// <summary>
    /// Plays the background music across scene loads (GDD §13.2). Asking for the track that is already playing does
    /// nothing, so the music carries on through a scene change instead of restarting (playtest 5); a different track
    /// crossfades in on the second voice. Created by the first sound director that needs it, and kept for the whole
    /// session. The active director sets its level each frame. Presentation only.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        static MusicPlayer s_instance;

        AudioSource _current, _previous;
        float _level = 0.55f;
        float _fadeTime = 1f;

        public MusicTrack Track { get; private set; }
        public AudioClip Clip => _current != null ? _current.clip : null;

        /// <summary>The player, created on first use.</summary>
        public static MusicPlayer Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("Music");
                    DontDestroyOnLoad(go);
                    s_instance = go.AddComponent<MusicPlayer>();
                }
                return s_instance;
            }
        }

        /// <summary>The playing track, or None when there is no player yet.</summary>
        public static MusicTrack CurrentTrack => s_instance != null ? s_instance.Track : MusicTrack.None;

        void Awake()
        {
            _current = Voice();
            _previous = Voice();
        }

        AudioSource Voice()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        /// <summary>Crossfades to a track, or fades out for <see cref="MusicTrack.None"/>. The same track keeps playing.</summary>
        public void Play(MusicTrack track, AudioClip clip)
        {
            if (track == Track && (clip == null || _current.clip == clip))
                return;
            Track = track;

            (_current, _previous) = (_previous, _current);
            _current.clip = clip;
            _current.volume = 0f;
            if (clip != null)
                _current.Play();
            else
                _current.Stop();
        }

        /// <summary>Called by the active sound director: its music level, before the player's volume settings.</summary>
        public void SetLevel(float level, float fadeTime)
        {
            _level = level;
            _fadeTime = Mathf.Max(0.01f, fadeTime);
        }

        void Update()
        {
            float target = _level * Settings.GameSettings.MasterVolume * Settings.GameSettings.MusicVolume;
            float step = Time.unscaledDeltaTime / _fadeTime * Mathf.Max(target, 0.01f);
            _current.volume = Mathf.MoveTowards(_current.volume, _current.clip != null ? target : 0f, step);
            _previous.volume = Mathf.MoveTowards(_previous.volume, 0f, step);
            if (_previous.isPlaying && _previous.volume <= 0f)
                _previous.Stop();
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;
    }
}
