# Project Pathfinder: New Home

A top-down, twin-stick roguelite shooter in neon-lit 3D, built in Unity 6 (6000.0.67f1, URP).
The design lives in [docs/GDD.md](docs/GDD.md). The **[documentation](docs/README.md)** explains how the code is
built and how to extend it, what every enemy, item and currency does, and the world's lore.

![Every enemy type, an elite, enemy bullets and Scraps](docs/images/m2-enemies.png)

## Status

| Milestone | State |
|---|---|
| M0 Project setup | ✅ |
| M1 Core combat feel | ✅ playtested |
| M2 Combat breadth | ✅ 3 primaries, 2 secondaries, 5 enemy types + elites, wave director, Scraps |
| M3 Run structure | ✅ galaxy map, 7 level types, 3 galaxies, victory/defeat |
| M4 Build system | ✅ 20 equipment items, stats, choice cards, Shop catalog, Titan + Stormbringer traits, Hub loadout |
| M5 Boss + meta | ✅ the Quarry Maw (3-phase rock-worm boss), run summary with banked rewards, Pathfinder Lab upgrades, save file |
| Playtest 2 fixes | ✅ readable bullets, chevron charge telegraphs, tougher Lancer, weak-point-only Quarry Maw with harder phases, Mystery events, uGUI map/shop/cards |
| M6 Slice polish | ✅ except the external playtest: synthesized SFX and music, explosions and camera kicks, crosshair ammo ring, uGUI HUD and Hub, economy probe |
| Playtest 4 fixes | ✅ per-enemy wind-up sounds, audio centred on the ship, wormhole pull after the boss reward, Scrap vacuum, Esc closes menus cleanly |
| Galaxy 2 content | ✅ Scorcher, Arc Drone and Crusher enemies; the Binary Wardens twin boss |
| Playtest 6 (first external) | ✅ an eruption you dash out of, a ship-mounted HUD readout, 16:9 HUD area, Precision Loader active reload, the Hive Matriarch (galaxy 3 boss) |

## Playing

**A full run:** open `Assets/_Project/Scenes/Boot.unity` (or `Hub.unity`), press Play, choose **Start Run** and pick a ship and loadout. You can also
open `Run.unity` directly. Pick a route on the galaxy map, clear levels, fly into the wormhole, and beat three bosses to
reach New Home. The galaxy boss is **the Quarry Maw**: it burrows and erupts under you, charges across the arena in
telegraphed volleys that shed walls of rocks, then enrages and spews bullet streams and spirals. Its shell deflects
everything: shoot the yellow weak-point crystals on its flank, and its maw whenever it opens (diving, emerging, resting
between charges, and all through the enrage).

Galaxy 2 adds three enemies: the **Scorcher** (a flamethrower grunt that marks its cone first), the **Arc Drone**
(locks a line onto you, then zaps along it) and the **Crusher** (slow and knockback-proof, but its slam hurts). Its
guardian is **the Binary Wardens**: two cores circling the arena, joined by a tether that sweeps like a clock hand. A
destroyed core only goes offline and reboots at half health, unless its twin falls while it is down: wear both down,
then finish them together.

![The Binary Wardens: two cores joined by their tether, with a health bar each](docs/images/wardens.png)

Mystery (?) nodes are usually **events**: a short scene and a choice with upsides and downsides.

![The enraged Quarry Maw: yellow weak-point bands on its body, a magenta bullet fan aimed at the ship](docs/images/m5-quarry-maw.png)

When a run ends, the Dark Matter and Singularity Cores you earned are banked, plus 10% of the Scraps you collected.
Spend them in the Hub's **Pathfinder Lab**: Hull Plating, Shield Matrix, Salvage Protocols (starting Scraps), a Second
Dash Charge, and the Quantum Charger unlock. Progress is saved to `meta.json` in `Application.persistentDataPath`; delete
it to start a fresh profile.

![A generated galaxy map: entries at the bottom converge to the Shop and Boss](docs/images/m3-map-graph.png)

