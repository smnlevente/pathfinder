using Pathfinder.Core.Random;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Run;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Placeholder IMGUI HUD for combat testing (GDD §12.2 layout): hull and shield, ammo, dash charges,
    /// wave, seed, and the pause/game-over overlays. The real uGUI or UI Toolkit HUD comes with the UI pass.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        const float ReferenceHeight = 1080f;

        [SerializeField] CombatWorld world;
        [SerializeField] RunFlow runFlow;
        [SerializeField] WaveSpawner waveSpawner;

        static readonly Color HullColor = new Color(0.95f, 0.35f, 0.3f);
        static readonly Color ShieldColor = new Color(0.3f, 0.85f, 1f);
        static readonly Color BackColor = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color DashColor = new Color(0.4f, 1f, 0.9f);

        GUIStyle _label, _big, _small;
        float _fps;

        void Update() => _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);

        void OnGUI()
        {
            EnsureStyles();
            float scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;

            var player = LocalPlayer();
            if (player != null && player.Vitals != null)
                DrawPlayerPanel(player);

            string wave = waveSpawner != null ? $"WAVE {waveSpawner.Wave}   ENEMIES {CombatRegistry.Enemies.Count}" : string.Empty;
            GUI.Label(new Rect(width * 0.5f - 300f, 20f, 600f, 40f), wave, _label);

            if (world != null)
                GUI.Label(new Rect(width - 420f, 20f, 400f, 30f), $"SEED {RunSeed.ToCode(world.Seed)}   {_fps:0} FPS", _small);

            if (runFlow != null && runFlow.IsGameOver)
                DrawOverlay(width, "SHIP DESTROYED", "Press E / (Y) to restart");
            else if (GameClock.Paused)
                DrawOverlay(width, "PAUSED", "Press Esc / Start to resume");
        }

        void DrawPlayerPanel(PlayerShip player)
        {
            var vitals = player.Vitals;
            const float x = 40f, y = 40f, barWidth = 360f;

            Bar(new Rect(x, y, barWidth, 26f), vitals.Hull / vitals.MaxHull, HullColor);
            if (vitals.MaxShield > 0f)
                Bar(new Rect(x, y - 10f, barWidth, 8f), vitals.Shield / vitals.MaxShield, ShieldColor);
            GUI.Label(new Rect(x + 10f, y, barWidth, 26f), $"HULL {Mathf.CeilToInt(vitals.Hull)} / {vitals.MaxHull:0}   SHIELD {Mathf.CeilToInt(vitals.Shield)}", _small);

            var weapon = player.Weapon;
            string ammo = weapon.IsReloading ? $"RELOADING {weapon.ReloadProgress * 100f:0}%" : $"{weapon.Ammo} / {weapon.MagazineSize}";
            GUI.Label(new Rect(x, y + 34f, barWidth, 30f), $"{player.PrimaryWeapon.DisplayName.ToUpperInvariant()}   {ammo}", _label);

            var dash = player.Dash;
            for (int i = 0; i < dash.MaxCharges; i++)
            {
                var pip = new Rect(x + i * 46f, y + 72f, 40f, 10f);
                float fill = i < dash.Charges ? 1f : i == dash.Charges ? dash.RechargeProgress : 0f;
                Bar(pip, fill, DashColor);
            }
        }

        void DrawOverlay(float width, string title, string hint)
        {
            float height = ReferenceHeight;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, height * 0.4f, width, 80f), title, _big);
            GUI.Label(new Rect(0f, height * 0.4f + 80f, width, 40f), hint, _label);
        }

        static void Bar(Rect rect, float fill, Color color)
        {
            GUI.color = BackColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static PlayerShip LocalPlayer()
        {
            foreach (var player in CombatRegistry.Players)
            {
                if (player.IsLocal)
                    return player;
            }
            return null;
        }

        void EnsureStyles()
        {
            if (_label != null)
                return;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _label.normal.textColor = Color.white;
            _big = new GUIStyle(_label) { fontSize = 64 };
            _small = new GUIStyle(_label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
        }
    }
}
