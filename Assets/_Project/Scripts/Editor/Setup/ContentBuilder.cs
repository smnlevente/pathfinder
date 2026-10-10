using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Pathfinder.Editor.Setup
{
    /// <summary>
    /// Generates the game content: placeholder materials, textures, prefabs and effects, the content definitions, and the
    /// wiring of the Sandbox, Run and Hub scenes.
    /// Rerunning rebuilds the placeholder art and prefabs (asset GUIDs are kept) but never overwrites tuning in
    /// existing content definitions.
    /// Batch mode: <c>Unity.exe -batchmode -projectPath . -executeMethod Pathfinder.Editor.Setup.ContentBuilder.BuildBatch</c>
    /// </summary>
    public static partial class ContentBuilder
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

        /// <summary>Generated assets from earlier builder versions that nothing uses anymore.</summary>
        static readonly string[] ObsoleteAssets =
        {
            MaterialsDir + "/M_DroneHull.mat",
            MaterialsDir + "/M_DroneGlow.mat",
            MaterialsDir + "/M_GunnerHull.mat",
            MaterialsDir + "/M_GunnerGlow.mat",
            MaterialsDir + "/M_EnemyBullet.mat",
        };

        [MenuItem("Pathfinder/Setup/Build Content")]
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

            EnsureScenes();
            var art = BuildArt();
            var data = BuildDefinitions();
            var actors = BuildActors(art, data);
            LinkDefinitions(art, actors, data);
            var runArt = BuildRunArt(art);
            var runConfig = BuildRunConfig(data, runArt);

            TunePostProcessing();
            PopulateScenes(art, actors, data, runArt, runConfig);
            AssetDatabase.SaveAssets();
            Debug.Log("[ContentBuilder] Combat sandbox built.");
        }

        static void DeleteObsoleteAssets()
        {
            foreach (string path in ObsoleteAssets)
            {
                if (File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        // ---------------------------------------------------------------- materials & textures

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

        /// <summary>Additive, soft-edged material for glows, halos and particles.</summary>
        static Material Additive(string name, Color hdrColor, Texture2D texture) => Material(name, ParticleShader, m =>
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

        static Texture2D CreateTexture(string name, int size, Func<float, float, Color> pixel, TextureWrapMode wrap, bool alphaIsTransparency)
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

        /// <summary>Radial texture: <paramref name="alpha"/> maps normalised distance from the centre (0..1 at the edge) to opacity.</summary>
        static Texture2D RadialTexture(string name, int size, Func<float, float> alpha) => CreateTexture(name, size, (x, y) =>
        {
            float half = size * 0.5f;
            float dx = (x + 0.5f) / half - 1f, dy = (y + 0.5f) / half - 1f;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy))));
        }, TextureWrapMode.Clamp, alphaIsTransparency: true);

        // ---------------------------------------------------------------- object helpers

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
            if (material.shader.name != LitShader)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A flat quad lying on the plane, facing up.</summary>
        static GameObject FlatQuad(string name, Transform parent, Material material, float height, float size) =>
            Primitive(PrimitiveType.Quad, name, parent, material, new Vector3(0f, height, 0f), new Vector3(size, size, 1f), Quaternion.Euler(90f, 0f, 0f));

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
                var property = so.FindProperty(field) ?? so.FindProperty($"<{field}>k__BackingField")
                    ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
                Assign(property, value, field);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Assign(SerializedProperty property, object value, string field)
        {
            switch (value)
            {
                case null: property.objectReferenceValue = null; break;
                case Object reference: property.objectReferenceValue = reference; break;
                case Object[] references:
                    property.arraySize = references.Length;
                    for (int i = 0; i < references.Length; i++)
                        property.GetArrayElementAtIndex(i).objectReferenceValue = references[i];
                    break;
                case float number: property.floatValue = number; break;
                case int integer:
                    if (property.propertyType == SerializedPropertyType.Enum)
                        property.enumValueIndex = integer;
                    else
                        property.intValue = integer;
                    break;
                case bool flag: property.boolValue = flag; break;
                case Vector2 vector: property.vector2Value = vector; break;
                case string text: property.stringValue = text; break;
                default: throw new NotSupportedException($"Unsupported value for '{field}': {value}");
            }
        }

        static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Missing {typeof(T).Name} at '{path}'.");

        /// <summary>Fails the build if any object reference on the component is still empty, except the listed optional fields.</summary>
        static void RequireReferences(Object target, params string[] optional)
        {
            var property = new SerializedObject(target).GetIterator();
            while (property.NextVisible(enterChildren: true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null
                    && property.name != "m_Script" && System.Array.IndexOf(optional, property.name) < 0)
                    throw new InvalidOperationException($"{target.GetType().Name}.{property.propertyPath} on '{target.name}' is not assigned.");
            }
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
