using System;
using System.Collections.Generic;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Settings;
using UnityEngine;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>One adjustable option row: its label, current value and how a step changes it.</summary>
    public sealed class SettingOption
    {
        public string Label;
        public string Detail;
        public Func<string> Value;

        /// <summary>Changes the value by a step. <c>wrap</c> cycles past the end (Enter, click); arrows stop at the ends.</summary>
        public Action<int, bool> Change;
    }

    /// <summary>
    /// The options shared by the Hub and the pause menu (GDD §12.1), and the controls reference. Built on each call, so
    /// the text follows the current language.
    /// </summary>
    public static class SettingsOptions
    {
        public static IReadOnlyList<SettingOption> All => new[]
        {
            new SettingOption
            {
                Label = Loc.T("options.language"),
                Detail = Loc.T("options.language.detail"),
                Value = LanguageName,
                Change = (step, wrap) => StepLanguage(step),
            },
            Volume("options.master", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v),
            Volume("options.music", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v),
            Volume("options.effects", () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v),
            new SettingOption
            {
                Label = Loc.T("options.shake"),
                Detail = Loc.T("options.shake.detail"),
                Value = () => Percent(GameSettings.ScreenShake),
                Change = (step, wrap) => GameSettings.ScreenShake = Step(GameSettings.ScreenShake, step, 0.25f, wrap),
            },
            Toggle("options.fullscreen", () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v),
            Toggle("options.fps", () => GameSettings.ShowFps, v => GameSettings.ShowFps = v),
        };

        /// <summary>Action, keyboard and mouse, gamepad.</summary>
        public static IReadOnlyList<(string action, string keyboard, string gamepad)> Controls
        {
            get
            {
                var rows = new List<(string, string, string)>();
                foreach (string key in new[] { "move", "aim", "fire", "secondary", "dash", "reload", "interact", "pause" })
                    rows.Add((Loc.T($"controls.{key}"), Loc.T($"controls.{key}.keys"), Loc.T($"controls.{key}.pad")));
                return rows;
            }
        }

        static SettingOption Volume(string key, Func<float> get, Action<float> set) => new SettingOption
        {
            Label = Loc.T(key),
            Detail = Loc.TOr(key + ".detail", null),
            Value = () => Percent(get()),
            Change = (step, wrap) => set(Step(get(), step, 0.1f, wrap)),
        };

        static SettingOption Toggle(string key, Func<bool> get, Action<bool> set) => new SettingOption
        {
            Label = Loc.T(key),
            Detail = Loc.TOr(key + ".detail", null),
            Value = () => Loc.T(get() ? "common.on" : "common.off"),
            Change = (step, wrap) => set(!get()),
        };

        static string LanguageName()
        {
            foreach (var (code, name) in Loc.Languages)
            {
                if (code == Loc.Language)
                    return name;
            }
            return Loc.Language;
        }

        static void StepLanguage(int step)
        {
            var languages = Loc.Languages;
            if (languages.Count < 2)
                return;
            int current = 0;
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i].code == Loc.Language)
                    current = i;
            }
            Loc.SetLanguage(languages[(current + step + languages.Count) % languages.Count].code);
        }

        static float Step(float value, int step, float size, bool wrap)
        {
            float next = Mathf.Round((value + step * size) / size) * size;
            if (wrap && next > 1.001f)
                return 0f;
            return Mathf.Clamp01(next);
        }

        static string Percent(float value) => value <= 0.001f ? Loc.T("common.off") : Loc.T("common.percent", ("value", value * 100f));
    }
}
