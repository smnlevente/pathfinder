# Testing

[Docs home](../README.md) › Technical › Testing

About 250 automated tests guard the rules, the content and the game itself. Run them before every commit.

| Suite | Where | Runs | Count |
|---|---|---|---|
| **EditMode** | `Assets/_Project/Tests/EditMode` | Without entering play mode: pure rules, content checks, localization coverage | ~140 |
| **PlayMode** | `Assets/_Project/Tests/PlayMode` | In the real Sandbox, Run and Hub scenes, with scripted pilots | ~110 |

Run them from **Window › General › Test Runner**, or from the command line (Editor closed):

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults editmode.xml
```

```bash
"D:/UnityInstalls/6000.0.67f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults playmode.xml
```

Add `-testFilter "WardenTests|GalaxyTwoEnemyTests"` to run a subset.

## EditMode

| Area | Tests |
|---|---|
| Combat rules (Core) | `VitalsTests`, `MagazineWeaponTests`, `ChargeWeaponTests`, `CooldownTests`, `DashStateTests`, `WaveComposerTests` |
| Randomness | `Pcg32Tests` (reference outputs), `SeedTests`, `RandomExtensionsTests`, `RunSeedTests` |
| Run structure | `GalaxyMapGeneratorTests` (every map rule over 500 seeds), `RunStateTests` |
| Equipment and meta | `EquipmentCoreTests` (stat formula, slots, offers), `MetaStateTests` |
| Content | `ContentDefinitionTests` (unique IDs), `GalaxyContentTests` (galaxy gating, each galaxy's boss), `PlaneMathTests` |
| Text | `LocTableTests` (the parser), `LocalizationCoverageTests` (every key the game uses exists in English) |

## PlayMode

| Area | Tests |
|---|---|
| Ship and weapons | `CombatSandboxTests`, `WeaponTests` |
| Enemies | `EnemyTests`, `GalaxyTwoEnemyTests` |
| Bosses | `BossTests` (the Quarry Maw), `WardenTests` (the Binary Wardens) |
| Equipment | `EquipmentTests` |
| Run flow | `RunTests`, `MysteryEventTests`, `EconomyAndWaveTests` |
| Meta and menus | `MetaTests`, `HubTests`, `PauseMenuTests`, `AudioTests` |
| Determinism | `DeterminismAndScreenshotTests` (the same seed gives the same fight, tick for tick) |
| Balance probes | `BalanceTests` (what levels pay), `Balance_TimeToKill` in the boss tests |
| Screenshots | `RunScreenshotTests` and the `Capture*` tests |

### Writing a PlayMode test

Derive from `SandboxTestBase`. It loads the Sandbox with a fixed seed, turns off the wave director, and gives you the
world and the pilot:

```csharp
public class MortarTests : SandboxTestBase
{
    [UnityTest]
    public IEnumerator Mortar_ShowsItsBlastThenHits()
    {
        yield return LoadRun();                                   // fixed seed, director off
        var mortar = Spawn("enemy.mortar", new Vector2(0f, 6f));  // offset from the pilot
        var brain = mortar.GetComponent<MortarBrain>();

        yield return WaitUntilTicks(() => brain.Current == MortarBrain.State.Aim, 300);
        Assert.IsTrue(((IAreaTelegraph)brain).ShowsArea, "It shows where the shell lands.");

        yield return WaitUntilTicks(() => DamageTaken > 0f, 120);
        Assert.Greater(DamageTaken, 0f, "A ship that stays put gets hit.");
    }
}
```

| Helper | Does |
|---|---|
| `LoadRun(keepDirector, seed)` | Loads the Sandbox with a seed; the wave director stays off unless asked |
| `Spawn(id, offset, eliteId)` | Spawns an enemy from the wave pool or the content database next to the pilot |
| `Script(tick => new ShipCommand { ... })` | Flies the ship: move, aim, fire, dash, per tick |
| `Once(command)` | A command for a single tick (a dash, a secondary) |
| `WaitTicks(n)`, `WaitUntilTicks(condition, max)` | Waits on the **simulation** tick, not on frames |
| `DamageTaken`, `Kill(entity)`, `SpawnEnemyBullet(...)` | Common checks and setups |

Run-scene tests (`RunTests`) drive the `RunDirector` instead: choose nodes, `DebugTakeExit()` to skip levels, and
`StartTestLevel(type)` to jump straight into a level of the current galaxy.

### Sound during tests

`SilentTestRun` mutes the game (`AudioListener.volume = 0`) for the whole PlayMode run, so a batch run doesn't fill
the room with music and explosions. Sounds still play and are checked by `AudioTests`; they just aren't heard.

### Watching the tests

Batch runs have no window. To **watch** the PlayMode tests, open the project in the Editor, open **Window › General ›
Test Runner**, choose **PlayMode** and run a test or a whole class: the Game view shows each fight as it plays.
Most tests take a few seconds; the balance bots run at 5× speed. For a slower look, set `Time.timeScale` lower in a
test while you watch, or capture screenshots (below).

### Rules for tests

- **Never touch the player's data.** Tests that bank rewards point `MetaProgression.SavePath` at a temporary file, and
  `GameSettings.Persist` is off so options don't write `PlayerPrefs`.
- **Wait on ticks.** Gameplay is tick-based; waiting a number of frames is flaky.
- **Prefer behaviour over numbers.** Assert "the ship takes damage while standing in the cone", not exact damage
  values, unless the number is the point (a boss's health, a Shop price).
- **Speed up long fights** with `Time.timeScale` inside the test only, as the balance bots do, and restore it.

## Bots and balance probes

Some tests play the game with a bot to keep balance honest:

- **`BalanceTests`** plays real levels in god mode with perfect aim and logs what each pays in Scraps and Dark Matter,
  and how long it takes. The economy section of the GDD is built from these numbers.
- **`Balance_TimeToKill`** in `BossTests` and `WardenTests` logs how long each boss takes with the default loadout and
  fails if it falls outside a sensible range. The Wardens' bot plays the intended way: it always shoots the core with
  more health left, so both wear down together.

The results are written to the test log as lines starting with `[Balance]`.

## Screenshots

Set `PATHFINDER_SCREENSHOT_DIR` to a folder before a PlayMode run, and the tests in category `Screenshot` render
enemies mid-attack, bosses, levels and menus to PNG files (the HUD included where it matters). Without the variable
they are skipped. Use them to review a visual change without opening the Editor; the images in `docs/images` came
from these tests.
