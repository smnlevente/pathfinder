using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Shows a charge path while an enemy winds up (GDD §12.4): a chevron lane along the path, filling in as the launch
    /// approaches. Used by the Lancer and the Quarry Maw's sweeps.
    /// </summary>
    public sealed class TelegraphLine : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing IChargeTelegraph, usually the enemy's brain.")] MonoBehaviour source;
        [SerializeField] ChevronLane lane;

        IChargeTelegraph _telegraph;

        void Awake() => _telegraph = source as IChargeTelegraph;

        void LateUpdate()
        {
            if (_telegraph == null || !_telegraph.ShowsCharge)
            {
                lane.Hide();
                return;
            }

            lane.Show(_telegraph.ChargeStart, _telegraph.ChargeDirection, _telegraph.ChargeLength, _telegraph.ChargeProgress);
        }
    }
}
