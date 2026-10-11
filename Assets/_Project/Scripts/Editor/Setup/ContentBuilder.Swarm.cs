using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Presentation;
using UnityEngine;

namespace Pathfinder.Editor.Setup
{
    public static partial class ContentBuilder
    {
        // M7: the Swarm's own creatures for the Veil Nebula (GDD §2, §7.3), joining the waves from galaxy 3. Crimson bodies
        // with the Swarm's toxic green glow, like the Hive Matriarch's brood.

        sealed class SwarmEnemies
        {
            public EnemyDefinition SporeDrifter, Stinger, Spitter;
        }

        static SwarmEnemies BuildSwarmEnemies(Art art, Matriarch matriarch, WaveDirectorConfig waves)
        {
            var swarmGlow = Unlit("M_SwarmGlow", new Color(0.5f, 3.2f, 0.7f));
            var swarm = new SwarmEnemies
            {
                SporeDrifter = Definition<EnemyDefinition>("Enemies/Enemy_SporeDrifter", "enemy.spore-drifter", "Spore Drifter", so => Set(so,
                    ("MaxHealth", 35f), ("MoveSpeed", 2.2f), ("ContactDamage", 10f), ("BudgetCost", 2), ("ScrapsDrop", 1), ("ScrapsDropChance", 0.7f))),
                Stinger = Definition<EnemyDefinition>("Enemies/Enemy_Stinger", "enemy.stinger", "Stinger", so => Set(so,
                    ("MaxHealth", 22f), ("MoveSpeed", 6.5f), ("ContactDamage", 9f), ("BudgetCost", 2), ("ScrapsDrop", 1), ("ScrapsDropChance", 0.7f))),
                Spitter = Definition<EnemyDefinition>("Enemies/Enemy_Spitter", "enemy.spitter", "Spitter", so => Set(so,
                    ("MaxHealth", 40f), ("MoveSpeed", 2.4f), ("ContactDamage", 8f), ("BudgetCost", 3), ("ScrapsDrop", 2), ("ScrapsDropChance", 0.8f))),
            };

            SetIfEmpty(swarm.SporeDrifter, ("Prefab", BuildSporeDrifter(art, swarm.SporeDrifter, swarmGlow, matriarch)));
            SetIfEmpty(swarm.Stinger, ("Prefab", BuildStinger(art, swarm.Stinger, swarmGlow)));
            SetIfEmpty(swarm.Spitter, ("Prefab", BuildSpitter(art, swarm.Spitter, swarmGlow, matriarch)));

            // From galaxy 3 on (0-based 2); Endless brings them in later, like galaxy 2's.
            AddWaveEntries(waves, new[] { (swarm.SporeDrifter, 2.5f, 1, 2), (swarm.Stinger, 2f, 1, 2), (swarm.Spitter, 2f, 2, 2) });
            return swarm;
        }

        static Enemy BuildSporeDrifter(Art art, EnemyDefinition definition, Material swarmGlow, Matriarch matriarch)
        {
            // A swollen crimson spore sac, breathing slowly, studded with glowing green pustules: it looks ready to burst.
            const float radius = 0.6f;
            var root = EnemyRoot("SporeDrifter", art, radius, out var halo, out var model);
            var sac = new GameObject("Sac").transform;
            sac.SetParent(model, false);
            SetFields(sac.gameObject.AddComponent<SimpleMotion>(), ("pulseAmount", 0.08f), ("pulseFrequency", 1.6f));
            Primitive(PrimitiveType.Sphere, "Body", sac, art.EnemyHull, Vector3.zero, new Vector3(1.15f, 0.9f, 1.15f));
            var glow = new Renderer[6];
            for (int i = 0; i < 5; i++)
            {
                var rotation = Quaternion.Euler(0f, i * 72f + 15f, 0f);
                glow[i] = Glow(Primitive(PrimitiveType.Sphere, "Pustule" + i, sac, swarmGlow, rotation * new Vector3(0f, 0.22f, 0.42f), Vector3.one * (i % 2 == 0 ? 0.24f : 0.18f)));
            }
            glow[5] = Glow(Primitive(PrimitiveType.Sphere, "Crown", sac, swarmGlow, new Vector3(0f, 0.44f, 0f), Vector3.one * 0.3f));

            SetFields(root.AddComponent<SporeDrifterBrain>(), ("cloudPrefab", matriarch.SporeCloud), ("burstPrefab", matriarch.SporeBurst));
            return FinishEnemy(root, definition, radius, art, halo, glow);
        }

