# Extending the game

[Docs home](../README.md) › Technical › Extending the game

Step-by-step recipes for the changes you'll make most. Each ends with a checklist. The galaxy 2 enemies and the
Binary Wardens were added exactly this way, so they are good worked examples: see `ScorcherBrain`,
`ArcDroneBrain`, `CrusherBrain`, `WardenBrain` and `ContentBuilder.Galaxy2.cs`.

> [!IMPORTANT]
> Close the Editor before running the content builder or tests from the command line, and rerun
> **Pathfinder › Setup › Build Content** after changing anything the builder generates.

## Where values live

| To change... | Edit |
|---|---|
| An enemy's health, speed, contact damage, cost, drops, knockback | Its `EnemyDefinition` in `Data/Enemies` |
| How an enemy behaves (wind-ups, ranges, bullet patterns) | The brain component's fields, set in the builder (prefabs are regenerated) |
| Which enemies appear when | `Data/Waves/Waves_ShatteredExpanse` (weight, first wave, first galaxy) |
| Level rewards, Shop prices, each galaxy's boss | `Data/Run/RunConfig` |
| Ships, weapons, equipment values | Their definitions in `Data/Ships`, `Data/Weapons`, `Data/Equipment` |
| Lab upgrade costs | `Data/Meta` |
| Any text | `Assets/StreamingAssets/Localization/en.loc` (F9 reloads in play mode) |
| A sound's volume, pitch variation, voice cap | `Data/Audio/SoundLibrary` |

## Add an enemy

### 1. Write its brain

A brain decides, each tick, where the enemy wants to go and what it does. Draw randomness only from `self.Random`, and
scale attack timers by `self.AttackRate` so Hasty elites work.

```csharp
namespace Pathfinder.Game.Enemies
{
    /// <summary>Mortar: lobs a shell at where the ship is, after showing the landing circle.</summary>
    public sealed class MortarBrain : EnemyBrain, IAreaTelegraph
    {
        public enum State { Reposition, Aim, Cooldown }

        [SerializeField, Min(0.1f)] float aimTime = 0.8f;
        [SerializeField, Min(0.1f)] float cooldown = 3f;
        [SerializeField, Min(0.1f)] float blastRadius = 2f;
        [SerializeField, Min(0f)] float blastDamage = 20f;

        float _timer;
        Vector2 _target;

        public State Current { get; private set; }

        // The floor telegraph reads these. (Here centred on the enemy; a real mortar would need a target position.)
        bool IAreaTelegraph.ShowsArea => Current == State.Aim;
        float IAreaTelegraph.AreaRadius => blastRadius;
        float IAreaTelegraph.AreaArc => 360f;
        float IAreaTelegraph.AreaProgress => 1f - _timer / aimTime;

        public override void ResetState(Enemy self)
        {
            Current = State.Cooldown;
            _timer = self.Random.Range(0.5f, cooldown); // stagger a wave's first shots
            self.IsTelegraphing = false;
        }

        public override Vector2 Think(Enemy self, float deltaTime, CombatWorld world)
        {
            var target = Target(self); // nearest living player, or null
            _timer -= deltaTime * self.AttackRate;
            switch (Current)
            {
                case State.Aim:
                    self.SpeedMultiplier = 0f; // multipliers reset every tick: set them while they apply
                    if (_timer <= 0f)
                    {
                        Explode(self, world);
                        Enter(self, State.Cooldown, cooldown);
                    }
                    return Vector2.zero;

                case State.Cooldown:
                    if (_timer <= 0f && target != null)
                    {
                        _target = target.Position;
                        Enter(self, State.Aim, aimTime); // IsTelegraphing → wind-up sound and glow
                    }
                    return Vector2.zero;
            }
            return Vector2.zero;
        }

        void Enter(Enemy self, State state, float duration)
        {
            Current = state;
            _timer = duration;
            self.IsTelegraphing = state == State.Aim;
        }

        void Explode(Enemy self, CombatWorld world) { /* damage players in range on the authority */ }
    }
}
```

Useful pieces:

| Need | Use |
|---|---|
| Fire a bullet | `world.Projectiles.Spawn(new ProjectileSpawn(position, direction, speed, damage, radius, range, self.Team, self.EntityId, bulletPrefab, impactPrefab))` |
| Face somewhere | `self.FaceTowards(direction)` or `self.TurnTowards(direction, maxDegrees)` |
| Charge or stand still this tick | `self.SpeedMultiplier`, `self.AccelerationMultiplier`, `self.ContactDamageMultiplier` |
| Take less (or more) damage | Implement `IDamageModifier` |
| Refuse a lethal hit | Implement `IDeathGuard` |
| Show a telegraph | Implement `IChargeTelegraph`, `IAreaTelegraph` or `IBeamTelegraph` |
| Hit players directly (a slam, a zap) | Loop over `CombatRegistry.Players`, skip `!IsAlive` and `IsIntangible`, call `ApplyDamage` |
| Kick the camera | `Presentation.CameraRig.Kick(0.2f)` |

