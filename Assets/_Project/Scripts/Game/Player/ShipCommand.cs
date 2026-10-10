using UnityEngine;

namespace Pathfinder.Game.Player
{
    /// <summary>
    /// Everything a pilot can ask the ship to do in one simulation tick. Ships consume commands and never
    /// read devices, so a command can come from a local device, a network peer, a bot or a test
    /// (GDD §14.4).
    /// </summary>
    public struct ShipCommand
    {
        /// <summary>Desired movement on the plane (x, z), magnitude ≤ 1.</summary>
        public Vector2 Move;

        /// <summary>Aim direction on the plane. Zero means "keep the current aim".</summary>
        public Vector2 Aim;

        public bool FirePrimary;

        /// <summary>Held while the secondary button is down. The ship fires one salvo per press, not per tick held.</summary>
        public bool FireSecondary;

        /// <summary>The following are one-shot presses, true for a single tick.</summary>
        public bool Dash;
        public bool Reload;
        public bool Interact;
    }

    public interface IShipCommandSource
    {
        /// <summary>Called once per simulation tick by the ship it controls.</summary>
        ShipCommand ReadCommand(PlayerShip ship);
    }
}
