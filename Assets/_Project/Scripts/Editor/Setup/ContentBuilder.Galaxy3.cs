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
        }

        static Matriarch BuildHiveMatriarch(Art art)
        {
            var dot = Load<Texture2D>(TexturesDir + "/T_SoftDot.png");
            var ring = Load<Texture2D>(TexturesDir + "/T_Ring.png");
            var beam = Load<Texture2D>(TexturesDir + "/T_Beam.png");
            var swarmGlow = Unlit("M_SwarmGlow", new Color(0.5f, 3.2f, 0.7f));
            var sporeFog = Blended("M_FX_SporeFog", new Color(0.45f, 1.7f, 0.35f, 0.55f), dot);
            var sporeGlow = Additive("M_FX_SporeGlow", new Color(0.45f, 2f, 0.35f), dot);
            var sporeRing = Additive("M_FX_SporeRing", new Color(0.7f, 3f, 0.5f), ring);
            var pulseLine = Additive("M_FX_BloomPulse", new Color(0.35f, 1.9f, 0.3f), beam);

            var broodling = Definition<EnemyDefinition>("Enemies/Enemy_Broodling", "enemy.broodling", "Broodling", so => Set(so,
                ("MaxHealth", 12f), ("MoveSpeed", 6.5f), ("ContactDamage", 8f), ("BudgetCost", 1), ("ScrapsDrop", 0), ("ScrapsDropChance", 0f)));
            var boss = Definition<EnemyDefinition>("Enemies/Enemy_HiveMatriarch", "boss.hive-matriarch", "The Hive Matriarch", so => Set(so,
                ("MaxHealth", 4800f), ("MoveSpeed", 4f), ("ContactDamage", 20f), ("BudgetCost", 30), ("ScrapsDrop", 40), ("ScrapsDropChance", 1f),
                ("KnockbackMultiplier", 0f)));

            // Spore pod landing mark: like the eruption telegraph, in green. Authored at a radius of 1.
            var mark = new GameObject("FX_SporeMark");
            var markGlow = FlatQuad("Glow", mark.transform, sporeGlow, 0.03f, 2.2f);
            FlatQuad("Outline", mark.transform, sporeRing, 0.04f, 2f / 0.88f);
            var closing = FlatQuad("ClosingRing", mark.transform, sporeRing, 0.05f, 2f / 0.88f);
            SetFields(mark.AddComponent<EruptTelegraph>(), ("closingRing", closing.transform), ("glow", markGlow.GetComponent<Renderer>()));
            var markPrefab = SavePrefab(mark, "FX");

            // Spore cloud: soft, slowly churning green fog. Authored at a radius of 1.
            var cloud = new GameObject("FX_SporeCloud");
            var fogA = FlatQuad("FogA", cloud.transform, sporeFog, 0.06f, 2.6f);
            SetFields(fogA.AddComponent<SimpleMotion>(), ("spinSpeed", 12f), ("pulseAmount", 0.06f), ("pulseFrequency", 0.7f));
            var fogB = FlatQuad("FogB", cloud.transform, sporeFog, 0.1f, 1.8f);
            SetFields(fogB.AddComponent<SimpleMotion>(), ("spinSpeed", -20f), ("pulseAmount", 0.1f), ("pulseFrequency", 1.1f));
            FlatQuad("Edge", cloud.transform, sporeRing, 0.04f, 2f / 0.88f);
            var cloudPrefab = SavePrefab(cloud, "FX");

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
            SetIfEmpty(boss, ("Prefab", BuildMatriarchBody(art, boss, broodling, swarmGlow, markPrefab, cloudPrefab, pulsePrefab)));
            return new Matriarch { Boss = boss, Broodling = broodling };
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
            GameObject markPrefab, GameObject cloudPrefab, RingLine pulsePrefab)
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
            SetFields(brain, ("heartMarker", heart), ("broodling", broodling), ("sporeMarkPrefab", markPrefab), ("sporeCloudPrefab", cloudPrefab),
                ("pulsePrefab", pulsePrefab), ("bulletPrefab", art.EnemyBulletVisual), ("bulletImpactPrefab", art.BulletImpact));
            var lane = ChargeLane("ChargeLane", art, root.transform, size: 2f, spacing: 2.1f);
            SetFields(root.AddComponent<TelegraphLine>(), ("source", brain), ("lane", lane.GetComponent<ChevronLane>()));
            return FinishEnemyWithExclusions(root, definition, radius, art, new[] { halo, lane.GetComponentInChildren<Renderer>(true) }, glow.ToArray());
        }
    }
}