**The combat sandbox:** open `Assets/_Project/Scenes/Sandbox.unity` and press Play. The wave director sends endless, escalating waves of Drones, Gunners, Spinners, Lancers and Carriers (and galaxy 2's Scorchers, Arc Drones and Crushers from wave 6), with the occasional gold-ringed elite, until the ship is destroyed. Enemies drop gold Scraps that fly to the ship.

| Action | Keyboard & Mouse | Gamepad |
|---|---|---|
| Move | WASD / arrows | Left stick |
| Aim | Mouse | Right stick |
| Fire primary (hold to charge the Quantum Charger) | Left mouse | Right trigger |
| Fire secondary | Right mouse | Left trigger |
| Dash (i-frames) | Space | A / Cross, RB |
| Reload | R | X / Square |
| Pause menu (options, controls, abandon run) | Esc | Start |
| Interact (stations), restart after a run | E | Y / Triangle |
| Galaxy map: choose a node | Left/Right + Enter, or click | D-pad / stick + A |

**Dev hotkeys** (Editor and development builds): **F1** cycle primary weapon, **F2** cycle secondary, **F3** god mode,
**F4** kill all enemies, **F5** skip to the next wave, **F6** skip the current level (Run scene), **F7** offer 3 random equipment,
**F8** +100 Scraps, **F9** reload the text files, **F10** spawn the next enemy type (every enemy and boss, in turn).

Tuning lives in the ScriptableObjects under `Assets/_Project/Data`: ships, weapons, equipment (`Data/Equipment`), meta upgrades (`Data/Meta`), enemies, elites, and the wave config
`Waves/Waves_ShatteredExpanse`. Behaviour tuning (bursts, charge speed, launch rates, the Quarry Maw's phases) is on the brain components in the
enemy prefabs. To replay an exact run, set a seed code on **Combat World** in the Run scene.

## Text and translations

All player-facing text is in `Assets/StreamingAssets/Localization/en.loc`: one `key = value` per line, with `{placeholders}`
for numbers. Edit a value and press **F9** in Play mode to see it at once. To add a language, copy `en.loc` to
`<code>.loc`, change `@language`, and translate the values; it appears under Options > Language. Missing keys fall back to
English. See GDD §12.5.

## Project layout

```
Assets/_Project/
  Scripts/Core      Pure C# rules, no Unity dependency: RNG, vitals, weapons, dash (unit-tested)
  Scripts/Game      Unity runtime: simulation, player, enemies, presentation, run flow
  Scripts/Editor    Setup scripts and content builders
  Tests/EditMode    Unit tests (Core + Game helpers)
  Tests/PlayMode    Drives the real Run scene with scripted pilot commands
  Data, Prefabs, Art, Scenes, Settings, Input
```

## Editor tools

- **Pathfinder > Setup > Apply M0 Project Setup**: converts a fresh template into the 3D URP setup. Safe to rerun.
- **Pathfinder > Setup > Build Content**: regenerates the placeholder art and prefabs, and the wiring of the Sandbox, Run
  and Hub scenes. ScriptableObject data is never overwritten. New definitions are created when missing.
  Run tuning is in `Data/Run/RunConfig`.

## Playtest builds

- **Pathfinder > Build > Windows Playtest Build** writes `Builds/Windows/ProjectPathfinder.exe` (no dev hotkeys).
  **Windows Dev Build** keeps the hotkeys. From the command line (Editor closed):

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -executeMethod Pathfinder.Editor.Build.PlayerBuild.BuildBatch -logFile build.log
```

- Zip the whole `Builds/Windows` folder for testers.
- Every finished run appends a line to `run-log.jsonl`, next to the profile, in
  `%USERPROFILE%\AppData\LocalLow\DefaultCompany\pathfinder\`. Ask testers to send it back. Each line has the
  seed, loadout, how far the run got, play time and what killed the pilot, and any run can be replayed from its seed.

## Running tests from the command line

Close the Editor first, because batch mode can't open a project the Editor has open.

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode.xml
```

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode.xml
```

Set `PATHFINDER_SCREENSHOT_DIR` before the PlayMode run to also save combat screenshots.
