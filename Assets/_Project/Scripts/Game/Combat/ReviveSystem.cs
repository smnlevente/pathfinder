using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Combat
{
    /// <summary>
    /// Local co-op revives (M8). A downed pilot's ship stays where it fell as a wreck. A living teammate hovering within
    /// <see cref="CoopRules.ReviveRadius"/> fills its revive progress over <see cref="CoopRules.ReviveTime"/>; on its own,
    /// the progress slowly drains. When it fills, the pilot is back with a little hull. The run is only lost when every
    /// pilot is down. Part of the fixed tick, after the ships move.
    /// </summary>
    public sealed class ReviveSystem
    {
        public void Simulate(float deltaTime)
        {
            var players = CombatRegistry.Players;
            if (players.Count < 2)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                var downed = players[i];
                if (downed.IsAlive)
                    continue;

                bool helped = false;
                foreach (var helper in players)
                {
                    if (helper != downed && helper.IsAlive
                        && (helper.Position - downed.Position).sqrMagnitude <= CoopRules.ReviveRadius * CoopRules.ReviveRadius)
                    {
                        helped = true;
                        break;
                    }
                }

                float rate = deltaTime / CoopRules.ReviveTime;
                downed.ReviveProgress = Mathf.Clamp01(downed.ReviveProgress + (helped ? rate : -rate * CoopRules.ReviveDrain));
                if (downed.ReviveProgress >= 1f)
                    downed.Revive(CoopRules.ReviveHullFraction);
            }
        }
    }
}
