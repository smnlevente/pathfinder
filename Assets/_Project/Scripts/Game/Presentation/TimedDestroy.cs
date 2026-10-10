using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>Destroys a one-shot effect after a delay. Presentation only.</summary>
    public sealed class TimedDestroy : MonoBehaviour
    {
        [SerializeField, Min(0f)] float lifetime = 0.3f;

        void Start() => Destroy(gameObject, lifetime);
    }
}