        static Enemy BuildStinger(Art art, EnemyDefinition definition, Material swarmGlow)
        {
            // A slim crimson insect on buzzing wings, a glowing green sting out front. A chevron lane shows its dash.
            const float radius = 0.42f;
            var root = EnemyRoot("Stinger", art, radius, out var halo, out var model);
            Primitive(PrimitiveType.Capsule, "Body", model, art.EnemyHull, new Vector3(0f, 0f, -0.1f), new Vector3(0.34f, 0.45f, 0.34f), Quaternion.Euler(90f, 0f, 0f));
            Primitive(PrimitiveType.Sphere, "Head", model, art.EnemyHull, new Vector3(0f, 0.02f, 0.38f), Vector3.one * 0.34f);
            var sting = Glow(Primitive(PrimitiveType.Cube, "Sting", model, swarmGlow, new Vector3(0f, 0f, 0.7f), new Vector3(0.08f, 0.08f, 0.42f)));
            var eyeL = Glow(Primitive(PrimitiveType.Sphere, "EyeL", model, swarmGlow, new Vector3(-0.12f, 0.1f, 0.48f), Vector3.one * 0.1f));
            var eyeR = Glow(Primitive(PrimitiveType.Sphere, "EyeR", model, swarmGlow, new Vector3(0.12f, 0.1f, 0.48f), Vector3.one * 0.1f));
            foreach (float side in new[] { -1f, 1f })
            {
                var wing = new GameObject(side < 0f ? "WingL" : "WingR").transform;
                wing.SetParent(model, false);
                wing.localPosition = new Vector3(side * 0.18f, 0.18f, 0f);
                SetFields(wing.gameObject.AddComponent<SimpleMotion>(), ("pulseAmount", 0.15f), ("pulseFrequency", 24f)); // a buzz
                Primitive(PrimitiveType.Cube, "Membrane", wing, art.EnemyHull, new Vector3(side * 0.32f, 0f, -0.12f), new Vector3(0.62f, 0.02f, 0.26f), Quaternion.Euler(0f, side * -25f, 0f));
            }

            var brain = root.AddComponent<StingerBrain>();
            var lane = ChargeLane("TelegraphLane", art, root.transform);
            SetFields(root.AddComponent<TelegraphLine>(), ("source", brain), ("lane", lane.GetComponent<ChevronLane>()));
            return FinishEnemyWithExclusions(root, definition, radius, art, new[] { halo, lane.GetComponentInChildren<Renderer>(true) }, sting, eyeL, eyeR);
        }

        static Enemy BuildSpitter(Art art, EnemyDefinition definition, Material swarmGlow, Matriarch matriarch)
        {
            // A squat crimson pod rooted on short legs, a gaping mouth tipped up and forward, and a green sac on its back
            // that swells as it winds up.
            const float radius = 0.6f;
            var root = EnemyRoot("Spitter", art, radius, out var halo, out var model);
            Primitive(PrimitiveType.Sphere, "Body", model, art.EnemyHull, Vector3.zero, new Vector3(1.15f, 0.6f, 1.15f));
            Primitive(PrimitiveType.Cylinder, "Neck", model, art.EnemyHull, new Vector3(0f, 0.22f, 0.32f), new Vector3(0.42f, 0.22f, 0.42f), Quaternion.Euler(55f, 0f, 0f));
            var mouth = Glow(Primitive(PrimitiveType.Sphere, "Mouth", model, swarmGlow, new Vector3(0f, 0.36f, 0.52f), new Vector3(0.3f, 0.12f, 0.3f), Quaternion.Euler(55f, 0f, 0f)));
            var sacPivot = new GameObject("Sac").transform;
            sacPivot.SetParent(model, false);
            sacPivot.localPosition = new Vector3(0f, 0.3f, -0.25f);
            SetFields(sacPivot.gameObject.AddComponent<SimpleMotion>(), ("pulseAmount", 0.1f), ("pulseFrequency", 2.4f));
            var sac = Glow(Primitive(PrimitiveType.Sphere, "Body", sacPivot, swarmGlow, Vector3.zero, new Vector3(0.55f, 0.45f, 0.55f)));
            for (int i = 0; i < 3; i++)
            {
                var rotation = Quaternion.Euler(0f, 60f + i * 120f, 0f);
                Primitive(PrimitiveType.Capsule, "Leg" + i, model, art.EnemyHull, rotation * new Vector3(0f, -0.2f, 0.52f), new Vector3(0.14f, 0.22f, 0.14f), rotation * Quaternion.Euler(60f, 0f, 0f));
            }

            SetFields(root.AddComponent<SpitterBrain>(), ("markPrefab", matriarch.SporeMark), ("burstPrefab", matriarch.SporeBurst),
                ("bulletPrefab", art.SpitterShot), ("bulletImpactPrefab", art.BulletImpact));
            return FinishEnemy(root, definition, radius, art, halo, mouth, sac);
        }
    }
}
