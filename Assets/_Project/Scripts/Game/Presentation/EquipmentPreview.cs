using Pathfinder.Game.Equipment;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// The item a menu is pointing at (a choice card, a Shop row), so the HUD's ship diagram can light up where it would go
    /// (playtest 7: "I have no idea how many slots of which things I have"). In the swap step it also names the slot that
    /// would be replaced. The menu that set it clears it when it moves on or closes.
    /// </summary>
    public static class EquipmentPreview
    {
        static object s_owner;

        public static EquipmentDefinition Item { get; private set; }

        /// <summary>The pilot the menu is for, whose ship diagram should light up (M8, local co-op); null for whoever.</summary>
        public static Player.PlayerShip Pilot { get; private set; }

        /// <summary>The slot, within the item's category, that a swap would replace, or -1.</summary>
        public static int ReplaceIndex { get; private set; } = -1;

        public static void Show(object owner, EquipmentDefinition item, int replaceIndex = -1, Player.PlayerShip pilot = null)
        {
            s_owner = owner;
            Pilot = pilot;
            Item = item;
            ReplaceIndex = item != null ? replaceIndex : -1;
        }

        /// <summary>Clears the preview if <paramref name="owner"/> set it.</summary>
        public static void Clear(object owner)
        {
            if (s_owner != owner)
                return;
            s_owner = null;
            Pilot = null;
            Item = null;
            ReplaceIndex = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_owner = null;
            Pilot = null;
            Item = null;
            ReplaceIndex = -1;
        }
    }
}
