using Pathfinder.Game.Combat;
using Pathfinder.Game.Player;
using UnityEngine;

namespace Pathfinder.Game.Presentation
{
    /// <summary>
    /// A ring on the floor under a pilot's ship, in their colour, so local co-op players can tell their ships apart (M8).
    /// Under a downed pilot's wreck it dims and pulses, and a brighter arc fills around it as a teammate revives them. Shown
    /// only while more than one pilot is flying. Presentation only.
    /// </summary>
    public sealed class PilotMarker : MonoBehaviour
    {
        [SerializeField] LineRenderer ring;
        [SerializeField] LineRenderer reviveArc;
        [SerializeField, Min(0.1f)] float radius = 1.15f;
        [SerializeField, Min(8)] int segments = 64;

        PlayerShip _ship;
        float _shownProgress = -1f;

        /// <summary>Whether the ring is showing. For tests.</summary>
        public bool Shown => ring != null && ring.enabled;

        void Awake()
        {
            _ship = GetComponentInParent<PlayerShip>();
            transform.localPosition = new Vector3(0f, 0.04f, 0f);
            if (ring != null)
            {
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = segments;
                for (int i = 0; i < segments; i++)
                    ring.SetPosition(i, Point(i / (float)segments, radius));
            }
            if (reviveArc != null)
            {
                reviveArc.useWorldSpace = false;
                reviveArc.loop = false;
                reviveArc.enabled = false;
            }
        }

        void LateUpdate()
        {
            bool coop = _ship != null && CombatRegistry.Players.Count > 1;
            if (ring != null)
                ring.enabled = coop;
            if (!coop)
            {
                if (reviveArc != null)
                    reviveArc.enabled = false;
                return;
            }

            // The ship turns with its aim; the ring stays put on the floor.
            transform.rotation = Quaternion.identity;
            var color = CoopRules.PilotColor(_ship.PlayerIndex);
            float alpha = _ship.IsAlive ? 0.85f : 0.3f + 0.25f * Mathf.Sin(Time.time * 5f);
            SetColor(ring, new Color(color.r, color.g, color.b, alpha));

            float progress = _ship.IsAlive ? 0f : _ship.ReviveProgress;
            if (reviveArc == null)
                return;
            reviveArc.enabled = progress > 0f;
            if (progress > 0f && !Mathf.Approximately(progress, _shownProgress))
            {
                _shownProgress = progress;
                int points = Mathf.Max(2, Mathf.CeilToInt(segments * progress) + 1);
                reviveArc.positionCount = points;
                for (int i = 0; i < points; i++)
                    reviveArc.SetPosition(i, Point(progress * i / (points - 1), radius * 1.18f));
                SetColor(reviveArc, Color.Lerp(color, Color.white, 0.35f));
            }
        }

        /// <summary>A point on the circle, a fraction of the way round clockwise from the top.</summary>
        static Vector3 Point(float fraction, float circleRadius)
        {
            float angle = fraction * Mathf.PI * 2f;
            return new Vector3(Mathf.Sin(angle) * circleRadius, 0f, Mathf.Cos(angle) * circleRadius);
        }

        static void SetColor(LineRenderer line, Color color)
        {
            if (line == null)
                return;
            line.startColor = color;
            line.endColor = color;
        }
    }
}
