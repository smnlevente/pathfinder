using Pathfinder.Game.Content;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Dresses the arena in a galaxy's theme (M7): the colour of the floor's grid, the walls and the background.
    /// Property blocks, so the shared materials are never changed. Presentation only.
    /// </summary>
    public sealed class ArenaTheme : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer floor;
        [SerializeField] Renderer[] walls = new Renderer[0];
        [SerializeField] Camera view;

        MaterialPropertyBlock _block;

        /// <summary>The theme showing now. For tests.</summary>
        public GalaxyThemeDefinition Current { get; private set; }

        public void Apply(GalaxyThemeDefinition theme)
        {
            if (theme == null || theme == Current)
                return;
            Current = theme;
            _block ??= new MaterialPropertyBlock();

            if (floor != null)
            {
                floor.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, theme.FloorTint);
                floor.SetPropertyBlock(_block);
            }
            foreach (var wall in walls)
            {
                if (wall == null)
                    continue;
                wall.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, theme.WallColor);
                wall.SetPropertyBlock(_block);
            }
            if (view != null)
                view.backgroundColor = theme.Background;
        }
    }
}