### 2. Add its definition and prefab in the builder

In `ContentBuilder.Data.cs`, add a field to `Data`, create the definition (only made if missing), and include it in
`Data.Enemies` so it reaches the content database:

```csharp
Mortar = Definition<EnemyDefinition>("Enemies/Enemy_Mortar", "enemy.mortar", "Mortar", so => Set(so,
    ("MaxHealth", 50f), ("MoveSpeed", 2f), ("ContactDamage", 10f), ("BudgetCost", 3), ("ScrapsDrop", 2), ("ScrapsDropChance", 0.8f))),
```

Build the prefab in an actors partial, following the visual language (crimson hull, red-orange glow strips, a red floor
halo) and link it in `LinkDefinitions`:

```csharp
static Enemy BuildMortar(Art art, Data data)
{
    var root = EnemyRoot("Mortar", art, radius: 0.6f, out var halo, out var model);
    Primitive(PrimitiveType.Cylinder, "Base", model, art.EnemyHull, Vector3.zero, new Vector3(1.1f, 0.25f, 1.1f));
    var tube = Glow(Primitive(PrimitiveType.Cylinder, "Tube", model, art.EnemyGlow, new Vector3(0f, 0.4f, 0f), new Vector3(0.3f, 0.3f, 0.3f)));

    var brain = root.AddComponent<MortarBrain>();
    AreaTelegraphChild(root.transform, brain, art.SlamArea); // renderers start disabled
    return FinishEnemy(root, data.Mortar, radius: 0.6f, art, halo, tube); // glow parts flare while telegraphing
}
```

### 3. Put it in the wave pool

Add it to the pool in `BuildDefinitions` for new configs, and, because the existing config asset is never
overwritten, add it with a one-off upgrade like `AddGalaxyTwoEnemies` (or add the entry in the inspector). Set the
**weight**, the **first wave** and the **first galaxy** (0-based).

### 4. Give it a voice and a name

- **Wind-up sound:** add a `SoundCue` **at the end of the enum**, a recipe in `ContentBuilder.Audio.cs`, and a line in
  `GameAudio.OnTelegraphed` that maps the brain type to the cue.
- **Name:** `enemy.mortar.name = Mortar` in `en.loc`.

### 5. Test it

Add a PlayMode test that spawns it next to the ship and checks its tell and its attack (see `GalaxyTwoEnemyTests`).
In the Sandbox, **F10** spawns it in front of the ship.

**Checklist:** brain · definition · prefab and link · `Data.Enemies` · wave pool (new and existing configs) · wind-up
cue and mapping · `enemy.<id>.name` · PlayMode test · [enemies page](../game/enemies.md) and GDD §7.3

## Add a boss

1. **Brain.** Same as an enemy, usually with phases or parts. The Quarry Maw spawns its segments on its first tick;
   the Binary Wardens' Alpha spawns its twin.
2. **Definition and prefab** in a builder partial, like `BuildQuarryMaw` or `BuildBinaryWardens`. Bosses usually set
   `KnockbackMultiplier` to 0.
3. **Which galaxy.** Put it in `RunConfig.GalaxyBosses` (galaxy 1, 2, 3). `LevelController` spawns the boss for the
   current galaxy and treats its death as the win: bullets defuse, Scraps vacuum, cores and the reward follow.
4. **Text.** `<id>.name` and `<id>.hint`, the one-line "how to hurt it" shown for the first 8 s of the fight. A test
   checks every galaxy's boss has both.
5. **A boss in several pieces** implements `IMultiPartBoss` on the main part's brain, and the HUD shows a bar per part.
   If killing one piece shouldn't end the fight, implement `IDeathGuard`. The enemy the level spawned must be the one
   whose death ends the fight.
6. **Balance.** Add a time-to-kill bot test like `BossTests.Balance_TimeToKill` or `WardenTests.Balance_TimeToKill`
   and log the result.

## Add an equipment item

### A stat item (no code)

Most passive items are pure stat modifiers. Add one line in `BuildEquipment`:

```csharp
content.Items.Add(StatItem("Reactive Plating", "reactive-plating", EquipmentCategory.Hull, Rarity.Rare,
    (StatId.DamageTaken, null, new[] { -0.08f, -0.14f, -0.2f })));   // flat per level, percent per level
```

