using System;
using System.Collections.Generic;
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

    /// <summary>The options shared by the Hub and the pause menu (GDD §12.1), and the controls reference.</summary>
    public static class SettingsOptions
    {
        public static readonly IReadOnlyList<SettingOption> All = new[]
        {
            Volume("Master volume", "Everything you hear.", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v),
            Volume("Music", "The soundtrack, relative to the master volume.", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v),
            Volume("Sound effects", "Weapons, hits, explosions and menus.", () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v),
            new SettingOption
            {
                Label = "Screen shake",
                Detail = "Camera kicks on hits and explosions. Turn it down if motion bothers you.",
                Value = () => Percent(GameSettings.ScreenShake),
                Change = (step, wrap) => GameSettings.ScreenShake = Step(GameSettings.ScreenShake, step, 0.25f, wrap),
            },
            Toggle("Fullscreen", null, () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v),
            Toggle("Show FPS", "Frames per second, next to the run seed.", () => GameSettings.ShowFps, v => GameSettings.ShowFps = v),
        };

        /// <summary>Action, keyboard and mouse, gamepad.</summary>
        public static readonly (string action, string keyboard, string gamepad)[] Controls =
        {
            ("Move", "WASD / arrows", "Left stick"),
            ("Aim", "Mouse", "Right stick"),
            ("Fire primary (hold to charge)", "Left mouse", "Right trigger"),
            ("Fire secondary", "Right mouse", "Left trigger"),
            ("Dash (invulnerable)", "Space", "A / Cross, RB"),
            ("Reload", "R", "X / Square"),
            ("Interact", "E", "Y / Triangle"),
            ("Pause", "Esc", "Start"),
        };

        static SettingOption Volume(string label, string detail, Func<float> get, Action<float> set) => new SettingOption
        {
            Label = label,
            Detail = detail,
            Value = () => Percent(get()),
            Change = (step, wrap) => set(Step(get(), step, 0.1f, wrap)),
        };

        static SettingOption Toggle(string label, string detail, Func<bool> get, Action<bool> set) => new SettingOption
        {
            Label = label,
            Detail = detail,
            Value = () => get() ? "ON" : "OFF",
            Change = (step, wrap) => set(!get()),
        };

        static float Step(float value, int step, float size, bool wrap)
        {
            float next = Mathf.Round((value + step * size) / size) * size;
            if (wrap && next > 1.001f)
                return 0f;
            return Mathf.Clamp01(next);
        }

        static string Percent(float value) => value <= 0.001f ? "OFF" : $"{Mathf.RoundToInt(value * 100f)}%";
    }
}
