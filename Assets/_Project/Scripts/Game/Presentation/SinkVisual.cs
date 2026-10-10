using Pathfinder.Game.Enemies;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// Lowers a model into the ground as its owner burrows, shaking it while it digs, so diving and emerging read as
    /// motion instead of a pop (playtest 3). Presentation only.
    /// </summary>
    public sealed class SinkVisual : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing ISinkDepth: the boss brain or a body segment.")] MonoBehaviour source;
        [SerializeField] Transform model;
        [SerializeField, Min(0f)] float depthDistance = 1.8f;
        [SerializeField, Min(0f)] float shakeDegrees = 6f;

        ISinkDepth _depth;
        Vector3 _baseOffset;
        Quaternion _baseRotation;

        void Awake()
        {
            _depth = source as ISinkDepth;
            _baseOffset = model.localPosition;
            _baseRotation = model.localRotation;
        }

        void LateUpdate()
        {
            float depth = _depth != null ? Mathf.Clamp01(_depth.SinkDepth) : 0f;
            model.localPosition = _baseOffset + Vector3.down * (depth * depthDistance);

            // Digging in or out: a quick shudder.
            bool digging = depth > 0.001f && depth < 0.999f;
            float shake = digging ? shakeDegrees * Mathf.Sin(Time.time * 45f) : 0f;
            model.localRotation = _baseRotation * Quaternion.Euler(shake, 0f, shake * 0.6f);
        }
    }
}