Its description is generated from the stat names, so it only needs `equipment.reactive-plating.name` in `en.loc`.

### A behaviour item

Subclass `EquipmentDefinition` and give it an `EquipmentEffect`. Here is the whole of Kill Refund:

```csharp
[CreateAssetMenu(menuName = "Pathfinder/Equipment/Kill Refund", fileName = "Equipment_KillRefund")]
public sealed class KillRefundDefinition : EquipmentDefinition
{
    [field: SerializeField] public float[] SecondsPerKill { get; private set; } = { 0.3f, 0.45f, 0.6f };

    public override EquipmentEffect CreateEffect() => new Effect(this);

    public override string Describe(int level) => Localization.Loc.T($"{Id}.desc", ("seconds", AtLevel(SecondsPerKill, level)));

    sealed class Effect : EquipmentEffect
    {
        readonly KillRefundDefinition _definition;

        public Effect(KillRefundDefinition definition) => _definition = definition;

        public override void OnKill(Enemy enemy, in DamageInfo info) => Ship.SecondaryCooldown?.Tick(AtLevel(_definition.SecondsPerKill, Level));
    }
}
```

The hooks an effect can override:

| Hook | When |
|---|---|
| `ApplyStats(StatSheet)` | Whenever the loadout changes (add modifiers here) |
| `OnLevelStart()` | Each level begins |
| `OnShotFired(ref ShotParams)` | The primary fires (change the shot) |
| `OnHit(target, info, result)` | A primary projectile hits |
| `OnKill(enemy, info)` | The pilot kills an enemy |
| `OverrideDashMovement`, `OnDashStart`, `OnDashEnd`, `ExtraDashIntangibility` | Dash modules |
| `OnPhasedThrough()` | A bullet passes through the dashing ship |
| `TryPreventLethal()` | A hit would destroy the ship |

Then register it in `BuildEquipment` with `Item<YourDefinition>(...)`, add `.name` and `.desc` (with placeholders) to
`en.loc`, and test it in `EquipmentTests`.

**Checklist:** definition (and effect) · `BuildEquipment` · `.name` and `.desc` · test · [equipment page](../game/equipment.md) and GDD §8

## Add a Mystery event

Add an entry to `BuildMysteryEvents` in `ContentBuilder.Events.cs`. Helpers build choices and outcomes:

```csharp
Event("Event_DriftingPod", "event.drifting-pod", "Drifting Pod",
    "An escape pod tumbles past, its beacon still blinking.",
    Gamble("Crack it open", 0.7f,
        new[] { Scraps(30) }, "Supplies, and nobody home.",
        new[] { Hull(-15) }, "It was rigged."),
    Require(Choice("Buy its coordinates from a trader", "Someone will be glad of them.", Scraps(-20), DarkMatter(15)),
        EventRequirement.Scraps, 20),
    Choice("Let it drift", "Not today.")),
```

Outcome kinds: Scraps, Hull, HullPercent, MaxHull, DarkMatter, Equipment (minimum rarity), UpgradeRandomItem,
LoseRandomItem, Ambush and StatPercent. Requirements: Scraps, AnyItem, UpgradeableItem. Then add `.name`, `.body`,
`.choice.N`, `.result.N` (and `.fail.N` for gambles) to `en.loc`.

## Add a Lab upgrade

Add an `Upgrade(...)` line in `BuildMetaUpgrades` (`ContentBuilder.Boss.cs`): currency, costs per tier, and an effect
(`StatBonus`, `StartingScraps` or `Unlock`). Add `.name` to `en.loc`. The Lab page lists it automatically.

## Add a sound

1. Add the cue **at the end** of `SoundCue`.
2. Add a recipe to `BuildAudio`: `(SoundCue.X, X, volume, minInterval, maxVoices, pitchJitter)`, and a synth function.
3. Play it: `GameAudio.Play(SoundCue.X, transform.position)`.

## Add text or a language

- **New text:** add `key = value` to `en.loc` and use `Loc.T("key", ("name", value))`. Never put player-facing text in
  code.
- **New language:** copy `en.loc` to `de.loc` (for example), set `@language = Deutsch`, translate the values. It shows
  up under Options › Language. Missing keys fall back to English.

## Add a level type

The biggest change; touch these in order:

1. `MapNodeType` and the map generator's weights and rules (Core), with EditMode tests for the rules.
2. `LevelController.Begin`: set up the objective, rewards and exit for the new type.
3. Text: `level.<type>`, `level.<type>.desc`, `glyph.<type>`, and its objective strings. The built-keys test lists
   every node type, so it will tell you what's missing.
4. The map screen colour for the node, and a PlayMode test that plays the level.
