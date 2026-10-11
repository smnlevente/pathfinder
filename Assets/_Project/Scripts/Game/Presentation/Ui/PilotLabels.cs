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

        /// <summary>The devices a menu for this pilot listens to; null (every device) when playing alone.</summary>
        public static InputDevice[] Devices(PlayerShip pilot) =>
            IsCoop && pilot != null && LocalPlayers.IsCoop && pilot.PlayerIndex < LocalPlayers.Count ? LocalPlayers.DevicesFor(pilot.PlayerIndex) : null;
    }
}
