using System.Collections.Generic;
using Pathfinder.Core.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Who is playing on this machine, and with what (M8, local co-op). Player 1 is always here. Alone, they play with any
    /// device. With company, every player has their own controls, chosen in the Hub by pressing a button: the keyboard
    /// and mouse, or one controller (playtest 8: "we need to specify which player uses which controls"). One physical
    /// controller can show up as several devices (a PlayStation pad plus a virtual Xbox pad from Steam or DS4Windows), so a
    /// player's controller is a list of devices. Kept across scene loads for the session.
    /// </summary>
    public static class LocalPlayers
    {
        public enum Controls
        {
            /// <summary>Every device nobody else uses: player 1 alone.</summary>
            Any,
            KeyboardMouse,
            Gamepad,
        }

        public sealed class Slot
        {
            public Controls Controls { get; internal set; }

            /// <summary>The devices of this player's controller, when <see cref="Controls"/> is <see cref="Controls.Gamepad"/>.</summary>
            public List<InputDevice> Gamepads { get; } = new List<InputDevice>();

            /// <summary>Their ship and weapons, chosen in the Hub. Player 1's is the run's own loadout.</summary>
            public PilotLoadout Loadout { get; set; }
        }

        static readonly List<Slot> s_slots = new List<Slot> { new Slot() };

        public static int Count => s_slots.Count;
        public static bool IsCoop => s_slots.Count > 1;
        public static IReadOnlyList<Slot> Slots => s_slots;

        /// <summary>Changes whenever someone joins, leaves or changes controls, so input sources know to look again.</summary>
        public static int Version { get; private set; }

        public static Controls ControlsOf(int index) => index >= 0 && index < s_slots.Count ? s_slots[index].Controls : Controls.Any;

        /// <summary>The player whose controls include this device, or -1. Player 1 playing with any device owns nothing.</summary>
        public static int OwnerOf(InputDevice device)
        {
            if (device == null)
                return -1;
            for (int i = 0; i < s_slots.Count; i++)
            {
                var slot = s_slots[i];
                if (slot.Controls == Controls.Gamepad && slot.Gamepads.Contains(device))
                    return i;
                if (slot.Controls == Controls.KeyboardMouse && IsKeyboardOrMouse(device))
                    return i;
            }
            return -1;
        }

        public static bool IsClaimed(InputDevice device) => OwnerOf(device) >= 0;

        static bool IsKeyboardOrMouse(InputDevice device) => device is Keyboard || device is Mouse;

        static int KeyboardOwner()
        {
            for (int i = 0; i < s_slots.Count; i++)
            {
                if (s_slots[i].Controls == Controls.KeyboardMouse)
                    return i;
            }
            return -1;
        }

        /// <summary>A new player can join with this controller (or the keyboard, if nobody else has it).</summary>
        public static bool CanJoin(InputDevice device) =>
            s_slots.Count < CoopRules.MaxPilots && device != null && (device is Gamepad || IsKeyboardOrMouse(device)) && !IsClaimed(device);

        /// <summary>
        /// A new player joins with a controller (all its devices) and starts with player 1's loadout. Returns the new
        /// player's index, or -1. Player 1, if they were playing with any device, takes <paramref name="playerOne"/> (the
        /// controller they were using) or else the keyboard and mouse.
        /// </summary>
        public static int Join(InputDevice[] controller, InputDevice[] playerOne = null) => Join(Controls.Gamepad, controller, playerOne);

        /// <summary>A new player joins with one controller device.</summary>
        public static int Join(InputDevice controller) => Join(Controls.Gamepad, new[] { controller }, null);

        /// <summary>
        /// A new player joins with the keyboard and mouse. Player 1, if they were playing with any device, takes
        /// <paramref name="playerOne"/>, or else keeps every controller nobody else has.
        /// </summary>
        public static int JoinWithKeyboard(InputDevice[] playerOne = null) => Join(Controls.KeyboardMouse, null, playerOne);

        static int Join(Controls controls, InputDevice[] controller, InputDevice[] playerOne)
        {
            if (s_slots.Count >= CoopRules.MaxPilots)
                return -1;
            if (controls == Controls.Gamepad && (controller == null || controller.Length == 0 || !(controller[0] is Gamepad) || IsClaimed(controller[0])))
                return -1;
            if (controls == Controls.KeyboardMouse && KeyboardOwner() >= 0)
                return -1;

            var slot = new Slot { Controls = controls, Loadout = s_slots[0].Loadout };
            if (controller != null)
            {
                foreach (var device in controller)
                {
                    if (device is Gamepad && !IsClaimed(device) && !slot.Gamepads.Contains(device))
                        slot.Gamepads.Add(device);
                }
            }
            s_slots.Add(slot);
            // Player 1 no longer takes every device: the controller they were using, or the keyboard and mouse.
            if (s_slots[0].Controls == Controls.Any)
            {
                var own = new List<InputDevice>();
                if (playerOne != null)
                    own.AddRange(System.Array.FindAll(playerOne, device => device is Gamepad && !IsClaimed(device)));
                if (own.Count > 0)
                {
                    s_slots[0].Controls = Controls.Gamepad;
                    s_slots[0].Gamepads.AddRange(own);
                }
                else if (KeyboardOwner() < 0)
                {
                    s_slots[0].Controls = Controls.KeyboardMouse;
                }
            }
            Version++;
            return s_slots.Count - 1;
        }

        /// <summary>
        /// Gives a player the keyboard and mouse, or a controller (all its devices). If another player had them, the two
        /// swap controls, so nobody is ever left without any.
        /// </summary>
        public static void Assign(int index, Controls controls, params InputDevice[] controller)
        {
            if (index < 0 || index >= s_slots.Count)
                return;
            var slot = s_slots[index];
            var previousControls = slot.Controls;
            var previousGamepads = new List<InputDevice>(slot.Gamepads);

            int other = controls == Controls.KeyboardMouse ? KeyboardOwner()
                : controls == Controls.Gamepad && controller != null && controller.Length > 0 ? OwnerOf(controller[0]) : -1;
            if (other == index)
                other = -1;

            slot.Controls = controls;
            slot.Gamepads.Clear();
            if (controls == Controls.Gamepad && controller != null)
            {
                foreach (var device in controller)
                {
                    if (device is Gamepad && !slot.Gamepads.Contains(device))
                        slot.Gamepads.Add(device);
                }
            }

            if (other >= 0)
            {
                var swapped = s_slots[other];
                swapped.Gamepads.Clear();
                swapped.Controls = previousControls == Controls.Any ? (controls == Controls.KeyboardMouse ? Controls.Gamepad : Controls.KeyboardMouse) : previousControls;
                if (swapped.Controls == Controls.Gamepad)
                {
                    foreach (var device in previousGamepads)
                    {
                        if (!slot.Gamepads.Contains(device))
                            swapped.Gamepads.Add(device);
                    }
                }
            }
            Version++;
        }

        /// <summary>Another device of a player's controller, seen pressing together with it (a virtual twin).</summary>
        public static void AddTwin(int index, InputDevice twin)
        {
            if (index < 0 || index >= s_slots.Count || !(twin is Gamepad) || IsClaimed(twin))
                return;
            var slot = s_slots[index];
            if (slot.Controls != Controls.Gamepad)
                return;
            slot.Gamepads.Add(twin);
            Version++;
        }

        /// <summary>A player other than player 1 leaves; those after them move up. Alone again, player 1 plays with anything.</summary>
        public static void Leave(int index)
        {
            if (index <= 0 || index >= s_slots.Count)
                return;
            s_slots.RemoveAt(index);
            if (s_slots.Count == 1)
            {
                s_slots[0].Controls = Controls.Any;
                s_slots[0].Gamepads.Clear();
            }
            Version++;
        }

        public static PilotLoadout LoadoutFor(int index) => index >= 0 && index < s_slots.Count ? s_slots[index].Loadout : default;

        public static void SetLoadout(int index, PilotLoadout loadout)
        {
            if (index >= 0 && index < s_slots.Count)
                s_slots[index].Loadout = loadout;
        }

        /// <summary>
        /// The devices a player's controls listen to: the keyboard and mouse, or their controller's devices. Null means every
        /// device (player 1 alone). Player 1 set to any device with company gets everything nobody else uses.
        /// </summary>
        public static InputDevice[] DevicesFor(int index)
        {
            if (index < 0 || index >= s_slots.Count)
                return new InputDevice[0];
            var slot = s_slots[index];
            var devices = new List<InputDevice>();
            switch (slot.Controls)
            {
                case Controls.Gamepad:
                    devices.AddRange(slot.Gamepads);
                    break;
                case Controls.KeyboardMouse:
                    foreach (var device in InputSystem.devices)
                    {
                        if (IsKeyboardOrMouse(device))
                            devices.Add(device);
                    }
                    break;
                default:
                    if (!IsCoop)
                        return null;
                    foreach (var device in InputSystem.devices)
                    {
                        if (OwnerOf(device) < 0)
                            devices.Add(device);
                    }
                    break;
            }
            return devices.ToArray();
        }

        /// <summary>"Keyboard & mouse", a controller's name, or "Any".</summary>
        public static string DescribeControls(int index)
        {
            var slot = index >= 0 && index < s_slots.Count ? s_slots[index] : null;
            if (slot == null)
                return string.Empty;
            switch (slot.Controls)
            {
                case Controls.KeyboardMouse:
                    return Localization.Loc.T("controls.keyboard");
                case Controls.Gamepad:
                    var pad = slot.Gamepads.Count > 0 ? slot.Gamepads[0] : null;
                    string name = pad != null && !string.IsNullOrEmpty(pad.displayName) ? pad.displayName : Localization.Loc.T("controls.controller");
                    return pad != null && pad.added ? name : Localization.Loc.T("controls.disconnected", ("name", name));
                default:
                    return Localization.Loc.T("controls.any");
            }
        }

        /// <summary>Back to player 1 alone, playing with any device.</summary>
        public static void Reset()
        {
            var first = s_slots.Count > 0 ? s_slots[0] : new Slot();
            s_slots.Clear();
            first.Controls = Controls.Any;
            first.Gamepads.Clear();
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
