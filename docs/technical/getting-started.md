# Getting started

[Docs home](../README.md) › Technical › Getting started

## Requirements

| | |
|---|---|
| **Unity** | 6000.0.67f1 (Unity 6), Universal Render Pipeline with the Forward+ renderer |
| **Packages** | Input System, uGUI (both already in the manifest) |
| **Platform** | Developed on Windows. Nothing is Windows-specific except the playtest build target. |
| **Third-party assets** | None. All art is generated primitives, and all sound and music is synthesized by the content builder. |

## Open and play

1. Clone the repository and open the folder in Unity Hub with 6000.0.67f1.
2. Open `Assets/_Project/Scenes/Boot.unity` and press **Play**. You land in the Hub.
3. Choose **Start Run**, pick a ship, a primary and a secondary weapon, and **Launch**.

Generated content (prefabs, materials, sounds, scene wiring) is committed, so a fresh clone plays straight away. You
only need the content builder when you change something it generates (see below).

### The scenes

| Scene | What it is | Open it to... |
|---|---|---|
| `Boot` | The entry point: hands over to the Hub (persistent services will start here) | play the game as a player would |
| `Hub` | Menu: Start Run, Survival (both through the loadout page), Pathfinder Lab, Options | test the meta loop and menus |
| `Run` | Galaxy map and levels, with the level controller and run director | play a run directly, skipping the Hub |
| `Survival` | One arena, endless waves and a clock, driven by the Survival controller | play Survival directly (F6 moves the clock on a minute) |
| `Sandbox` | One arena with an endless wave director; not in the Hub | tune combat, try enemies (F10 spawns any of them) |

All five live in `Assets/_Project/Scenes`.

### Dev hotkeys

Active in the Editor and in development builds only.

| Key | Action |
|---|---|
| F1 / F2 | Cycle the primary / secondary weapon |
| F3 | God mode |
| F4 | Kill every enemy |
| F5 | Skip to the next wave |
| F6 | Skip the current level (Run scene) |
| F7 | Offer a choice of 3 random equipment items |
| F8 | +100 Scraps |
| F9 | Reload the `.loc` text files |
| F10 | Spawn the next enemy type in front of the ship, cycling through every enemy and boss |

To replay an exact run, set a seed code on the **Combat World** component in the Run scene.

## Editor menus

| Menu | What it does |
|---|---|
| **Pathfinder › Setup › Build Content** | Regenerates the placeholder art, prefabs, effects, sounds and music, and rewires the Sandbox, Run, Survival and Hub scenes. Creates any content definition that is missing, but **never overwrites tuning** in an existing one. See [Content pipeline](content-pipeline.md). |
| **Pathfinder › Setup › Apply M0 Project Setup** | Converts a fresh template project to the 3D URP setup. Safe to rerun; you won't normally need it. |
| **Pathfinder › Build › Windows Playtest Build** | Writes `Builds/Windows/ProjectPathfinder.exe` without dev hotkeys. |
| **Pathfinder › Build › Windows Dev Build** | The same, with dev hotkeys. |

> [!IMPORTANT]
> Prefabs, generated materials, sound files and scene wiring are **outputs** of the content builder. Edit them by
> changing the builder (`Assets/_Project/Scripts/Editor/Setup`) or the data it reads, not by hand: the next build
> replaces them. Tuning values on ScriptableObjects in `Assets/_Project/Data` are yours and are kept.

## Command line

Close the Editor first: batch mode can't open a project that the Editor already has open.

Rebuild content:

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -executeMethod Pathfinder.Editor.Setup.ContentBuilder.BuildBatch -logFile build.log
```

Run the tests:

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode.xml
```

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode.xml
```

Set `PATHFINDER_SCREENSHOT_DIR` before a PlayMode run to also save screenshots of enemies, bosses and menus. See
[Testing](testing.md).

Make a playtest build:

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -executeMethod Pathfinder.Editor.Build.PlayerBuild.BuildBatch -logFile build.log
```

Adjust the Unity path to your install.

## Where things are saved

| File | Where | What |
|---|---|---|
| `meta.json` | `Application.persistentDataPath` (on Windows: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\pathfinder\`) | The profile: Dark Matter, Singularity Cores, Lab upgrades, run stats. Delete it to start fresh. |
| `run.json` | Next to `meta.json` | The run in progress, written at each galaxy map and deleted when the run ends (`RunSaveFile`). Readable JSON; delete it to forget the run. |
| `run-log.jsonl` | Next to `meta.json` | One line per finished run: seed, loadout, progress, play time and what killed the pilot. Ask testers for it. |
| Settings | `PlayerPrefs` | Language, volumes, screen shake, fullscreen, FPS counter. |

Tests never touch these: they redirect the save path and turn settings persistence off.

## Common problems

| Symptom | Cause and fix |
|---|---|
| Batch build or tests fail with "another Unity instance is running" | The Editor has the project open. Close it, then rerun. |
| No sound in the Editor | Check **Mute Audio** in the Game view toolbar. The console logs one `[Audio]` line on start with the output rate and volumes. |
| Text shows as `[some.key]` | The key is missing from `Assets/StreamingAssets/Localization/en.loc`. Add it and press F9. The localization tests also catch this. |
| A prefab change disappeared | Prefabs are rebuilt by **Build Content**. Make the change in the builder instead. |
| A run plays differently from a seed you shared | Seeds only reproduce runs on the same version of the content and code. |
