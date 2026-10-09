using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>Cosmetic spin and pulse for placeholder models and markers. Doesn't affect gameplay.</summary>
    public sealed class SimpleMotion : MonoBehaviour
    {
        [SerializeField, Tooltip("Degrees per second around local Y.")] float spinSpeed;
        [SerializeField, Min(0f), Tooltip("Scale pulse amplitude as a fraction of the base scale.")] float pulseAmount;
        [SerializeField, Min(0f)] float pulseFrequency = 2f;

        Vector3 _baseScale;

        void Awake() => _baseScale = transform.localScale;

        void Update()
        {
            if (spinSpeed != 0f)
                transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);

            if (pulseAmount > 0f)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * pulseAmount;
                transform.localScale = _baseScale * pulse;
            }
        }
    }
}
