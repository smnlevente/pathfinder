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
        // Galaxy 2 content: the Scorcher, the Arc Drone and the Crusher, and the Binary Wardens (GDD §7.3, §7.5).

        /// <summary>Flames, floor telegraphs, lightning and the Wardens' effects.</summary>
        static void BuildHazardArt(Art art)
        {
            var dot = Load<Texture2D>(TexturesDir + "/T_SoftDot.png");
            // Across a line: bright in the middle, soft at the edges, constant along its length.
            var beam = CreateTexture("T_Beam", 64, (x, y) =>
            {
                float v = (y + 0.5f) / 64f;
                return new Color(1f, 1f, 1f, Mathf.Exp(-Mathf.Pow((v - 0.5f) / 0.2f, 2f)));
            }, TextureWrapMode.Clamp, alphaIsTransparency: true);

            // Flames are alpha-blended, so a dense stream stays a readable orange cone instead of blowing out (playtest 2).
            art.Flame = Blended("M_FX_Flame", new Color(2.4f, 0.75f, 0.12f, 0.75f), dot);
            art.FlameCore = Blended("M_FX_FlameCore", new Color(3f, 2.2f, 0.9f, 0.85f), dot);
            art.HazardArea = Additive("M_FX_HazardArea", new Color(1.6f, 0.5f, 0.1f), dot);
            art.SlamArea = Additive("M_FX_SlamArea", new Color(2f, 0.2f, 0.1f), dot);
            art.Beam = Additive("M_FX_Beam", new Color(2.6f, 0.45f, 0.15f), beam);
            art.BoltCore = Additive("M_FX_BoltCore", new Color(3f, 3f, 3.2f), beam);
            art.BoltGlow = Additive("M_FX_BoltGlow", new Color(2.4f, 0.6f, 0.2f), beam);
            art.Tether = Additive("M_FX_Tether", new Color(3f, 0.7f, 0.2f), beam);

            var flame = new GameObject("PV_Flame");
            var body = FlatQuad("Body", flame.transform, art.Flame, 0f, 1f);
            SetFields(body.AddComponent<SimpleMotion>(), ("pulseAmount", 0.2f), ("pulseFrequency", 9f));
            FlatQuad("Core", flame.transform, art.FlameCore, 0.02f, 0.45f);
            art.FlameVisual = SavePrefab(flame, "Projectiles");

            var bolt = new GameObject("FX_ArcBolt");
            var glow = Line("Glow", bolt.transform, art.BoltGlow, 0.5f);
            var core = Line("Core", bolt.transform, art.BoltCore, 0.13f);
            SetFields(bolt.AddComponent<ArcBolt>(), ("core", core), ("glow", glow));
            art.ArcBolt = SavePrefab(bolt, "FX");

            // The slam's shockwave ring reaches the Crusher's 3.3 u slam radius (the ring band sits at 44% of the quad).
            art.SlamShockwave = SavePrefab(Explosion("FX_SlamShockwave", art.FxGrey, art.FxOrange, art.ShockRing, count: 30, speed: 6f, size: 0.5f, lifetime: 0.5f, ringSize: 7.5f), "FX");

            // Arriving at a level: a cyan flash and ring where the ship comes out of the wormhole (playtest 5).
            art.WarpIn = SavePrefab(Explosion("FX_WarpIn", art.FxCyan, art.FxCyan, art.NovaRing, count: 24, speed: 5f, size: 0.4f, lifetime: 0.45f, ringSize: 6f), "FX");
            art.CoreOffline = SavePrefab(Explosion("FX_CoreOffline", art.FxOrange, art.FxGrey, art.ShockRing, count: 26, speed: 6f, size: 0.35f, lifetime: 0.6f, ringSize: 4f), "FX");
            art.CoreReboot = SavePrefab(Explosion("FX_CoreReboot", art.FxRed, art.FxOrange, art.ShockRing, count: 20, speed: 5f, size: 0.3f, lifetime: 0.4f, ringSize: 5.5f), "FX");
        }

        static LineRenderer Line(string name, Transform parent, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        /// <summary>A floor telegraph for an IAreaTelegraph brain. Its renderers start disabled, so the hit flash skips them.</summary>
        static void AreaTelegraphChild(Transform parent, MonoBehaviour source, Material material)
        {
            var go = new GameObject("AreaTelegraph");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var outline = go.AddComponent<MeshFilter>();
            DisabledRenderer(go, material);
            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(go.transform, false);
            var fill = fillObject.AddComponent<MeshFilter>();
            DisabledRenderer(fillObject, material);
            SetFields(go.AddComponent<AreaTelegraph>(), ("source", source), ("outline", outline), ("fill", fill));
        }

        static void DisabledRenderer(GameObject go, Material material)
        {
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
        }

        // ---------------------------------------------------------------- enemies

        static Enemy BuildScorcher(Art art, Data data)
        {
            // A squat tank with a flamethrower nozzle and a fuel drum on its back. Nozzle and drum band flare as it winds up.
            var root = EnemyRoot("Scorcher", art, radius: 0.6f, out var halo, out var model);
            Primitive(PrimitiveType.Cube, "Body", model, art.EnemyHull, Vector3.zero, new Vector3(0.95f, 0.35f, 0.95f));
            Primitive(PrimitiveType.Cylinder, "Drum", model, art.EnemyHull, new Vector3(0f, 0.28f, -0.32f), new Vector3(0.5f, 0.3f, 0.5f), Quaternion.Euler(0f, 0f, 90f));
            var band = Glow(Primitive(PrimitiveType.Cube, "DrumBand", model, art.EnemyGlow, new Vector3(0f, 0.28f, -0.32f), new Vector3(0.66f, 0.1f, 0.12f)));
            Primitive(PrimitiveType.Cube, "Nozzle", model, art.EnemyHull, new Vector3(0f, 0.08f, 0.6f), new Vector3(0.22f, 0.22f, 0.55f));
            var tip = Glow(Primitive(PrimitiveType.Cube, "NozzleTip", model, art.EnemyGlow, new Vector3(0f, 0.08f, 0.9f), new Vector3(0.3f, 0.3f, 0.08f)));

            var brain = root.AddComponent<ScorcherBrain>();
            SetFields(brain, ("flamePrefab", art.FlameVisual));
            AreaTelegraphChild(root.transform, brain, art.HazardArea);
            return FinishEnemy(root, data.Scorcher, radius: 0.6f, art, halo, band, tip);
        }

        static Enemy BuildArcDrone(Art art, Data data)
        {
            // A small diamond with two coil prongs. The coils and its core spark while it charges.
            var root = EnemyRoot("ArcDrone", art, radius: 0.45f, out var halo, out var model);
            Primitive(PrimitiveType.Cube, "Body", model, art.EnemyHull, Vector3.zero, new Vector3(0.55f, 0.22f, 0.55f), Quaternion.Euler(0f, 45f, 0f));
            Primitive(PrimitiveType.Cube, "ProngL", model, art.EnemyHull, new Vector3(-0.2f, 0f, 0.42f), new Vector3(0.08f, 0.1f, 0.5f));
            Primitive(PrimitiveType.Cube, "ProngR", model, art.EnemyHull, new Vector3(0.2f, 0f, 0.42f), new Vector3(0.08f, 0.1f, 0.5f));
            var coilL = Glow(Primitive(PrimitiveType.Sphere, "CoilL", model, art.EnemyGlow, new Vector3(-0.2f, 0f, 0.7f), Vector3.one * 0.17f));
            var coilR = Glow(Primitive(PrimitiveType.Sphere, "CoilR", model, art.EnemyGlow, new Vector3(0.2f, 0f, 0.7f), Vector3.one * 0.17f));
            var core = Glow(Primitive(PrimitiveType.Sphere, "Core", model, art.EnemyGlow, new Vector3(0f, 0.14f, 0f), Vector3.one * 0.22f));

            var brain = root.AddComponent<ArcDroneBrain>();
            SetFields(brain, ("boltPrefab", art.ArcBolt.GetComponent<ArcBolt>()));
            var line = Line("BeamTelegraph", root.transform, art.Beam, 0.15f);
            SetFields(line.gameObject.AddComponent<BeamTelegraph>(), ("source", brain), ("line", line));
            return FinishEnemy(root, data.ArcDrone, radius: 0.45f, art, halo, coilL, coilR, core);
        }

        static Enemy BuildCrusher(Art art, Data data)
        {
            // A heavy armoured block on treads with a hammer plate on top. Its vents blaze as it rears up to slam.
            var root = EnemyRoot("Crusher", art, radius: 0.85f, out var halo, out var model);
            Primitive(PrimitiveType.Cube, "Body", model, art.EnemyHull, Vector3.zero, new Vector3(1.4f, 0.6f, 1.4f));
            Primitive(PrimitiveType.Cube, "Hammer", model, art.EnemyHull, new Vector3(0f, 0.45f, 0f), new Vector3(1.05f, 0.3f, 1.05f), Quaternion.Euler(0f, 45f, 0f));
            Primitive(PrimitiveType.Cube, "TreadL", model, art.EnemyHull, new Vector3(-0.8f, -0.15f, 0f), new Vector3(0.25f, 0.35f, 1.6f));
            Primitive(PrimitiveType.Cube, "TreadR", model, art.EnemyHull, new Vector3(0.8f, -0.15f, 0f), new Vector3(0.25f, 0.35f, 1.6f));
            var vents = new Renderer[4];
            for (int i = 0; i < vents.Length; i++)
            {
                var rotation = Quaternion.Euler(0f, i * 90f, 0f);
                vents[i] = Glow(Primitive(PrimitiveType.Cube, "Vent" + i, model, art.EnemyGlow, rotation * new Vector3(0f, 0.08f, 0.71f), new Vector3(0.85f, 0.12f, 0.05f), rotation));
            }

            var brain = root.AddComponent<CrusherBrain>();
            SetFields(brain, ("slamEffect", art.SlamShockwave));
            AreaTelegraphChild(root.transform, brain, art.SlamArea);
            return FinishEnemy(root, data.Crusher, radius: 0.85f, art, halo, vents);
        }

        // ---------------------------------------------------------------- the Binary Wardens

        sealed class Wardens
        {
            public EnemyDefinition Alpha, Beta;
        }

        static Wardens BuildBinaryWardens(Art art)
        {
            var beta = Definition<EnemyDefinition>("Enemies/Enemy_WardenBeta", "boss.binary-wardens.beta", "Warden Beta", so => Set(so,
                ("MaxHealth", 2200f), ("MoveSpeed", 5f), ("ContactDamage", 15f), ("BudgetCost", 20), ("ScrapsDrop", 0), ("ScrapsDropChance", 0f),
                ("KnockbackMultiplier", 0f)));
            var alpha = Definition<EnemyDefinition>("Enemies/Enemy_BinaryWardens", "boss.binary-wardens", "The Binary Wardens", so => Set(so,
                ("MaxHealth", 2200f), ("MoveSpeed", 5f), ("ContactDamage", 15f), ("BudgetCost", 20), ("ScrapsDrop", 30), ("ScrapsDropChance", 1f),
                ("KnockbackMultiplier", 0f)));

            SetIfEmpty(beta, ("Prefab", WardenCore("WardenBeta", art, beta, partner: null, isAlpha: false)));
            SetIfEmpty(alpha, ("Prefab", WardenCore("WardenAlpha", art, alpha, partner: beta, isAlpha: true)));
            return new Wardens { Alpha = alpha, Beta = beta };
        }

        static Enemy WardenCore(string name, Art art, EnemyDefinition definition, EnemyDefinition partner, bool isAlpha)
        {
            // An armoured AI core with a glowing eye, ringed by spinning emitters: the Alpha a ring of six, the Beta a
            // cross of blades, so the two read apart at a glance.
            const float radius = 1.1f;
            var root = EnemyRoot(name, art, radius, out var halo, out var model);
            Primitive(PrimitiveType.Sphere, "Core", model, art.EnemyHull, Vector3.zero, new Vector3(1.6f, 1.2f, 1.6f));
            Primitive(PrimitiveType.Cylinder, "Collar", model, art.EnemyHull, new Vector3(0f, -0.25f, 0f), new Vector3(2f, 0.1f, 2f));
            var glow = new List<Renderer>
            {
                Glow(Primitive(PrimitiveType.Sphere, "Eye", model, art.EnemyGlow, new Vector3(0f, 0.45f, 0.35f), new Vector3(0.6f, 0.45f, 0.6f))),
            };

            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(model, false);
            if (isAlpha)
            {
                for (int i = 0; i < 6; i++)
                {
                    var rotation = Quaternion.Euler(0f, i * 60f, 0f);
                    glow.Add(Glow(Primitive(PrimitiveType.Cube, "Emitter" + i, spinner, art.EnemyGlow, rotation * new Vector3(0f, 0f, 1.3f), new Vector3(0.32f, 0.18f, 0.32f), rotation * Quaternion.Euler(0f, 45f, 0f))));
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    var rotation = Quaternion.Euler(0f, i * 90f + 45f, 0f);
                    Primitive(PrimitiveType.Cube, "Blade" + i, spinner, art.EnemyHull, rotation * new Vector3(0f, 0f, 1.05f), new Vector3(0.22f, 0.12f, 0.9f), rotation);
                    glow.Add(Glow(Primitive(PrimitiveType.Cube, "BladeTip" + i, spinner, art.EnemyGlow, rotation * new Vector3(0f, 0f, 1.55f), new Vector3(0.28f, 0.16f, 0.14f), rotation)));
                }
            }

            var brain = root.AddComponent<WardenBrain>();
            SetFields(brain, ("partnerDefinition", partner), ("bulletPrefab", art.EnemyBulletVisual), ("bulletImpactPrefab", art.BulletImpact),
                ("offlineEffect", art.CoreOffline), ("rebootEffect", art.CoreReboot));
            SetFields(root.AddComponent<SinkVisual>(), ("source", brain), ("model", model));
            AreaTelegraphChild(root.transform, brain, art.FxGrey); // the reboot ring: grey, filling as the core comes back

            var visual = root.AddComponent<WardenVisual>();
            var powered = glow.ConvertAll(r => (Object)r.gameObject).ToArray();
            SetFields(visual, ("brain", brain), ("poweredParts", powered), ("spinner", spinner));
            if (isAlpha)
                SetFields(visual, ("tether", Line("Tether", root.transform, art.Tether, 0.4f)));

            return FinishEnemy(root, definition, radius, art, halo, glow.ToArray());
        }

        /// <summary>
        /// Each galaxy gets its guardian: the Quarry Maw, the Binary Wardens, the Hive Matriarch. Configs from before the
        /// Matriarch (galaxy 3 reusing the Maw) are upgraded once; any other choice is left alone.
        /// </summary>
        static void AssignGalaxyBosses(RunConfig config, EnemyDefinition quarryMaw, EnemyDefinition wardens, EnemyDefinition matriarch)
        {
            var so = new SerializedObject(config);
            var bosses = so.FindProperty("<GalaxyBosses>k__BackingField");
            if (bosses.arraySize == 0)
                Assign(bosses, new Object[] { quarryMaw, wardens, matriarch }, "GalaxyBosses");
            else if (bosses.arraySize == 3 && bosses.GetArrayElementAtIndex(2).objectReferenceValue == quarryMaw)
                bosses.GetArrayElementAtIndex(2).objectReferenceValue = matriarch;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Wave configs made before galaxy 2's enemies get them added once, from galaxy 2 on.</summary>
        static void AddGalaxyTwoEnemies(Data data)
        {
            var so = new SerializedObject(data.Waves);
            var entries = so.FindProperty("<Enemies>k__BackingField");
            foreach (var (enemy, weight, minWave, minGalaxy) in GalaxyTwoPool(data))
            {
                bool present = false;
                for (int i = 0; i < entries.arraySize; i++)
                    present |= entries.GetArrayElementAtIndex(i).FindPropertyRelative("Enemy").objectReferenceValue == enemy;
                if (present)
                    continue;

                entries.arraySize++;
                var element = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                element.FindPropertyRelative("Enemy").objectReferenceValue = enemy;
                element.FindPropertyRelative("Weight").floatValue = weight;
                element.FindPropertyRelative("MinWave").intValue = minWave;
                element.FindPropertyRelative("MinGalaxy").intValue = minGalaxy;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static (EnemyDefinition enemy, float weight, int minWave, int minGalaxy)[] GalaxyTwoPool(Data data) => new[]
        {
            (data.Scorcher, 2f, 1, 1), (data.ArcDrone, 3f, 1, 1), (data.Crusher, 1.5f, 2, 1),
        };
    }
}
