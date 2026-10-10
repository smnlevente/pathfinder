using System.Collections.Generic;
using Pathfinder.Core.Equipment;
using Pathfinder.Core.Random;
using Pathfinder.Game.Content;
using Pathfinder.Game.Player;

namespace Pathfinder.Game.Equipment
{
    /// <summary>
    /// Rolls equipment for a pilot (GDD §8.6): caches, boss rewards, the Shop and Survival's upgrades. Items the pilot
    /// already has at level III, and items that don't suit their loadout, are left out.
    /// </summary>
    public static class EquipmentOffers
    {
        public static List<EquipmentDefinition> Roll(ContentDatabase content, PlayerShip pilot, int count, int galaxyIndex, Rarity minimum, IRandom random)
        {
            var pool = content.Equipment;
            var candidates = new LootCandidate[pool.Length];
            for (int i = 0; i < pool.Length; i++)
            {
                bool excluded = pilot.PreviewEquip(pool[i]) == EquipOutcome.AlreadyMaxed || !pool[i].SuitsPilot(pilot);
                candidates[i] = new LootCandidate(pool[i].Rarity, pool[i].Weight, excluded);
            }

            int total = count + pilot.Definition.ExtraEquipmentChoices;
            var offer = new List<EquipmentDefinition>();
            foreach (int index in EquipmentRoller.Roll(candidates, total, galaxyIndex, minimum, random))
                offer.Add(pool[index]);
            return offer;
        }
    }
}
