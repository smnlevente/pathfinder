using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// The colours a pilot can paint their ship (playtest 8: "it's hard to distinguish who is who"). Each player picks one in
    /// the Hub, and it goes on their ship, its trails, the ring under it, their tag and their panel. No two local players
    /// share one. The first four are players 1–4's defaults; none is red, which is the enemies' colour.
    /// </summary>
    public readonly struct PilotPaint
    {
        /// <summary>Its name's key in the localisation file, "paint.cyan".</summary>
        public string Key { get; }

        /// <summary>The colour on menus, tags and the floor ring.</summary>
        public Color Ui { get; }

        /// <summary>The ship's lights: wingtips, cockpit and engine. HDR, so they bloom.</summary>
        public Color Glow { get; }

        public PilotPaint(string key, Color ui, Color glow)
        {
            Key = key;
            Ui = ui;
            Glow = glow;
        }

        /// <summary>The engine's faint exhaust trail: dim enough to stay below the bloom threshold.</summary>
        public Color Exhaust => Glow * 0.36f;

        /// <summary>The dash trail: brighter than the lights.</summary>
        public Color DashTrail => Glow * 1.5f;

        /// <summary>The hull, painted (local co-op): the colour, with a hint of the bare metal.</summary>
        public Color Hull(Color metal) => Color.Lerp(metal, Ui, 0.9f);

        public static readonly PilotPaint[] All =
        {
            new PilotPaint("paint.cyan", new Color(0.35f, 0.9f, 1f), new Color(0.3f, 1.6f, 2.6f)),
            new PilotPaint("paint.gold", new Color(1f, 0.8f, 0.3f), new Color(2.6f, 1.7f, 0.35f)),
            new PilotPaint("paint.violet", new Color(0.78f, 0.55f, 1f), new Color(1.7f, 0.8f, 2.6f)),
            new PilotPaint("paint.pink", new Color(1f, 0.5f, 0.72f), new Color(2.6f, 0.7f, 1.5f)),
            new PilotPaint("paint.green", new Color(0.45f, 1f, 0.45f), new Color(0.6f, 2.6f, 0.6f)),
            new PilotPaint("paint.orange", new Color(1f, 0.58f, 0.22f), new Color(2.6f, 1.05f, 0.2f)),
            new PilotPaint("paint.blue", new Color(0.42f, 0.55f, 1f), new Color(0.45f, 0.8f, 3f)),
            new PilotPaint("paint.white", new Color(0.92f, 0.95f, 1f), new Color(2.2f, 2.3f, 2.5f)),
        };

        public static int Count => All.Length;

        /// <summary>A paint by index, wrapped into range.</summary>
        public static PilotPaint Get(int paint) => All[((paint % All.Length) + All.Length) % All.Length];

        /// <summary>Player 1 cyan, player 2 gold, player 3 violet, player 4 pink.</summary>
        public static int DefaultFor(int playerIndex) => Mathf.Clamp(playerIndex, 0, All.Length - 1);
    }
}
