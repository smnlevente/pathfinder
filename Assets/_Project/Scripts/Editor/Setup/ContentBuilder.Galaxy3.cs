using System.Collections.Generic;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        // Galaxy 3: the Hive Matriarch and her brood (GDD §7.5). The Swarm's own colour is a toxic green glow: on its
        // creatures, its spore clouds and its bloom pulses, so it never reads as a Remnant machine or a bullet.

        sealed class Matriarch
        {
            public EnemyDefinition Boss, Broodling;
            public GameObject SporeMark, SporeCloud, SporeBurst;
        }

        static Matriarch BuildHiveMatriarch(Art art)
        {
            var dot = Load<Texture2D>(TexturesDir + "/T_SoftDot.png");
            var beam = Load<Texture2D>(TexturesDir + "/T_Beam.png");

            // Playtest 7: the first spore marks were 64 and 128 px textures stretched over a 5 u circle, and read as blurry.
            // The new ones draw their rings as geometry with a crisp line profile (T_LineCore, from the art step), and fill
            // with textures sized for the area.
            var lineCore = Load<Texture2D>(TexturesDir + "/T_LineCore.png");
            var disc = Load<Texture2D>(TexturesDir + "/T_Disc.png");
            var sharpRing = Load<Texture2D>(TexturesDir + "/T_RingSharp.png");
            var fog = CreateTexture("T_SporeFog", 256, (x, y) =>
            {
                float u = (x + 0.5f) / 256f, v = (y + 0.5f) / 256f;
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float noise = 0f, amplitude = 0.5f, frequency = 4f;
                for (int octave = 0; octave < 4; octave++)
                {
                    noise += amplitude * Mathf.PerlinNoise(u * frequency + 17.3f * octave, v * frequency + 5.1f * octave);
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }
                return new Color(1f, 1f, 1f, Mathf.Clamp01(falloff * falloff * (noise * 1.8f - 0.3f)));
            }, TextureWrapMode.Clamp, alphaIsTransparency: true);

            var swarmGlow = Unlit("M_SwarmGlow", new Color(0.5f, 3.2f, 0.7f));
            var sporeLine = Additive("M_FX_SporeRing", new Color(0.7f, 3f, 0.5f), lineCore);
            var sporeFill = Additive("M_FX_SporeFill", new Color(0.25f, 1.1f, 0.2f, 0.6f), disc);
            var sporeArea = Blended("M_FX_SporeArea", new Color(0.2f, 0.85f, 0.18f, 0.2f), disc);
            var sporeFog = Blended("M_FX_SporeFog", new Color(0.45f, 1.6f, 0.35f, 0.42f), fog);
            var sporeGlow = Additive("M_FX_SporeGlow", new Color(0.45f, 2f, 0.35f), dot);
            var sporeBurstRing = Additive("M_FX_SporeBurst", new Color(0.6f, 3f, 0.45f), sharpRing);
            var pulseLine = Additive("M_FX_BloomPulse", new Color(0.35f, 1.9f, 0.3f), beam);

            var broodling = Definition<EnemyDefinition>("Enemies/Enemy_Broodling", "enemy.broodling", "Broodling", so => Set(so,
                ("MaxHealth", 12f), ("MoveSpeed", 6.5f), ("ContactDamage", 8f), ("BudgetCost", 1), ("ScrapsDrop", 0), ("ScrapsDropChance", 0f)));
            var boss = Definition<EnemyDefinition>("Enemies/Enemy_HiveMatriarch", "boss.hive-matriarch", "The Hive Matriarch", so => Set(so,
                ("MaxHealth", 4800f), ("MoveSpeed", 4f), ("ContactDamage", 20f), ("BudgetCost", 30), ("ScrapsDrop", 40), ("ScrapsDropChance", 1f),
                ("KnockbackMultiplier", 0f)));

            // Spore pod landing mark: the pod in flight, a crisp outline, a fill growing to it and a ring closing in on it.
            var mark = new GameObject("FX_SporeMark");
            var fill = FlatQuad("Fill", mark.transform, sporeFill, 0.03f, 1f);
            var outline = FloorRing("Outline", mark.transform, sporeLine, 0.12f, 0.05f);
            var closing = FloorRing("ClosingRing", mark.transform, sporeLine, 0.16f, 0.06f);
            var pod = new GameObject("Pod");
            pod.transform.SetParent(mark.transform, false);
            Primitive(PrimitiveType.Sphere, "Body", pod.transform, swarmGlow, Vector3.zero, Vector3.one * 0.55f);
            FlatQuad("Halo", pod.transform, sporeGlow, 0f, 1.6f);
            SetFields(mark.AddComponent<ImpactTelegraph>(), ("fill", fill.GetComponent<Renderer>()), ("outline", outline), ("closingRing", closing),
                ("projectile", pod.transform));
            var markPrefab = SavePrefab(mark, "FX");

            // Spore cloud: a translucent area with a crisp breathing edge, churning fog and spores drifting up. Authored at a
            // radius of 1; the cloud sizes itself.
            var cloud = new GameObject("FX_SporeCloud");
            var area = FlatQuad("Area", cloud.transform, sporeArea, 0.02f, 2f);
            var fogA = FlatQuad("FogA", cloud.transform, sporeFog, 0.05f, 2.3f);
            var fogB = FlatQuad("FogB", cloud.transform, sporeFog, 0.08f, 1.7f);
            var edge = FloorRing("Edge", cloud.transform, sporeLine, 0.1f, 0.06f);
            SetFields(cloud.AddComponent<SporeCloud>(),
                ("areas", new Object[] { area.GetComponent<Renderer>(), fogA.GetComponent<Renderer>(), fogB.GetComponent<Renderer>() }),
                ("fogLayers", new Object[] { fogA.transform, fogB.transform }), ("edge", edge), ("motes", SporeMotes(cloud.transform, sporeGlow)));
            var cloudPrefab = SavePrefab(cloud, "FX");

            // Spore burst: a sharp green ring snapping out from where a pod lands, and a puff of spores.
            var burst = new GameObject("FX_SporeBurst");
            var burstRing = FlatQuad("Ring", burst.transform, sporeBurstRing, 0.07f, 2f);
            Burst("Spores", sporeGlow, count: 24, speed: 5f, size: 0.35f, lifetime: 0.45f).transform.SetParent(burst.transform, false);
            SetFields(burst.AddComponent<RingPulse>(), ("ring", burstRing.GetComponent<Renderer>()), ("duration", 0.45f), ("startScale", 0.3f), ("endScale", 1.25f));
            var burstPrefab = SavePrefab(burst, "FX");

            // Bloom pulse: a constant-width green circle the boss expands.
            var pulse = new GameObject("FX_BloomPulse");
            var line = pulse.AddComponent<LineRenderer>();
            line.sharedMaterial = pulseLine;
            line.widthMultiplier = 0.8f;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 2;
            pulse.AddComponent<RingLine>();
            var pulsePrefab = SavePrefab(pulse, "FX").GetComponent<RingLine>();

            SetIfEmpty(broodling, ("Prefab", BuildBroodling(art, broodling, swarmGlow)));
            SetIfEmpty(boss, ("Prefab", BuildMatriarchBody(art, boss, broodling, swarmGlow, markPrefab, cloudPrefab, burstPrefab, pulsePrefab)));
            return new Matriarch { Boss = boss, Broodling = broodling, SporeMark = markPrefab, SporeCloud = cloudPrefab, SporeBurst = burstPrefab };
        }

        /// <summary>A circle drawn as a looping line on the floor, its points set by the component that owns it.</summary>
        static LineRenderer FloorRing(string name, Transform parent, Material material, float width, float height)
        {
            var line = Line(name, parent, material, width);
            line.transform.localPosition = new Vector3(0f, height, 0f);
            line.useWorldSpace = false;
            line.loop = true;
            return line;
        }

        /// <summary>Spores drifting up out of a cloud: soft green motes, spread over the floor circle, fading in and out.</summary>
        static ParticleSystem SporeMotes(Transform parent, Material material)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 2f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = particles.emission;
            emission.rateOverTime = 3f; // per unit of radius squared; the cloud scales it

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f); // lay the circle on the floor

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return particles;
        }

        static Enemy BuildBroodling(Art art, EnemyDefinition definition, Material swarmGlow)
        {
            // A small crimson grub with glowing green eyes and stubby legs.
            const float radius = 0.38f;
            var root = EnemyRoot("Broodling", art, radius, out var halo, out var model);
            Primitive(PrimitiveType.Sphere, "Body", model, art.EnemyHull, Vector3.zero, new Vector3(0.62f, 0.4f, 0.78f));
            var eyeL = Glow(Primitive(PrimitiveType.Sphere, "EyeL", model, swarmGlow, new Vector3(-0.13f, 0.1f, 0.3f), Vector3.one * 0.13f));
            var eyeR = Glow(Primitive(PrimitiveType.Sphere, "EyeR", model, swarmGlow, new Vector3(0.13f, 0.1f, 0.3f), Vector3.one * 0.13f));
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = i < 2 ? 0.15f : -0.15f;
                Primitive(PrimitiveType.Cube, "Leg" + i, model, art.EnemyHull, new Vector3(side * 0.34f, -0.12f, z), new Vector3(0.22f, 0.06f, 0.06f), Quaternion.Euler(0f, side * 20f, side * -25f));
            }
            root.AddComponent<BroodlingBrain>();
            return FinishEnemy(root, definition, radius, art, halo, eyeL, eyeR);
        }

        static Enemy BuildMatriarchBody(Art art, EnemyDefinition definition, EnemyDefinition broodling, Material swarmGlow,
            GameObject markPrefab, GameObject cloudPrefab, GameObject burstPrefab, RingLine pulsePrefab)
        {
            // A great crimson queen: a swollen abdomen behind a thorax and a small head, six tentacles splayed on the
            // floor, green glow sacs along her flanks, and a yellow heart on her back that opens when she can be hurt.
            const float radius = 2f;
            var root = EnemyRoot("HiveMatriarch", art, radius, out var halo, out var model);
            Primitive(PrimitiveType.Sphere, "Abdomen", model, art.EnemyHull, new Vector3(0f, 0f, -0.9f), new Vector3(3.2f, 1.5f, 3.8f));
            Primitive(PrimitiveType.Sphere, "Thorax", model, art.EnemyHull, new Vector3(0f, 0.15f, 1f), new Vector3(2.1f, 1.3f, 2f));
            Primitive(PrimitiveType.Sphere, "Head", model, art.EnemyHull, new Vector3(0f, 0.2f, 2.2f), new Vector3(1.3f, 0.9f, 1.2f));
            var glow = new List<Renderer>
            {
                Glow(Primitive(PrimitiveType.Sphere, "EyeL", model, swarmGlow, new Vector3(-0.3f, 0.35f, 2.7f), Vector3.one * 0.3f)),
                Glow(Primitive(PrimitiveType.Sphere, "EyeR", model, swarmGlow, new Vector3(0.3f, 0.35f, 2.7f), Vector3.one * 0.3f)),
            };
            for (int i = 0; i < 6; i++)
            {
                float heading = 50f + i * 52f;
                var rotation = Quaternion.Euler(0f, heading, 0f);
                Primitive(PrimitiveType.Capsule, "Tentacle" + i, model, art.EnemyHull, rotation * new Vector3(0f, -0.35f, 2.2f), new Vector3(0.35f, 1.3f, 0.35f), rotation * Quaternion.Euler(90f, 0f, 0f));
            }
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = i < 2 ? -0.3f : -1.6f;
                glow.Add(Glow(Primitive(PrimitiveType.Sphere, "Sac" + i, model, swarmGlow, new Vector3(side * 1.45f, 0.25f, z), new Vector3(0.5f, 0.4f, 0.6f))));
            }

            var heart = new GameObject("Heart");
            heart.transform.SetParent(model, false);
            var crystal = Primitive(PrimitiveType.Sphere, "Crystal", heart.transform, art.WeakPoint, new Vector3(0f, 0.95f, 0.6f), new Vector3(1f, 0.7f, 1f));
            SetFields(crystal.AddComponent<SimpleMotion>(), ("pulseAmount", 0.15f), ("pulseFrequency", 2.2f));
            FlatQuad("Ring", heart.transform, art.WeakPointRing, 1f, 2.8f).transform.localPosition = new Vector3(0f, 1f, 0.6f);

            var brain = root.AddComponent<MatriarchBrain>();
            SetFields(brain, ("heartMarker", heart), ("broodling", broodling), ("sporeMarkPrefab", markPrefab), ("sporeCloudPrefab", cloudPrefab), ("sporeBurstPrefab", burstPrefab),
                ("pulsePrefab", pulsePrefab), ("bulletPrefab", art.MatriarchShot), ("bulletImpactPrefab", art.BulletImpact));
            var lane = ChargeLane("ChargeLane", art, root.transform, size: 2f, spacing: 2.1f);
            SetFields(root.AddComponent<TelegraphLine>(), ("source", brain), ("lane", lane.GetComponent<ChevronLane>()));
            return FinishEnemyWithExclusions(root, definition, radius, art, new[] { halo, lane.GetComponentInChildren<Renderer>(true) }, glow.ToArray());
        }
    }
}
