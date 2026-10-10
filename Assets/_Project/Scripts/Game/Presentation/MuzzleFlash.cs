using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>A small flash at the gun each time the ship fires its primary. Presentation only.</summary>
    public sealed class MuzzleFlash : MonoBehaviour
    {
        [SerializeField] PlayerShip ship;
        [SerializeField] GameObject flashPrefab;
        [SerializeField, Min(0f)] float forwardOffset = 0.85f;

        void OnEnable() => ship.Fired += OnFired;
        void OnDisable() => ship.Fired -= OnFired;

        void OnFired(PlayerShip source)
        {
            if (flashPrefab != null)
                Instantiate(flashPrefab, source.transform.position + PlaneMath.ToWorld(source.AimDirection * forwardOffset, ProjectileSystem.VisualHeight), Quaternion.identity);
        }
    }
}
