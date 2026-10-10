using Pathfinder.Core.Equipment;
using Pathfinder.Game.Content;
using UnityEngine;

namespace Pathfinder.Game.Equipment
{
    /// <summary>
    /// An item for the in-run build system (GDD §8). The definition is shared data. Each equipped copy gets its own
    /// <see cref="EquipmentEffect"/>, which applies stat modifiers and reacts to gameplay hooks.
    /// </summary>
    public abstract class EquipmentDefinition : ContentDefinition
    {
        [field: SerializeField] public EquipmentCategory Category { get; private set; } = EquipmentCategory.Hull;
        [field: SerializeField] public Rarity Rarity { get; private set; } = Rarity.Common;
        [field: SerializeField, Min(0f), Tooltip("Relative chance among items of the same rarity.")] public float Weight { get; private set; } = 1f;

        public abstract EquipmentEffect CreateEffect();

        /// <summary>Player-facing description of what the item does at a level (1–3).</summary>
        public abstract string Describe(int level);

        protected static float AtLevel(float[] values, int level) =>
            values == null || values.Length == 0 ? 0f : values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
    }
}
