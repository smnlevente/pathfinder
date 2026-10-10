using System;
using UnityEngine;

namespace Pathfinder.Game.Settings
{
    /// <summary>
    /// Player options (GDD §12.1): language, volumes, sound in the background, screen shake, fullscreen and the FPS counter. They belong to this machine, not
    /// the profile, so they live in PlayerPrefs rather than the meta save. Loaded on first use, saved on every change.
    /// </summary>
    public static class GameSettings
    {
        const string Prefix = "pathfinder.settings.";

        static bool s_loaded;
        static float s_master = 0.8f, s_music = 0.7f, s_effects = 1f, s_shake = 1f;
        static bool s_fullscreen = true, s_showFps, s_soundInBackground;
        static string s_language = "en";

        /// <summary>False keeps changes in memory only, so tests never touch the player's preferences.</summary>
        public static bool Persist { get; set; } = true;

        /// <summary>
        /// Whether the game reacts to losing window focus (muting, pausing). Off for tests, which run in the background
        /// and mustn't pause or go silent when the developer clicks elsewhere.
        /// </summary>
        public static bool RespondToFocus { get; set; } = true;

        public static event Action Changed;

        public static float MasterVolume { get => Get(ref s_master); set => Set(ref s_master, Mathf.Clamp01(value), "master"); }
        public static float MusicVolume { get => Get(ref s_music); set => Set(ref s_music, Mathf.Clamp01(value), "music"); }
        public static float EffectsVolume { get => Get(ref s_effects); set => Set(ref s_effects, Mathf.Clamp01(value), "effects"); }

        /// <summary>Multiplier on camera shake. 0 turns it off, for players sensitive to motion.</summary>
        public static float ScreenShake { get => Get(ref s_shake); set => Set(ref s_shake, Mathf.Clamp01(value), "shake"); }

        public static bool Fullscreen
        {
            get { Load(); return s_fullscreen; }
            set
            {
                Load();
                s_fullscreen = value;
                if (!Application.isBatchMode)
                    Screen.fullScreen = value;
                Save("fullscreen", value ? 1f : 0f);
            }
        }

        /// <summary>The .loc file in use, by code (en, de, ...). See <see cref="Localization.Loc"/>.</summary>
        public static string Language
        {
            get { Load(); return s_language; }
            set
            {
                Load();
                s_language = string.IsNullOrWhiteSpace(value) ? "en" : value;
                if (Persist)
                {
                    PlayerPrefs.SetString(Prefix + "language", s_language);
                    PlayerPrefs.Save();
                }
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Keep the music and sounds playing while the game window isn't focused (playtest 5). Off: the game goes quiet
        /// when you switch away. Either way, a fight pauses itself.
        /// </summary>
        public static bool SoundInBackground
        {
            get { Load(); return s_soundInBackground; }
            set { Load(); s_soundInBackground = value; ApplyBackground(); Save("background", value ? 1f : 0f); }
        }

        /// <summary>With sound in the background, the game keeps running when unfocused, so the music plays on.</summary>
        static void ApplyBackground()
        {
            if (!Application.isBatchMode)
                Application.runInBackground = s_soundInBackground;
        }

        public static bool ShowFps
        {
            get { Load(); return s_showFps; }
            set { Load(); s_showFps = value; Save("fps", value ? 1f : 0f); }
        }

        /// <summary>Back to the defaults. Used by tests.</summary>
        public static void ResetToDefaults()
        {
            s_master = 0.8f;
            s_music = 0.7f;
            s_effects = 1f;
            s_shake = 1f;
            s_fullscreen = true;
            s_showFps = false;
            s_soundInBackground = false;
            s_language = "en";
            s_loaded = true;
            Changed?.Invoke();
        }

        static float Get(ref float field)
        {
            Load();
            return field;
        }

        static void Set(ref float field, float value, string key)
        {
            Load();
            field = value;
            Save(key, value);
        }

        static void Save(string key, float value)
        {
            if (Persist)
            {
                PlayerPrefs.SetFloat(Prefix + key, value);
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
        }

        static void Load()
        {
            if (s_loaded)
                return;
            s_loaded = true;
            if (!Persist)
                return;
            s_master = PlayerPrefs.GetFloat(Prefix + "master", s_master);
            s_music = PlayerPrefs.GetFloat(Prefix + "music", s_music);
            s_effects = PlayerPrefs.GetFloat(Prefix + "effects", s_effects);
            s_shake = PlayerPrefs.GetFloat(Prefix + "shake", s_shake);
            s_fullscreen = PlayerPrefs.GetFloat(Prefix + "fullscreen", s_fullscreen ? 1f : 0f) > 0.5f;
            s_showFps = PlayerPrefs.GetFloat(Prefix + "fps", 0f) > 0.5f;
            s_soundInBackground = PlayerPrefs.GetFloat(Prefix + "background", 0f) > 0.5f;
            ApplyBackground();
            s_language = PlayerPrefs.GetString(Prefix + "language", s_language);
        }
    }
}
