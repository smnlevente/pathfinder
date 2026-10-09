using System;
using System.IO;
using Pathfinder.Game.Boot;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Pathfinder.Editor.Setup
{
    /// <summary>
    /// One-time M0 setup (GDD §14.1, §17). It converts the 2D URP template into the 3D Forward+ setup
    /// and creates the Boot/Hub/Run scenes. Safe to run again: existing assets and scenes are kept.
    /// Batch mode: <c>Unity.exe -batchmode -projectPath . -executeMethod Pathfinder.Editor.Setup.ProjectSetup.ApplyBatch</c>
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Project";
        const string RenderingDir = Root + "/Settings/Rendering";
        const string MaterialsDir = Root + "/Art/Materials";
        const string ScenesDir = Root + "/Scenes";
        const string InputActionsPath = Root + "/Input/PlayerControls.inputactions";

        const string RendererPath = RenderingDir + "/PC_Renderer.asset";
        const string PipelinePath = RenderingDir + "/PC_RPAsset.asset";
        const string PostProcessProfilePath = RenderingDir + "/PP_Default.asset";
        const string FloorMaterialPath = MaterialsDir + "/M_ArenaFloor.mat";
        const string DefaultPostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        // Leftovers from the 2D URP template that the 3D setup replaces.
        static readonly string[] TemplateLeftovers =
        {
            "Assets/Settings",
            "Assets/Scenes",
            "Assets/InputSystem_Actions.inputactions",
        };

        static readonly Color BackgroundColor = new Color(0.02f, 0.02f, 0.05f);

        [MenuItem("Pathfinder/Setup/Apply M0 Project Setup")]
        static void ApplyFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Apply M0 Project Setup",
                "Switches the project to 3D URP (Forward+), creates the Boot/Hub/Run scenes and input actions, " +
                "and deletes the 2D template assets (Assets/Settings, Assets/Scenes, InputSystem_Actions).",
                "Apply", "Cancel");

            if (!confirmed || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Apply();
            EditorSceneManager.OpenScene($"{ScenesDir}/{SceneNames.Run}.unity");
        }

        public static void ApplyBatch()
        {
            try
            {
                Apply();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Apply()
        {
            EnsureFolder(RenderingDir);
            EnsureFolder(MaterialsDir);
            EnsureFolder(ScenesDir);

            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode3D;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            GraphicsSettings.defaultRenderPipeline = CreateRenderPipeline();
            MoveToRenderingDir("Assets/UniversalRenderPipelineGlobalSettings.asset");
            MoveToRenderingDir("Assets/DefaultVolumeProfile.asset");

            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
                throw new InvalidOperationException($"Missing input actions at {InputActionsPath}.");
            InputSystem.actions = inputActions;

            CreateScenes(CreatePostProcessProfile(), CreateFloorMaterial());
            DeleteTemplateLeftovers();

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] M0 setup complete: 3D URP (Forward+), Boot/Hub/Run scenes, PlayerControls input.");
        }

        static RenderPipelineAsset CreateRenderPipeline()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessDataPath);
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            // Forward+ makes many dynamic lights cheap: projectiles and explosions light the arena (GDD §13.1).
            renderer.renderingMode = RenderingMode.ForwardPlus;
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.supportsHDR = true;
            pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.shadowDistance = 60f;
            pipeline.maxAdditionalLightsCount = 8;
            EditorUtility.SetDirty(pipeline);

            return pipeline;
        }

        static VolumeProfile CreatePostProcessProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath);
            if (profile != null)
                return profile;

            profile = VolumeProfileFactory.CreateVolumeProfileAtPath(PostProcessProfilePath);

            var bloom = VolumeProfileFactory.CreateVolumeComponent<Bloom>(profile, overrides: false);
            bloom.intensity.Override(1.2f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.7f);

            var tonemapping = VolumeProfileFactory.CreateVolumeComponent<Tonemapping>(profile, overrides: false);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = VolumeProfileFactory.CreateVolumeComponent<Vignette>(profile, overrides: false);
            vignette.intensity.Override(0.25f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static Material CreateFloorMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.06f, 0.07f, 0.1f));
            material.SetFloat("_Smoothness", 0.6f);
            AssetDatabase.CreateAsset(material, FloorMaterialPath);
            return material;
        }

        static void CreateScenes(VolumeProfile profile, Material floorMaterial)
        {
            CreateScene(SceneNames.Boot, () =>
            {
                new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
            });

            CreateScene(SceneNames.Hub, () =>
            {
                CreateCamera(new Vector3(0f, 1f, -10f), Quaternion.identity);
                CreateSun();
                CreateGlobalVolume(profile);
            });

            CreateScene(SceneNames.Run, () =>
            {
                // Top-down at ~60° pitch (GDD §13.1). The play plane is XZ at Y = 0.
                var rotation = Quaternion.Euler(60f, 0f, 0f);
                CreateCamera(Vector3.zero - rotation * Vector3.forward * 24f, rotation);
                CreateSun();
                CreateGlobalVolume(profile);

                // Placeholder arena floor matching the GDD's ~60 × 40 u arena (Unity planes are 10 × 10 u).
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "ArenaFloor";
                floor.transform.localScale = new Vector3(6f, 1f, 4f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            });

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{ScenesDir}/{SceneNames.Boot}.unity", true),
                new EditorBuildSettingsScene($"{ScenesDir}/{SceneNames.Hub}.unity", true),
                new EditorBuildSettingsScene($"{ScenesDir}/{SceneNames.Run}.unity", true),
            };
        }

        static void CreateScene(string name, Action populate)
        {
            string path = $"{ScenesDir}/{name}.unity";
            if (File.Exists(path))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.14f);
            RenderSettings.skybox = null;
            populate();
            EditorSceneManager.SaveScene(scene, path);
        }

        static void CreateCamera(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(position, rotation);

            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.fieldOfView = 40f;

            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        }

        static void CreateSun()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.75f, 0.8f, 1f);
            light.intensity = 0.8f;
            light.shadows = LightShadows.Soft;
        }

        static void CreateGlobalVolume(VolumeProfile profile)
        {
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static void MoveToRenderingDir(string assetPath)
        {
            if (!File.Exists(assetPath))
                return;

            string error = AssetDatabase.MoveAsset(assetPath, $"{RenderingDir}/{Path.GetFileName(assetPath)}");
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Could not move {assetPath}: {error}");
        }

        static void DeleteTemplateLeftovers()
        {
            foreach (string path in TemplateLeftovers)
            {
                if (AssetDatabase.IsValidFolder(path) || File.Exists(path))
                    AssetDatabase.DeleteAsset(path);
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
