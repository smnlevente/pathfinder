using System;
using System.Collections.Generic;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Settings;
using UnityEngine;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>How an option is shown and changed.</summary>
    public enum SettingKind
    {
        /// <summary>A value from a list, shown as text: click or arrows cycle it (the language).</summary>
        Choice,

        /// <summary>A 0–1 amount on a slider: drag it, or step it with arrows, click and right click (volumes, shake).</summary>
        Slider,

        /// <summary>On or off, shown as a tickbox: click, Enter or arrows toggle it.</summary>
        Toggle,
    }

    /// <summary>One adjustable option row: its label, current value and how a step changes it.</summary>
    public sealed class SettingOption
    {
        public SettingKind Kind;
        public string Label;
        public string Detail;
        public Func<string> Value;

        /// <summary>Changes the value by a step. <c>wrap</c> cycles past the end; otherwise it stops at the ends.</summary>
        public Action<int, bool> Change;

        /// <summary>Sliders: the value as 0–1, and setting it directly (a drag).</summary>
        public Func<float> Get01;
        public Action<float> Set01;
        public float StepSize = 0.1f;

        /// <summary>Toggles: whether it is on.</summary>
        public Func<bool> IsOn;

        /// <summary>A left click on the row (or Enter): a slider steps up and stops at the top, anything else cycles.</summary>
        public void Click() => Change(1, Kind != SettingKind.Slider);

        /// <summary>A right click on the row: one step down (a toggle flips).</summary>
        public void RightClick() => Change(-1, false);
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
                Kind = SettingKind.Choice,
                Label = Loc.T("options.language"),
                Detail = Loc.T("options.language.detail"),
                Value = LanguageName,
                Change = (step, wrap) => StepLanguage(step),
            },
            Slider("options.master", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, 0.1f),
            Slider("options.music", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, 0.1f),
            Slider("options.effects", () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v, 0.1f),
            Toggle("options.background_sound", () => GameSettings.SoundInBackground, v => GameSettings.SoundInBackground = v),
            Slider("options.shake", () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v, 0.25f),
            Toggle("options.fullscreen", () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v),
            Toggle("options.fps", () => GameSettings.ShowFps, v => GameSettings.ShowFps = v),
        };

        /// <summary>Finds an option by its text key, e.g. "options.music". For tests.</summary>
        public static SettingOption Find(string key)
        {
            string label = Loc.T(key);
            foreach (var option in All)
            {
                if (option.Label == label)
                    return option;
            }
            return null;
        }

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

        static SettingOption Slider(string key, Func<float> get, Action<float> set, float step) => new SettingOption
        {
            Kind = SettingKind.Slider,
            Label = Loc.T(key),
            Detail = Loc.TOr(key + ".detail", null),
            Value = () => Percent(get()),
            Change = (direction, wrap) => set(Step(get(), direction, step, wrap)),
            Get01 = get,
            Set01 = set,
            StepSize = step,
        };

        static SettingOption Toggle(string key, Func<bool> get, Action<bool> set) => new SettingOption
        {
            Kind = SettingKind.Toggle,
            Label = Loc.T(key),
            Detail = Loc.TOr(key + ".detail", null),
            Value = () => Loc.T(get() ? "common.on" : "common.off"),
            Change = (step, wrap) => set(!get()),
            IsOn = get,
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

    /// <summary>Builds and refreshes option rows the same way in the Hub and the pause menu.</summary>
    public static class OptionRows
    {
        /// <summary>A row for an option: a slider for amounts, a tickbox for on/off, the value as text for a choice.</summary>
        public static UiRow Create(Transform list, SettingOption option)
        {
            var row = new UiRow(list, option.Label, option.Value(), option.Detail, null, UiKit.Accent);
            switch (option.Kind)
            {
                case SettingKind.Slider:
                    var slider = row.AddSlider(280f, option.StepSize >= 0.25f ? option.StepSize : 0.05f);
                    slider.Set(option.Get01());
                    slider.Dragged += value =>
                    {
                        option.Set01(value);
                        row.SetNote(option.Value());
                    };
                    break;
                case SettingKind.Toggle:
                    row.AddCheckbox();
                    row.SetChecked(option.IsOn());
                    break;
            }
            return row;
        }

        /// <summary>Shows the option's current value after a change made some other way (arrows, click).</summary>
        public static void Refresh(UiRow row, SettingOption option)
        {
            switch (option.Kind)
            {
                case SettingKind.Toggle:
                    row.SetChecked(option.IsOn());
                    break;
                case SettingKind.Slider:
                    row.Slider?.Set(option.Get01());
                    row.SetNote(option.Value());
                    break;
                default:
                    row.SetNote(option.Value());
                    break;
            }
        }
    }
}
