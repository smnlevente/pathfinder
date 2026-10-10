using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A flat circle of constant thickness drawn with a line renderer, e.g. the Hive Matriarch's expanding bloom pulse:
    /// unlike a scaled ring texture, it doesn't get fatter as it grows. The owner sets the centre and radius.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RingLine : MonoBehaviour
    {
        [SerializeField, Min(8)] int segments = 72;
        [SerializeField] float height = 0.3f;

        LineRenderer _line;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = segments;
        }

        public void Show(Vector2 centre, float radius)
        {
            if (_line == null)
                Awake();
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                _line.SetPosition(i, new Vector3(centre.x + Mathf.Sin(angle) * radius, height, centre.y + Mathf.Cos(angle) * radius));
            }
        }
    }
}
