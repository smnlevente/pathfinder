using Pathfinder.Game.Combat;
using Pathfinder.Game.Localization;
using Pathfinder.Game.Player;
using UnityEngine.InputSystem;

namespace Pathfinder.Game.Presentation.Ui
{
    /// <summary>
    /// Who a menu is for in local co-op (M8): a "PLAYER 2" label in their colour, and the devices the menu should listen to,
    /// so each player picks their own equipment. Alone, there's no label and every device works.
    /// </summary>
    public static class PilotLabels
    {
        public static bool IsCoop => CombatRegistry.Players.Count > 1;

        /// <summary>"PLAYER 2" in the pilot's colour, and a gap, to go before a menu's title; empty when playing alone.</summary>
        public static string Prefix(PlayerShip pilot) =>
            IsCoop && pilot != null ? $"<color=#{UiKit.Hex(CoopRules.PilotColor(pilot.PlayerIndex))}>{Name(pilot.PlayerIndex)}</color>   " : string.Empty;

        /// <summary>"PLAYER 2".</summary>
        public static string Name(int index) => Loc.T("coop.player", ("number", index + 1));

        /// <summary>Whether this pilot plays with the mouse, so may click their own menus (always, when playing alone).</summary>
        public static bool UsesMouse(PlayerShip pilot) =>
            Devices(pilot) == null || LocalPlayers.ControlsOf(pilot.PlayerIndex) != LocalPlayers.Controls.Gamepad;

        /// <summary>"Only PLAYER 2 can choose here", in their colour, for a menu that is theirs; empty when playing alone.</summary>
        public static string OnlyFor(PlayerShip pilot) =>
            IsCoop && pilot != null ? UiKit.Colored(Loc.T("coop.menu_for", ("player", Name(pilot.PlayerIndex))), CoopRules.PilotColor(pilot.PlayerIndex)) : string.Empty;

        /// <summary>The devices a menu for this pilot listens to; null (every device) when playing alone.</summary>
        public static InputDevice[] Devices(PlayerShip pilot) =>
            IsCoop && pilot != null && LocalPlayers.IsCoop && pilot.PlayerIndex < LocalPlayers.Count ? LocalPlayers.DevicesFor(pilot.PlayerIndex) : null;
    }
}
