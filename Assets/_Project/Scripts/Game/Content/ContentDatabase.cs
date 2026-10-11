using System.Collections.Generic;
using Pathfinder.Game.Equipment;
using UnityEngine;

namespace Pathfinder.Game.Content
{
    /// <summary>
    /// Every piece of content, looked up by stable ID (GDD §14.4). Loadouts, saves and (later) network messages store
    /// IDs, and this resolves them back to definitions.
    /// </summary>
    [CreateAssetMenu(menuName = "Pathfinder/Content Database", fileName = "ContentDatabase")]
    public sealed class ContentDatabase : ScriptableObject
    {
        [field: SerializeField] public ShipDefinition[] Ships { get; private set; } = new ShipDefinition[0];
        [field: SerializeField] public PrimaryWeaponDefinition[] PrimaryWeapons { get; private set; } = new PrimaryWeaponDefinition[0];
        [field: SerializeField] public SecondaryWeaponDefinition[] SecondaryWeapons { get; private set; } = new SecondaryWeaponDefinition[0];
        [field: SerializeField, Tooltip("Items that can drop and be sold. Ship traits are not listed here.")] public EquipmentDefinition[] Equipment { get; private set; } = new EquipmentDefinition[0];
        [field: SerializeField, Tooltip("Every enemy and boss.")] public EnemyDefinition[] Enemies { get; private set; } = new EnemyDefinition[0];
        [field: SerializeField, Tooltip("Hub upgrades and unlocks (GDD §11.2).")] public Meta.MetaUpgradeDefinition[] MetaUpgrades { get; private set; } = new Meta.MetaUpgradeDefinition[0];
        [field: SerializeField, Tooltip("Mystery events (GDD §10.5).")] public MysteryEventDefinition[] Events { get; private set; } = new MysteryEventDefinition[0];
        [field: SerializeField, Tooltip("Currency and map node icons.")] public GameIcons Icons { get; private set; }

        public T Find<T>(IEnumerable<T> items, string id) where T : ContentDefinition
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var item in items)
            {
                if (item != null && item.Id == id)
                    return item;
            }
            return null;
        }

        public ShipDefinition FindShip(string id) => Find(Ships, id);
        public PrimaryWeaponDefinition FindPrimary(string id) => Find(PrimaryWeapons, id);
        public SecondaryWeaponDefinition FindSecondary(string id) => Find(SecondaryWeapons, id);
        public EquipmentDefinition FindEquipment(string id) => Find(Equipment, id);
        public EnemyDefinition FindEnemy(string id) => Find(Enemies, id);
    }
}
