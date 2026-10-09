using System;
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

        static void PopulateRunScene(Art art, Actors actors, Data data)
        {
            // Opening a scene can unload assets that nothing references yet, which invalidates the in-memory
            // handles from the build steps above. Capture paths first and reload afterwards.
            string Path(Object asset) => AssetDatabase.GetAssetPath(asset);
            string playerPath = Path(actors.Player), telegraphPath = Path(art.SpawnTelegraph), scrapPath = Path(art.ScrapVisual);
            string floorPath = Path(art.Floor), wallPath = Path(art.Wall);
            string shipPath = Path(data.Ship), wavesPath = Path(data.Waves);
            string[] primaryPaths = Array.ConvertAll(data.Primaries, p => Path(p));
            string[] secondaryPaths = Array.ConvertAll(data.Secondaries, s => Path(s));

            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);

            var world = FindOrCreate<CombatWorld>("Combat World");
            SetFields(world, ("arenaSize", ArenaSize), ("scrapPickupPrefab", Load<GameObject>(scrapPath)));

            var floor = GameObject.Find("ArenaFloor");
            if (floor != null)
                floor.GetComponent<MeshRenderer>().sharedMaterial = Load<Material>(floorPath);
            BuildArenaWalls(Load<Material>(wallPath));

            var primaries = Array.ConvertAll(primaryPaths, p => (Object)Load<PrimaryWeaponDefinition>(p));
            var secondaries = Array.ConvertAll(secondaryPaths, p => (Object)Load<SecondaryWeaponDefinition>(p));

            var spawner = FindOrCreate<PlayerSpawner>("Player Spawner");
            SetFields(spawner, ("world", world), ("shipPrefab", Load<PlayerShip>(playerPath)), ("ship", Load<ShipDefinition>(shipPath)),
                ("primaryWeapon", primaries[0]), ("secondaryWeapon", secondaries[0]));

            // M2 replaced the M1 test spawner with the wave director. Its script no longer exists.
            var oldSpawner = GameObject.Find("Wave Spawner");
            if (oldSpawner != null)
                Object.DestroyImmediate(oldSpawner);

            var director = FindOrCreate<WaveDirector>("Wave Director");
            SetFields(director, ("world", world), ("config", Load<WaveDirectorConfig>(wavesPath)), ("telegraphPrefab", Load<GameObject>(telegraphPath)));

            var debug = FindOrCreate<DebugHotkeys>("Dev Tools");
            SetFields(debug, ("waveDirector", director), ("primaryWeapons", primaries), ("secondaryWeapons", secondaries));

            var flow = FindOrCreate<RunFlow>("Run Flow");
            SetFields(flow, ("world", world));
            var hud = GetOrAdd<CombatHud>(flow.gameObject);
            SetFields(hud, ("world", world), ("runFlow", flow), ("waveDirector", director), ("debugHotkeys", debug));

            var camera = Camera.main;
            if (camera == null)
                throw new InvalidOperationException("Run scene has no camera tagged MainCamera.");
            var rig = GetOrAdd<CameraRig>(camera.gameObject);
            SetFields(rig, ("world", world), ("distance", 20f));

            foreach (var component in new Component[] { world, spawner, director, debug, flow, hud, rig })
                RequireReferences(component);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
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
