using UnityEngine;

namespace Pathfinder.Game.Simulation
{
    /// <summary>
    /// Gameplay happens on the XZ plane (GDD §14.1). The simulation uses <see cref="Vector2"/> (x, z),
    /// and these helpers convert to and from world space.
    /// </summary>
    public static class PlaneMath
    {
        public static Vector3 ToWorld(Vector2 point, float height = 0f) => new Vector3(point.x, height, point.y);

        public static Vector2 ToPlane(Vector3 world) => new Vector2(world.x, world.z);

        /// <summary>Unity yaw in degrees for a plane direction: 0 = +Z (up the screen), 90 = +X.</summary>
        public static float HeadingDegrees(Vector2 direction) => Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

        public static Vector2 FromHeading(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }

        public static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        /// <summary>
        /// First point where the segment a→b enters the circle, as a fraction of the segment. A segment
        /// that starts inside the circle hits at t = 0. Sweeping like this stops fast projectiles from
        /// tunnelling through targets between ticks.
        /// </summary>
        public static bool SegmentHitsCircle(Vector2 a, Vector2 b, Vector2 center, float radius, out float t)
        {
            Vector2 fromCenter = a - center;
            float c = Vector2.Dot(fromCenter, fromCenter) - radius * radius;
            if (c <= 0f)
            {
                t = 0f;
                return true;
            }

            Vector2 d = b - a;
            float lengthSquared = Vector2.Dot(d, d);
            float halfB = Vector2.Dot(fromCenter, d);
            float discriminant = halfB * halfB - lengthSquared * c;
            if (lengthSquared < 1e-8f || halfB > 0f || discriminant < 0f)
            {
                t = 0f;
                return false;
            }

            t = (-halfB - Mathf.Sqrt(discriminant)) / lengthSquared;
            return t <= 1f;
        }
    }
}
