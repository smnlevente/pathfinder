using System;
using System.IO;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Enemies;
using Pathfinder.Game.Player;
using Pathfinder.Game.Presentation;
using Pathfinder.Game.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    /// <summary>
    /// Generates the M1 combat sandbox: placeholder materials, textures, prefabs and effects, the content
    /// definitions, and the wiring of the Run scene.
    /// Rerunning rebuilds the placeholder art and prefabs (asset GUIDs are kept) but leaves the content
    /// definitions alone, so designer tuning survives.
    /// Batch mode: <c>Unity.exe -batchmode -projectPath . -executeMethod Pathfinder.Editor.Setup.M1ContentBuilder.BuildBatch</c>
    /// </summary>
    public static class M1ContentBuilder
    {
        const string Root = "Assets/_Project";
        const string MaterialsDir = Root + "/Art/Materials";
        const string TexturesDir = Root + "/Art/Textures";
        const string PrefabsDir = Root + "/Prefabs";
        const string DataDir = Root + "/Data";
        const string RunScenePath = Root + "/Scenes/Run.unity";

        const string UnlitShader = "Universal Render Pipeline/Unlit";
        const string LitShader = "Universal Render Pipeline/Lit";
        const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

        static readonly Vector2 ArenaSize = new Vector2(60f, 40f);

        sealed class Materials
        {
            public Material Floor, ShipHull, ShipGlow, EnemyHull, EnemyGlow, EnemyHalo;
            public Material PlayerBolt, EnemyBulletCore, EnemyBulletHalo, Wall, Telegraph, FxOrange, FxCyan, FxMagenta, FxRed;
        }

        sealed class Prefabs
        {
            public GameObject PlayerBolt, EnemyBullet, BoltImpact, BulletImpact, EnemyExplosion, PlayerExplosion, SpawnTelegraph;
            public Enemy Drone, Gunner;
            public PlayerShip Player;
        }

        sealed class Definitions
        {
            public ShipDefinition Ship;
            public PrimaryWeaponDefinition PhotonRepeater;
            public EnemyDefinition Drone, Gunner;
        }

        [MenuItem("Pathfinder/Setup/Build M1 Content")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Build();
            EditorSceneManager.OpenScene(RunScenePath);
        }

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            foreach (var dir in new[] { MaterialsDir, TexturesDir, PrefabsDir, DataDir })
                EnsureFolder(dir);

            DeleteObsoleteAssets();
            var materials = BuildMaterials();
            var prefabs = new Prefabs();
            BuildEffects(materials, prefabs);
            BuildProjectiles(materials, prefabs);

            var definitions = BuildDefinitions();
            BuildEnemies(materials, prefabs, definitions);
            BuildPlayer(materials, prefabs, definitions);
            LinkDefinitions(prefabs, definitions);

            TunePostProcessing();
            PopulateRunScene(materials, prefabs, definitions);
            AssetDatabase.SaveAssets();
            Debug.Log("[M1ContentBuilder] Combat sandbox built.");
        }

        /// <summary>Generated assets from earlier builder versions that nothing uses anymore.</summary>
        static readonly string[] ObsoleteAssets =
        {
            MaterialsDir + "/M_DroneHull.mat",
            MaterialsDir + "/M_DroneGlow.mat",
            MaterialsDir + "/M_GunnerHull.mat",
            MaterialsDir + "/M_GunnerGlow.mat",
            MaterialsDir + "/M_EnemyBullet.mat",
        };

        static void DeleteObsoleteAssets()
        {
            foreach (string path in ObsoleteAssets)
            {
                if (File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        // ---------------------------------------------------------------- materials & textures

        static Materials BuildMaterials()
        {
            var grid = CreateTexture("T_Grid", 128, (x, y) =>
            {
                bool line = x < 3 || y < 3;
                return line ? new Color(0.16f, 0.36f, 0.46f) : new Color(0.05f, 0.06f, 0.09f);
            }, TextureWrapMode.Repeat, alphaIsTransparency: false);

            var dot = CreateTexture("T_SoftDot", 64, (x, y) =>
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1f, 1f, 1f, alpha * alpha);
            }, TextureWrapMode.Clamp, alphaIsTransparency: true);

            var floor = Material("M_ArenaFloor", LitShader, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", grid);
                m.SetTextureScale("_BaseMap", ArenaSize / 2f);   // one grid cell = 2 × 2 units
                m.SetFloat("_Smoothness", 0.55f);
                m.SetTexture("_EmissionMap", grid);
                m.SetColor("_EmissionColor", new Color(0.35f, 0.75f, 1f) * 0.6f);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            });

            return new Materials
            {
                Floor = floor,
                ShipHull = Lit("M_ShipHull", new Color(0.62f, 0.68f, 0.78f), smoothness: 0.6f, metallic: 0.35f),
                ShipGlow = Unlit("M_ShipGlow", new Color(0.3f, 1.6f, 2.6f)),
                // Visual language (GDD §12.4): enemies are solid crimson bodies with red-orange accent strips
                // and a red floor glow. They never look like glowing orbs, which is what enemy bullets look like.
                EnemyHull = Lit("M_EnemyHull", new Color(0.72f, 0.13f, 0.1f), smoothness: 0.5f, metallic: 0.35f, emission: new Color(0.22f, 0.03f, 0.02f)),
                EnemyGlow = Unlit("M_EnemyGlow", new Color(3.4f, 0.6f, 0.12f)),
                PlayerBolt = Unlit("M_PlayerBolt", new Color(0.5f, 2.2f, 3.2f)),
                // Enemy bullets are the only magenta in the game: a white-hot core inside a pulsing magenta halo.
                EnemyBulletCore = Unlit("M_EnemyBulletCore", new Color(6f, 4f, 6f)),
                Wall = Unlit("M_ArenaWall", new Color(0.3f, 1.3f, 2.2f)),
                Telegraph = Unlit("M_SpawnTelegraph", new Color(2.6f, 0.25f, 0.2f)),
                FxOrange = Particle("M_FX_Orange", new Color(4f, 1.4f, 0.3f), dot),
                FxCyan = Particle("M_FX_Cyan", new Color(0.6f, 2.4f, 4f), dot),
                FxMagenta = Particle("M_FX_Magenta", new Color(4f, 0.6f, 3f), dot),
                FxRed = Particle("M_FX_Red", new Color(3f, 0.25f, 0.15f), dot),
                EnemyHalo = Particle("M_FX_EnemyHalo", new Color(1.1f, 0.08f, 0.05f), dot),
                EnemyBulletHalo = Particle("M_FX_EnemyBulletHalo", new Color(6f, 0.4f, 4.5f), dot),
            };
        }

        static Material Lit(string name, Color color, float smoothness, float metallic, Color? emission = null) => Material(name, LitShader, m =>
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                // A faint self-glow keeps silhouettes readable against the dark floor.
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
        });

        static Material Unlit(string name, Color hdrColor) => Material(name, UnlitShader, m => m.SetColor("_BaseColor", hdrColor));

        static Material Particle(string name, Color hdrColor, Texture2D texture) => Material(name, ParticleShader, m =>
        {
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", hdrColor);
            m.SetFloat("_Surface", (float)BaseShaderGUI.SurfaceType.Transparent);
            m.SetFloat("_Blend", (float)BaseShaderGUI.BlendMode.Additive);
            BaseShaderGUI.SetMaterialKeywords(m);
        });

        /// <summary>Creates the material or updates the existing one in place, which keeps its GUID and references.</summary>
        static Material Material(string name, string shaderName, Action<Material> configure)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Shader '{shaderName}' not found.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D CreateTexture(string name, int size, Func<int, int, Color> pixel, TextureWrapMode wrap, bool alphaIsTransparency)
        {
            string path = $"{TexturesDir}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    texture.SetPixel(x, y, pixel(x, y));
            }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------- effects & projectiles

        static void BuildEffects(Materials materials, Prefabs prefabs)
        {
            prefabs.BoltImpact = SavePrefab(Burst("FX_BoltImpact", materials.FxCyan, count: 6, speed: 4f, size: 0.18f, lifetime: 0.15f), "FX");
            prefabs.BulletImpact = SavePrefab(Burst("FX_BulletImpact", materials.FxMagenta, count: 10, speed: 5f, size: 0.25f, lifetime: 0.2f), "FX");
            prefabs.EnemyExplosion = SavePrefab(Burst("FX_EnemyExplosion", materials.FxOrange, count: 28, speed: 7f, size: 0.4f, lifetime: 0.45f), "FX");
            prefabs.PlayerExplosion = SavePrefab(Burst("FX_PlayerExplosion", materials.FxCyan, count: 70, speed: 9f, size: 0.55f, lifetime: 0.9f), "FX");

            var telegraph = new GameObject("FX_SpawnTelegraph");
            // A soft additive glow on the floor reads as "something is arriving here", not as a solid object.
            var glow = Primitive(PrimitiveType.Quad, "Glow", telegraph.transform, materials.FxRed, new Vector3(0f, 0.03f, 0f), new Vector3(2.4f, 2.4f, 1f), Quaternion.Euler(90f, 0f, 0f));
            SetFields(glow.AddComponent<SimpleMotion>(), ("pulseAmount", 0.2f), ("pulseFrequency", 3f));
            var core = Primitive(PrimitiveType.Cylinder, "Core", telegraph.transform, materials.Telegraph, new Vector3(0f, 0.02f, 0f), new Vector3(0.35f, 0.01f, 0.35f));
            SetFields(core.AddComponent<SimpleMotion>(), ("pulseAmount", 0.4f), ("pulseFrequency", 3f));
            prefabs.SpawnTelegraph = SavePrefab(telegraph, "FX");
        }

        static GameObject Burst(string name, Material material, int count, float speed, float size, float lifetime)
        {
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = lifetime;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return go;
        }

        static void BuildProjectiles(Materials materials, Prefabs prefabs)
        {
            var bolt = new GameObject("PV_PlayerBolt");
            Primitive(PrimitiveType.Capsule, "Mesh", bolt.transform, materials.PlayerBolt, Vector3.zero, new Vector3(0.14f, 0.35f, 0.14f), Quaternion.Euler(90f, 0f, 0f));
            prefabs.PlayerBolt = SavePrefab(bolt, "Projectiles");

            var bullet = new GameObject("PV_EnemyBullet");
            Primitive(PrimitiveType.Sphere, "Core", bullet.transform, materials.EnemyBulletCore, Vector3.zero, Vector3.one * 0.32f);
            var halo = Primitive(PrimitiveType.Quad, "Halo", bullet.transform, materials.EnemyBulletHalo, Vector3.zero, new Vector3(1.3f, 1.3f, 1f), Quaternion.Euler(90f, 0f, 0f));
            SetFields(halo.AddComponent<SimpleMotion>(), ("pulseAmount", 0.18f), ("pulseFrequency", 7f));
            prefabs.EnemyBullet = SavePrefab(bullet, "Projectiles");
        }

        // ---------------------------------------------------------------- data

        static Definitions BuildDefinitions()
        {
            // Values from GDD §5–§7. Created only when missing, so tuning in the inspector survives rebuilds.
            return new Definitions
            {
                Ship = Definition<ShipDefinition>("Ships/Ship_Stormbringer", "ship.stormbringer", "Stormbringer Battleship"),
                PhotonRepeater = Definition<PrimaryWeaponDefinition>("Weapons/Primary_PhotonRepeater", "primary.photon-repeater", "Photon Repeater"),
                Drone = Definition<EnemyDefinition>("Enemies/Enemy_Drone", "enemy.drone", "Drone", so =>
                {
                    SetAuto(so, "MaxHealth", 15f);
                    SetAuto(so, "MoveSpeed", 5.5f);
                    SetAuto(so, "ContactDamage", 10f);
                    SetAuto(so, "BudgetCost", 1);
                }),
                Gunner = Definition<EnemyDefinition>("Enemies/Enemy_Gunner", "enemy.gunner", "Gunner", so =>
                {
                    SetAuto(so, "MaxHealth", 35f);
                    SetAuto(so, "MoveSpeed", 4f);
                    SetAuto(so, "ContactDamage", 10f);
                    SetAuto(so, "BudgetCost", 2);
                }),
            };
        }

        static T Definition<T>(string relativePath, string id, string displayName, Action<SerializedObject> configure = null) where T : ContentDefinition
        {
            string path = $"{DataDir}/{relativePath}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            configure?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /// <summary>Fills in prefab references that are still empty. A designer's own choices are never overwritten.</summary>
        static void LinkDefinitions(Prefabs prefabs, Definitions definitions)
        {
            SetIfEmpty(definitions.PhotonRepeater, "ProjectilePrefab", prefabs.PlayerBolt);
            SetIfEmpty(definitions.PhotonRepeater, "ImpactPrefab", prefabs.BoltImpact);
            SetIfEmpty(definitions.Drone, "Prefab", prefabs.Drone);
            SetIfEmpty(definitions.Gunner, "Prefab", prefabs.Gunner);
        }

        // ---------------------------------------------------------------- actors

        static void BuildEnemies(Materials materials, Prefabs prefabs, Definitions definitions)
        {
            // Drone: a spinning crimson blade with glowing tips, sitting on a red floor glow.
            var drone = new GameObject("Drone");
            var droneHalo = Halo(drone.transform, materials.EnemyHalo, 0.45f);
            var droneModel = Model(drone.transform);
            SetFields(droneModel.gameObject.AddComponent<SimpleMotion>(), ("spinSpeed", 300f));
            Primitive(PrimitiveType.Cube, "Blade", droneModel, materials.EnemyHull, Vector3.zero, new Vector3(1f, 0.08f, 1f), Quaternion.Euler(0f, 45f, 0f));
            Primitive(PrimitiveType.Cylinder, "Hub", droneModel, materials.EnemyHull, new Vector3(0f, 0.06f, 0f), new Vector3(0.45f, 0.08f, 0.45f));
            var droneTips = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 tip = Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0f, 0.66f);
                droneTips[i] = Primitive(PrimitiveType.Cube, "Tip" + i, droneModel, materials.EnemyGlow, tip, new Vector3(0.12f, 0.1f, 0.12f), Quaternion.Euler(0f, 45f, 0f)).GetComponent<Renderer>();
            }

            drone.AddComponent<DroneBrain>();
            var droneEnemy = drone.AddComponent<Enemy>();
            SetFields(droneEnemy, ("radius", 0.45f), ("definition", definitions.Drone));
            var droneVisual = AddVisual(drone, droneEnemy, prefabs.EnemyExplosion, droneTips);
            SetFields(droneVisual, ("flashExclusions", new Object[] { droneHalo }));
            prefabs.Drone = SavePrefab(drone, "Enemies").GetComponent<Enemy>();

            // Gunner: a squat crimson turret. Its eye slit and muzzle flare up as it winds up a burst.
            var gunner = new GameObject("Gunner");
            var gunnerHalo = Halo(gunner.transform, materials.EnemyHalo, 0.6f);
            var gunnerModel = Model(gunner.transform);
            Primitive(PrimitiveType.Cylinder, "Body", gunnerModel, materials.EnemyHull, Vector3.zero, new Vector3(1.1f, 0.22f, 1.1f));
            Primitive(PrimitiveType.Cube, "Turret", gunnerModel, materials.EnemyHull, new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.24f, 0.6f));
            var eye = Primitive(PrimitiveType.Cube, "Eye", gunnerModel, materials.EnemyGlow, new Vector3(0f, 0.25f, 0.3f), new Vector3(0.42f, 0.07f, 0.06f));
            Primitive(PrimitiveType.Cube, "Barrel", gunnerModel, materials.EnemyHull, new Vector3(0f, 0.15f, 0.6f), new Vector3(0.18f, 0.18f, 0.7f));
            var muzzle = Primitive(PrimitiveType.Cube, "Muzzle", gunnerModel, materials.EnemyGlow, new Vector3(0f, 0.15f, 0.97f), new Vector3(0.22f, 0.22f, 0.08f));

            var gunnerBrain = gunner.AddComponent<GunnerBrain>();
            SetFields(gunnerBrain, ("bulletPrefab", prefabs.EnemyBullet), ("bulletImpactPrefab", prefabs.BulletImpact));
            var gunnerEnemy = gunner.AddComponent<Enemy>();
            SetFields(gunnerEnemy, ("radius", 0.6f), ("definition", definitions.Gunner));
            var gunnerVisual = AddVisual(gunner, gunnerEnemy, prefabs.EnemyExplosion, eye.GetComponent<Renderer>(), muzzle.GetComponent<Renderer>());
            SetFields(gunnerVisual, ("flashExclusions", new Object[] { gunnerHalo }));
            prefabs.Gunner = SavePrefab(gunner, "Enemies").GetComponent<Enemy>();
        }

        static void BuildPlayer(Materials materials, Prefabs prefabs, Definitions definitions)
        {
            var root = new GameObject("PlayerShip");
            var model = Model(root.transform);
            Primitive(PrimitiveType.Cube, "Hull", model, materials.ShipHull, Vector3.zero, new Vector3(0.55f, 0.22f, 1.2f));
            Primitive(PrimitiveType.Cube, "Nose", model, materials.ShipHull, new Vector3(0f, 0f, 0.6f), new Vector3(0.38f, 0.2f, 0.38f), Quaternion.Euler(0f, 45f, 0f));
            Primitive(PrimitiveType.Cube, "Wings", model, materials.ShipHull, new Vector3(0f, -0.02f, -0.25f), new Vector3(1.5f, 0.06f, 0.45f));
            Primitive(PrimitiveType.Cube, "WingtipL", model, materials.ShipGlow, new Vector3(-0.75f, 0f, -0.25f), new Vector3(0.1f, 0.08f, 0.5f));
            Primitive(PrimitiveType.Cube, "WingtipR", model, materials.ShipGlow, new Vector3(0.75f, 0f, -0.25f), new Vector3(0.1f, 0.08f, 0.5f));
            Primitive(PrimitiveType.Sphere, "Cockpit", model, materials.ShipGlow, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.25f, 0.15f, 0.35f));
            Primitive(PrimitiveType.Cube, "Engine", model, materials.ShipGlow, new Vector3(0f, 0f, -0.62f), new Vector3(0.35f, 0.14f, 0.1f));

            var trailObject = new GameObject("DashTrail");
            trailObject.transform.SetParent(model, false);
            trailObject.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            var trail = trailObject.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.minVertexDistance = 0.1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.9f, 1f, 0f);
            trail.sharedMaterial = materials.FxCyan;
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;

            var ship = root.AddComponent<PlayerShip>();
            SetFields(ship, ("radius", 0.45f), ("ship", definitions.Ship), ("primaryWeapon", definitions.PhotonRepeater));

            var visual = AddVisual(root, ship, prefabs.PlayerExplosion);
            SetFields(visual, ("dashTrail", trail), ("hideOnDeath", true));
            prefabs.Player = SavePrefab(root, "Player").GetComponent<PlayerShip>();
        }

        /// <summary>The red floor glow that marks every enemy as a unit rather than a projectile.</summary>
        static Renderer Halo(Transform parent, Material material, float radius)
        {
            float size = radius * 3.4f;
            return Primitive(PrimitiveType.Quad, "Halo", parent, material, new Vector3(0f, 0.03f, 0f), new Vector3(size, size, 1f), Quaternion.Euler(90f, 0f, 0f))
                .GetComponent<Renderer>();
        }

        static Transform Model(Transform parent)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(parent, false);
            model.localPosition = new Vector3(0f, 0.5f, 0f);
            return model;
        }

        static EntityVisual AddVisual(GameObject root, CombatEntity entity, GameObject deathEffect, params Renderer[] glowRenderers)
        {
            var visual = root.AddComponent<EntityVisual>();
            var so = new SerializedObject(visual);
            so.FindProperty("entity").objectReferenceValue = entity;
            so.FindProperty("deathEffect").objectReferenceValue = deathEffect;
            var glow = so.FindProperty("glowRenderers");
            glow.arraySize = glowRenderers.Length;
            for (int i = 0; i < glowRenderers.Length; i++)
                glow.GetArrayElementAtIndex(i).objectReferenceValue = glowRenderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return visual;
        }

        // ---------------------------------------------------------------- scene

        /// <summary>Strong bloom sells the neon look (GDD §13.1): emissive parts are authored well above 1.0.</summary>
        static void TunePostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/Settings/Rendering/PP_Default.asset");
            if (profile == null || !profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
                throw new InvalidOperationException("PP_Default with Bloom is missing. Run the M0 project setup first.");

            bloom.intensity.Override(2.5f);
            bloom.threshold.Override(0.8f);
            bloom.scatter.Override(0.75f);
            EditorUtility.SetDirty(profile);
        }

        static void PopulateRunScene(Materials materials, Prefabs prefabs, Definitions definitions)
        {
            // Opening a scene can unload assets that nothing references yet, which invalidates the in-memory
            // handles from the build steps above. Capture paths first and reload afterwards.
            string playerPath = AssetDatabase.GetAssetPath(prefabs.Player);
            string telegraphPath = AssetDatabase.GetAssetPath(prefabs.SpawnTelegraph);
            string floorPath = AssetDatabase.GetAssetPath(materials.Floor);
            string wallPath = AssetDatabase.GetAssetPath(materials.Wall);
            string shipPath = AssetDatabase.GetAssetPath(definitions.Ship);
            string weaponPath = AssetDatabase.GetAssetPath(definitions.PhotonRepeater);
            string dronePath = AssetDatabase.GetAssetPath(definitions.Drone);
            string gunnerPath = AssetDatabase.GetAssetPath(definitions.Gunner);

            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);

            var world = FindOrCreate<CombatWorld>("Combat World");
            SetFields(world, ("arenaSize", ArenaSize));

            var floor = GameObject.Find("ArenaFloor");
            if (floor != null)
                floor.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(floorPath);
            BuildArenaWalls(Load<Material>(wallPath));

            var spawner = FindOrCreate<PlayerSpawner>("Player Spawner");
            SetFields(spawner, ("world", world), ("shipPrefab", Load<PlayerShip>(playerPath)),
                ("ship", Load<ShipDefinition>(shipPath)), ("primaryWeapon", Load<PrimaryWeaponDefinition>(weaponPath)));

            var waves = FindOrCreate<WaveSpawner>("Wave Spawner");
            SetFields(waves, ("world", world), ("drone", Load<EnemyDefinition>(dronePath)), ("gunner", Load<EnemyDefinition>(gunnerPath)),
                ("telegraphPrefab", Load<GameObject>(telegraphPath)));

            var flow = FindOrCreate<RunFlow>("Run Flow");
            SetFields(flow, ("world", world));
            var hud = GetOrAdd<CombatHud>(flow.gameObject);
            SetFields(hud, ("world", world), ("runFlow", flow), ("waveSpawner", waves));

            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Run scene has no camera tagged MainCamera.");
            var rig = GetOrAdd<CameraRig>(camera.gameObject);
            SetFields(rig, ("world", world), ("distance", 20f));

            foreach (var component in new Component[] { spawner, waves, flow, hud, rig })
                RequireReferences(component);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing {typeof(T).Name} at '{path}'.");

        /// <summary>Fails the build if any object reference on the component is still empty.</summary>
        static void RequireReferences(Object target)
        {
            var property = new SerializedObject(target).GetIterator();
            while (property.NextVisible(enterChildren: true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.name != "m_Script")
                    throw new InvalidOperationException($"{target.GetType().Name}.{property.name} on '{target.name}' is not assigned.");
            }
        }

        static void BuildArenaWalls(Material material)
        {
            var arena = GameObject.Find("Arena");
            if (arena == null)
                arena = new GameObject("Arena");
            for (int i = arena.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(arena.transform.GetChild(i).gameObject);

            const float height = 0.4f, thickness = 0.3f;
            Vector2 half = ArenaSize * 0.5f;
            Primitive(PrimitiveType.Cube, "Wall North", arena.transform, material, new Vector3(0f, height / 2f, half.y + thickness / 2f), new Vector3(ArenaSize.x + thickness * 2f, height, thickness));
            Primitive(PrimitiveType.Cube, "Wall South", arena.transform, material, new Vector3(0f, height / 2f, -half.y - thickness / 2f), new Vector3(ArenaSize.x + thickness * 2f, height, thickness));
            Primitive(PrimitiveType.Cube, "Wall East", arena.transform, material, new Vector3(half.x + thickness / 2f, height / 2f, 0f), new Vector3(thickness, height, ArenaSize.y));
            Primitive(PrimitiveType.Cube, "Wall West", arena.transform, material, new Vector3(-half.x - thickness / 2f, height / 2f, 0f), new Vector3(thickness, height, ArenaSize.y));
        }

        static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();

        static T FindOrCreate<T>(string name) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            return existing != null ? existing : new GameObject(name).AddComponent<T>();
        }

        // ---------------------------------------------------------------- helpers

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // gameplay collision is simulated on the plane, not by physics
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (material.shader.name == UnlitShader)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject SavePrefab(GameObject root, string folder)
        {
            string dir = $"{PrefabsDir}/{folder}";
            EnsureFolder(dir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{dir}/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void SetFields(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
                switch (value)
                {
                    case Object reference: property.objectReferenceValue = reference; break;
                    case Object[] references:
                        property.arraySize = references.Length;
                        for (int i = 0; i < references.Length; i++)
                            property.GetArrayElementAtIndex(i).objectReferenceValue = references[i];
                        break;
                    case float number: property.floatValue = number; break;
                    case int integer: property.intValue = integer; break;
                    case bool flag: property.boolValue = flag; break;
                    case Vector2 vector: property.vector2Value = vector; break;
                    case string text: property.stringValue = text; break;
                    default: throw new NotSupportedException($"Unsupported value for '{field}': {value}");
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetAuto(SerializedObject so, string property, float value) => so.FindProperty($"<{property}>k__BackingField").floatValue = value;

        static void SetAuto(SerializedObject so, string property, int value) => so.FindProperty($"<{property}>k__BackingField").intValue = value;

        static void SetIfEmpty(Object target, string autoProperty, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty($"<{autoProperty}>k__BackingField");
            if (property.objectReferenceValue != null)
                return;
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
