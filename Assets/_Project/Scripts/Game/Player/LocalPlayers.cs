using System.Collections.Generic;
using Pathfinder.Core.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Who is playing on this machine (M8, local co-op). Player 1 is always here and plays with every device nobody else
    /// has claimed: keyboard and mouse, or a gamepad. Each other player joins in the Hub with a gamepad of their own, and
    /// picks a loadout. Kept across scene loads for the session.
    /// </summary>
    public static class LocalPlayers
    {
        public sealed class Slot
        {
            /// <summary>The gamepad this player joined with; none for player 1.</summary>
            public InputDevice Device { get; internal set; }

            /// <summary>Their ship and weapons, chosen in the Hub. Player 1's is the run's own loadout.</summary>
            public PilotLoadout Loadout { get; set; }
        }

        static readonly List<Slot> s_slots = new List<Slot> { new Slot() };

        public static int Count => s_slots.Count;
        public static bool IsCoop => s_slots.Count > 1;
        public static IReadOnlyList<Slot> Slots => s_slots;

        /// <summary>Changes whenever someone joins or leaves, so input sources know to look again.</summary>
        public static int Version { get; private set; }

        public static bool IsClaimed(InputDevice device) => IndexOf(device) >= 0;

        /// <summary>The player who joined with this device, or -1. Player 1 never claims one.</summary>
        public static int IndexOf(InputDevice device)
        {
            if (device == null)
                return -1;
            for (int i = 1; i < s_slots.Count; i++)
            {
                if (s_slots[i].Device == device)
                    return i;
            }
            return -1;
        }

        public static bool CanJoin(InputDevice device) => device is Gamepad && s_slots.Count < CoopRules.MaxPilots && !IsClaimed(device);

        /// <summary>A new player joins with their gamepad, starting with player 1's loadout. Returns their index, or -1.</summary>
        public static int Join(InputDevice device)
        {
            if (!CanJoin(device))
                return -1;
            s_slots.Add(new Slot { Device = device, Loadout = s_slots[0].Loadout });
            Version++;
            return s_slots.Count - 1;
        }

        /// <summary>A player other than player 1 leaves; those after them move up.</summary>
        public static void Leave(int index)
        {
            if (index <= 0 || index >= s_slots.Count)
                return;
            s_slots.RemoveAt(index);
            Version++;
        }

        public static PilotLoadout LoadoutFor(int index) => index >= 0 && index < s_slots.Count ? s_slots[index].Loadout : default;

        public static void SetLoadout(int index, PilotLoadout loadout)
        {
            if (index >= 0 && index < s_slots.Count)
                s_slots[index].Loadout = loadout;
        }

        /// <summary>
        /// The devices a player's controls listen to: their own gamepad, or for player 1 every device nobody else claimed.
        /// Null means every device (player 1 alone).
        /// </summary>
        public static InputDevice[] DevicesFor(int index)
        {
            if (index > 0)
                return index < s_slots.Count && s_slots[index].Device != null ? new[] { s_slots[index].Device } : new InputDevice[0];
            if (!IsCoop)
                return null;
            var devices = new List<InputDevice>();
            foreach (var device in InputSystem.devices)
            {
                if (!IsClaimed(device))
                    devices.Add(device);
            }
            return devices.ToArray();
        }

        /// <summary>Back to player 1 alone.</summary>
        public static void Reset()
        {
            var first = s_slots.Count > 0 ? s_slots[0] : new Slot();
            s_slots.Clear();
            s_slots.Add(first);
            Version++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_slots.Clear();
            s_slots.Add(new Slot());
            Version++;
        }
    }
}
