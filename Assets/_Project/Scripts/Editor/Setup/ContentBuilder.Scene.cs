using System;
using System.Linq;
using Pathfinder.Game.Audio;
using Pathfinder.Game.Boot;
using Pathfinder.Game.Combat;
using Pathfinder.Game.Content;
using Pathfinder.Game.Dev;
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
    public static partial class ContentBuilder
    {
        const string ScenesDir = Root + "/Scenes";
        const string SandboxScenePath = ScenesDir + "/Sandbox.unity";
        const string HubScenePath = ScenesDir + "/Hub.unity";

        /// <summary>Strong bloom sells the neon look (GDD §13.1): emissive parts are authored well above 1.0.</summary>
        static void TunePostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/Settings/Rendering/PP_Default.asset");
            if (profile == null || !profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
                throw new InvalidOperationException("PP_Default with Bloom is missing. Run the M0 project setup first.");

            // Moderate bloom: enough for the neon look, but dense bullet patterns and many enemies mustn't blow out
            // the screen (playtest 2).
            bloom.intensity.Override(1.3f);
            bloom.threshold.Override(1f);
            bloom.scatter.Override(0.65f);
            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// M3 split the combat scene: the endless combat playground became Sandbox, and Run became the full run.
        /// An existing Run scene from M1–M2 is moved to Sandbox, keeping its GUID, and Run is recreated from it.
        /// </summary>
        static void EnsureScenes()
        {
            if (!System.IO.File.Exists(SandboxScenePath) && System.IO.File.Exists(RunScenePath))
            {
                string error = AssetDatabase.MoveAsset(RunScenePath, SandboxScenePath);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException($"Could not move the Run scene to Sandbox: {error}");
            }
            if (!System.IO.File.Exists(RunScenePath) && !AssetDatabase.CopyAsset(SandboxScenePath, RunScenePath))
                throw new InvalidOperationException("Could not create the Run scene from Sandbox.");

            var scenes = new[] { SceneNames.Boot, SceneNames.Hub, SceneNames.Run, SceneNames.Sandbox };
            EditorBuildSettings.scenes = scenes.Select(name => new EditorBuildSettingsScene($"{ScenesDir}/{name}.unity", true)).ToArray();
        }

        static void PopulateScenes(Art art, Actors actors, Data data, RunArt runArt, RunConfig runConfig, EquipmentContent equipment, SoundLibrary audio)
        {
            // Opening a scene can unload assets that nothing references yet, which invalidates the in-memory
            // handles from the build steps above. Capture paths first and reload afterwards.
            var assets = new ScenePaths
            {
                Player = AssetPathOf(actors.Player), Telegraph = AssetPathOf(art.SpawnTelegraph), Scrap = AssetPathOf(art.ScrapVisual), Deflect = AssetPathOf(art.Deflect),
                Floor = AssetPathOf(art.Floor), Wall = AssetPathOf(art.Wall), Ship = AssetPathOf(data.Ship), Waves = AssetPathOf(data.Waves),
                RunConfig = AssetPathOf(runConfig), Database = AssetPathOf(equipment.Database), Audio = AssetPathOf(audio),
                Primaries = Array.ConvertAll(data.Primaries, p => AssetPathOf(p)),
                Secondaries = Array.ConvertAll(data.Secondaries, s => AssetPathOf(s)),
            };

            PopulateCombatScene(SandboxScenePath, assets, isRun: false);
            PopulateCombatScene(RunScenePath, assets, isRun: true);
            PopulateHubScene(assets);
        }

        sealed class ScenePaths
        {
            public string Player, Telegraph, Scrap, Deflect, Floor, Wall, Ship, Waves, RunConfig, Database, Audio;
            public string[] Primaries, Secondaries;
        }

        static string AssetPathOf(Object asset) => AssetDatabase.GetAssetPath(asset);

        static void PopulateCombatScene(string scenePath, ScenePaths assets, bool isRun)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var world = FindOrCreate<CombatWorld>("Combat World");
            SetFields(world, ("arenaSize", ArenaSize), ("scrapPickupPrefab", Load<GameObject>(assets.Scrap)), ("deflectEffectPrefab", Load<GameObject>(assets.Deflect)));
            AddAudio(assets, isRun ? MusicTrack.Hub : MusicTrack.Combat); // a run opens on the map

            var floor = GameObject.Find("ArenaFloor");
            if (floor != null)
                floor.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(assets.Floor);
            BuildArenaWalls(Load<Material>(assets.Wall));

            var primaries = Array.ConvertAll(assets.Primaries, p => (Object)Load<PrimaryWeaponDefinition>(p));
            var secondaries = Array.ConvertAll(assets.Secondaries, p => (Object)Load<SecondaryWeaponDefinition>(p));

            var database = Load<ContentDatabase>(assets.Database);
            var spawner = FindOrCreate<PlayerSpawner>("Player Spawner");
            SetFields(spawner, ("world", world), ("shipPrefab", Load<PlayerShip>(assets.Player)), ("ship", Load<ShipDefinition>(assets.Ship)),
                ("primaryWeapon", primaries[0]), ("secondaryWeapon", secondaries[0]), ("useRunLoadout", isRun), ("content", database));

            // M2 replaced the M1 test spawner with the wave director. Its script no longer exists.
            var oldSpawner = GameObject.Find("Wave Spawner");
            if (oldSpawner != null)
                Object.DestroyImmediate(oldSpawner);

            var director = FindOrCreate<WaveDirector>("Wave Director");
            SetFields(director, ("world", world), ("config", Load<WaveDirectorConfig>(assets.Waves)), ("telegraphPrefab", Load<GameObject>(assets.Telegraph)),
                ("autoStart", !isRun));

            var flow = FindOrCreate<RunFlow>("Run Flow");
            var hud = GetOrAdd<CombatHud>(flow.gameObject);
            var equipmentChoice = GetOrAdd<EquipmentChoiceScreen>(flow.gameObject);
            var pause = GetOrAdd<PauseMenu>(flow.gameObject);
            var debug = FindOrCreate<DebugHotkeys>("Dev Tools");

            RunDirector runDirector = null;
            LevelController level = null;
            ChoiceDialog dialog = null;
            if (isRun)
            {
                runDirector = FindOrCreate<RunDirector>("Run Director");
                level = GetOrAdd<LevelController>(runDirector.gameObject);
                dialog = GetOrAdd<ChoiceDialog>(runDirector.gameObject);
                var mapScreen = GetOrAdd<GalaxyMapScreen>(runDirector.gameObject);

                SetFields(level, ("world", world), ("director", director), ("config", Load<RunConfig>(assets.RunConfig)), ("dialog", dialog),
                    ("equipmentChoice", equipmentChoice), ("content", database));
                SetFields(runDirector, ("world", world), ("level", level));
                SetFields(mapScreen, ("director", runDirector));
                foreach (var component in new Component[] { runDirector, level, mapScreen })
                    RequireReferences(component);
            }

            SetFields(flow, ("world", world), ("runDirector", (Object)runDirector));
            SetFields(pause, ("runFlow", flow), ("runDirector", (Object)runDirector));
            SetFields(hud, ("world", world), ("runFlow", flow), ("waveDirector", director), ("debugHotkeys", debug), ("runDirector", (Object)runDirector));
            SetFields(debug, ("waveDirector", director), ("primaryWeapons", primaries), ("secondaryWeapons", secondaries), ("level", (Object)level),
                ("content", database), ("equipmentChoice", equipmentChoice), ("dialog", (Object)dialog));

            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException($"{scenePath} has no camera tagged MainCamera.");
            var rig = GetOrAdd<CameraRig>(camera.gameObject);
            SetFields(rig, ("world", world), ("distance", 20f));

            foreach (var component in new Component[] { world, spawner, director, rig })
                RequireReferences(component);
            RequireReferences(flow, optional: "runDirector");
            RequireReferences(hud, optional: "runDirector");
            RequireReferences(debug, "level", "dialog");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void PopulateHubScene(ScenePaths assets)
        {
            var scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            var menu = FindOrCreate<HubMenu>("Hub Menu");
            SetFields(menu, ("content", Load<ContentDatabase>(assets.Database)));
            AddAudio(assets, MusicTrack.Hub);
            RequireReferences(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Every scene gets a sound director (GDD §13.2).</summary>
        static void AddAudio(ScenePaths assets, MusicTrack startTrack)
        {
            var audio = FindOrCreate<GameAudio>("Audio");
            SetFields(audio, ("library", Load<SoundLibrary>(assets.Audio)), ("startTrack", (int)startTrack), ("masterVolume", 1f), ("musicVolume", 0.55f));
            RequireReferences(audio);
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
    }
}
