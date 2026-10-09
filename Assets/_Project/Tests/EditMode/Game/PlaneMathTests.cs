using NUnit.Framework;
using Pathfinder.Game.Simulation;
using UnityEngine;

namespace Pathfinder.Tests.Game
{
    public class PlaneMathTests
    {
        [Test]
        public void Heading_ZeroIsUpScreen_NinetyIsRight()
        {
            Assert.AreEqual(0f, PlaneMath.HeadingDegrees(Vector2.up), 1e-4f);
            Assert.AreEqual(90f, PlaneMath.HeadingDegrees(Vector2.right), 1e-4f);
            Assert.That(Vector2.Distance(Vector2.right, PlaneMath.FromHeading(90f)), Is.LessThan(1e-5f));
        }

        [Test]
        public void ToWorld_MapsPlaneOntoXZ()
        {
            Assert.AreEqual(new Vector3(1f, 0.5f, 2f), PlaneMath.ToWorld(new Vector2(1f, 2f), 0.5f));
            Assert.AreEqual(new Vector2(1f, 2f), PlaneMath.ToPlane(new Vector3(1f, 9f, 2f)));
        }

        [Test]
        public void Segment_PassingThroughCircle_HitsAtEntryPoint()
        {
            Assert.IsTrue(PlaneMath.SegmentHitsCircle(new Vector2(-5f, 0f), new Vector2(5f, 0f), Vector2.zero, 1f, out float t));
            Assert.AreEqual(0.4f, t, 1e-5f);
        }

        [Test]
        public void Segment_StartingInsideCircle_HitsImmediately()
        {
            Assert.IsTrue(PlaneMath.SegmentHitsCircle(new Vector2(0.2f, 0f), new Vector2(5f, 0f), Vector2.zero, 1f, out float t));
            Assert.AreEqual(0f, t);
        }

        [TestCase(-5f, 2f, 5f, 2f)]    // passes beside the circle
        [TestCase(-5f, 0f, -2f, 0f)]   // stops short of it
        [TestCase(2f, 0f, 5f, 0f)]     // moving away from it
        public void Segment_Missing_DoesNotHit(float ax, float ay, float bx, float by)
        {
            Assert.IsFalse(PlaneMath.SegmentHitsCircle(new Vector2(ax, ay), new Vector2(bx, by), Vector2.zero, 1f, out _));
        }

        [Test]
        public void FastSegment_DoesNotTunnelThroughSmallCircle()
        {
            // A projectile moving 50 u in one tick still hits a 0.2 u target in the middle of its path.
            Assert.IsTrue(PlaneMath.SegmentHitsCircle(new Vector2(-25f, 0f), new Vector2(25f, 0f), Vector2.zero, 0.2f, out _));
        }
    }
}
