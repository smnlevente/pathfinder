using Pathfinder.Core.Run;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// Pictures for things that aren't content definitions: the currencies and the galaxy map's node types (playtest 7:
    /// fewer words, more pictures, so younger players can follow). Content definitions carry their own
    /// <see cref="ContentDefinition.Icon"/>. Icons are white on transparent; the UI tints them.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Game Icons", fileName = "GameIcons")]
    public sealed class GameIcons : ScriptableObject
    {
        [field: Header("Currencies")]
        [field: SerializeField] public Sprite Scraps { get; private set; }
        [field: SerializeField] public Sprite DarkMatter { get; private set; }
        [field: SerializeField] public Sprite Cores { get; private set; }

        [field: Header("Map nodes (Scraps and Dark Matter use the currency icons)")]
        [field: SerializeField] public Sprite Equipment { get; private set; }
        [field: SerializeField] public Sprite Mystery { get; private set; }
        [field: SerializeField] public Sprite Repair { get; private set; }
        [field: SerializeField] public Sprite Shop { get; private set; }
        [field: SerializeField] public Sprite Boss { get; private set; }

        [field: Header("Stations")]
        [field: SerializeField, Tooltip("The repair station's Reinforce option: more max hull.")] public Sprite Reinforce { get; private set; }

        public Sprite ForNode(MapNodeType type) => type switch
        {
            MapNodeType.Equipment => Equipment,
            MapNodeType.Scraps => Scraps,
            MapNodeType.DarkMatter => DarkMatter,
            MapNodeType.Mystery => Mystery,
            MapNodeType.Repair => Repair,
            MapNodeType.Shop => Shop,
            MapNodeType.Boss => Boss,
            _ => null,
        };
    }
}
