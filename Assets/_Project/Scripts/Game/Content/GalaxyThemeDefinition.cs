using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// How a galaxy looks (M7, GDD §2): the arena floor's grid glow and tint, the walls and the space behind. Each galaxy
    /// has its own, so a run visibly moves somewhere new after every boss. Kept dim and low in saturation, so the
    /// gameplay colours (cyan pilots, magenta shots, red-orange enemies, green Swarm) always stand out against it.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Galaxy Theme", fileName = "Theme_")]
    public sealed class GalaxyThemeDefinition : ContentDefinition
    {
        [field: SerializeField, Tooltip("The floor's grid lines (the grid texture is neutral; this colours it). The squares between take a darker shade.")]
        public Color FloorTint { get; private set; } = new Color(0.16f, 0.36f, 0.46f);
        [field: SerializeField, ColorUsage(false, true)] public Color WallColor { get; private set; } = new Color(0.3f, 1.3f, 2.2f);
        [field: SerializeField, Tooltip("The space behind the arena.")] public Color Background { get; private set; } = new Color(0.02f, 0.02f, 0.05f);
    }
}
