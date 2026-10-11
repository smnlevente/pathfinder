using System;
using Pathfinder.Game.Presentation;
using UnityEngine;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        // Playtest 7: "everything is a ball". Each shooter now fires its own kind of shot. They all keep the hot magenta body
        // and dark outline of an enemy bullet (GDD §12.4), so they read as danger at a glance; the shape tells who fired it.
        // Shapes are flat textures facing up the texture, which is the projectile's direction of travel once laid flat.
        // A white core in the same shape, added on top, makes each one glow from the inside.

        /// <summary>
        /// A bullet shape from a signed distance (negative inside, in units where the texture spans -1..1): a white fill the
        /// material tints, a dark outline band inside the edge, and a one-pixel soft edge.
        /// </summary>
        static Texture2D BulletShape(string name, Func<float, float, float> distance, float outline = 0.14f, int size = 128) =>
            CreateTexture(name, size, (x, y) =>
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = distance(u, v), pixel = 2f / size;
                if (d > 0f)
                    return new Color(0f, 0f, 0f, 0.9f * Mathf.Clamp01(1f - d / pixel));
                float dark = Mathf.Clamp01((d + outline) / pixel + 0.5f); // 0 in the fill, 1 in the outline band
                return Color.Lerp(Color.white, new Color(0f, 0f, 0f, 0.9f), dark);
            }, TextureWrapMode.Clamp, alphaIsTransparency: true);

        /// <summary>Distance to a rhombus with half-width <paramref name="a"/> and half-length <paramref name="b"/>.</summary>
        static float Rhombus(float u, float v, float a, float b) => (Mathf.Abs(u) * b + Mathf.Abs(v) * a - a * b) / Mathf.Sqrt(a * a + b * b);

        static void BuildEnemyShots(Art art, Texture2D dot)
        {
            // Gunner: a dart, long nose and short tail, so it points where it's going.
            var dart = BulletShape("T_ShotDart", (u, v) => Rhombus(u, v, 0.55f, v > 0f ? 0.95f : 0.55f));
            // Spinner: a four-point star that spins as it flies.
            var star = BulletShape("T_ShotStar", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), angle = Mathf.Atan2(v, u);
                return (r - (0.36f + 0.6f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * angle)), 3f))) * 0.75f;
            });
            // Binary Wardens: a long six-sided crystal shard.
            var shard = BulletShape("T_ShotShard", (u, v) => Mathf.Max(Rhombus(u, v, 0.8f, 0.98f), Mathf.Abs(u) - 0.46f));
            // Hive Matriarch: an egg-shaped seed, pointed at the front.
            var seed = BulletShape("T_ShotSeed", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v), forward = r > 1e-4f ? Mathf.Max(0f, v / r) : 0f;
                return r - (0.55f + 0.4f * forward * forward);
            });

            var body = art.EnemyBulletBody.GetColor("_BaseColor");
            var core = new Color(2.2f, 1.7f, 2.2f);
            art.GunnerShot = Shot("PV_GunnerDart", art, Blended("M_FX_ShotDart", body, dart), Additive("M_FX_ShotDartCore", core, dart),
                new Vector2(0.58f, 0.86f), art.EnemyBulletHalo, 1.05f);
            art.SpinnerShot = Shot("PV_SpinnerStar", art, Blended("M_FX_ShotStar", body, star), Additive("M_FX_ShotStarCore", core, star),
                new Vector2(0.74f, 0.74f), art.EnemyBulletHalo, 1.1f, spin: 540f);
            art.WardenShot = Shot("PV_WardenShard", art, Blended("M_FX_ShotShard", body, shard), Additive("M_FX_ShotShardCore", core, shard),
                new Vector2(0.56f, 0.92f), Blended("M_FX_ShotShardHalo", new Color(0.85f, 0.25f, 1.6f, 0.55f), dot), 1.2f, flicker: true);
            art.MatriarchShot = Shot("PV_MatriarchSeed", art, Blended("M_FX_ShotSeed", body, seed), Additive("M_FX_ShotSeedCore", core, seed),
                new Vector2(0.58f, 0.72f), Blended("M_FX_ShotSeedHalo", new Color(0.4f, 1.6f, 0.3f, 0.5f), dot), 1.15f, pulse: true);
            art.MawShot = Rock(art);
        }

        /// <summary>A flat shot: a soft halo, the outlined body and a glowing core of the same shape.</summary>
        static GameObject Shot(string name, Art art, Material body, Material core, Vector2 size, Material halo, float haloSize,
            float spin = 0f, bool flicker = false, bool pulse = false)
        {
            var root = new GameObject(name);
            var haloQuad = FlatQuad("Halo", root.transform, halo, -0.02f, haloSize);
            SetFields(haloQuad.AddComponent<SimpleMotion>(), ("pulseAmount", flicker ? 0.3f : 0.15f), ("pulseFrequency", flicker ? 17f : 7f));

            // The shape turns about the floor's normal, so a pivot carries the spin and the quads lie flat under it.
            var shape = new GameObject("Shape").transform;
            shape.SetParent(root.transform, false);
            if (spin != 0f || pulse)
                SetFields(shape.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", spin), ("pulseAmount", pulse ? 0.12f : 0f), ("pulseFrequency", 5f));
            var bodyQuad = FlatQuad("Body", shape, body, 0f, 1f);
            bodyQuad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var coreQuad = FlatQuad("Core", shape, core, 0.02f, 1f);
            coreQuad.transform.localScale = new Vector3(size.x * 0.5f, size.y * 0.5f, 1f);
            return SavePrefab(root, "Projectiles");
        }

        /// <summary>The Quarry Maw's shot: a craggy rock chunk tumbling as it flies, glowing magenta from within.</summary>
        static GameObject Rock(Art art)
        {
            // Lighter than the floor, and hot inside: a magenta halo under it and a soft glow over it keep it reading as a shot.
            var rockMaterial = Lit("M_ShotRock", new Color(0.62f, 0.3f, 0.24f), smoothness: 0.2f, metallic: 0.1f, emission: new Color(0.9f, 0.12f, 0.6f));
            var root = new GameObject("PV_MawRock");
            var halo = FlatQuad("Halo", root.transform, art.EnemyBulletHalo, -0.02f, 1.4f);
            SetFields(halo.AddComponent<SimpleMotion>(), ("pulseAmount", 0.15f), ("pulseFrequency", 7f));
            FlatQuad("Glow", root.transform, art.FxMagenta, 0.32f, 0.75f);
            // A tilted pivot turns its tilted axis, so the chunk tumbles instead of just spinning.
            var tumble = new GameObject("Tumble").transform;
            tumble.SetParent(root.transform, false);
            tumble.localRotation = Quaternion.Euler(35f, 0f, 25f);
            SetFields(tumble.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", 300f));
            Primitive(PrimitiveType.Sphere, "Core", tumble, art.EnemyBulletCore, Vector3.zero, Vector3.one * 0.34f);
            Primitive(PrimitiveType.Cube, "Chunk A", tumble, rockMaterial, new Vector3(0.04f, 0f, 0.02f), new Vector3(0.36f, 0.3f, 0.32f), Quaternion.Euler(20f, 35f, 10f));
            Primitive(PrimitiveType.Cube, "Chunk B", tumble, rockMaterial, new Vector3(-0.06f, 0.04f, -0.05f), new Vector3(0.28f, 0.26f, 0.3f), Quaternion.Euler(-30f, 10f, 45f));
            Primitive(PrimitiveType.Cube, "Chunk C", tumble, rockMaterial, new Vector3(0.02f, -0.06f, 0.08f), new Vector3(0.22f, 0.2f, 0.24f), Quaternion.Euler(15f, -40f, -20f));
            return SavePrefab(root, "Projectiles");
        }
    }
}
