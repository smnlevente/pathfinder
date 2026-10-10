# Project Pathfinder: New Home — Game Design Document

| | |
|---|---|
| **Version** | 2.3 (playtest 5: slower shield regeneration, a deadlier Crusher, a faster enraged Quarry Maw, secondary charges on the HUD, level arrival, a music track per galaxy) |
| **Date** | 2026-10-10 |
| **Engine** | Unity 6 (6000.0.67f1), URP |
| **Status** | Pre-production → Vertical Slice |
| **Sources** | `Project Pathfinder_ New Home.pdf` (concept), `Technical documentation_ Project Pathfinder_ New Home.pdf` (systems). This document supersedes both; see [§16 Changes from the source docs](#16-changes-from-the-source-docs). |

> **About the numbers:** every number here is a *starting value for tuning*, not a final balance. Values marked **[tune]** are expected to change during playtesting.

---

## 1. Overview

### 1.1 Pitch
A **top-down, twin-stick roguelite shooter** in neon-lit 3D. You pilot a ship through three procedurally generated galaxies, using the Pathfinder wormhole machine to search for a new home for humanity. In each galaxy you pick a route through a branching map of levels, build up your ship with equipment, and fight a boss. When you die you lose the ship, but the Pathfinder recovers what it learned, so every run makes the next one stronger.

### 1.2 Influences
| Reference | What we take from it |
|---|---|
| **Warblade** | Arcade shoot 'em up feel, enemy formations, upgrade-pickup rush |
| **Alienation / Helldivers** | Top-down 3D twin-stick camera, neon lighting, readable chaos |
| **Slay the Spire** | Branching map of typed nodes, informed route choice |
| **Hades** | Fast runs, hub between runs, persistent upgrades, dash-centric builds |
| **Returnal** | Sci-fi survival tone, bullet patterns, risk/reward pickups |
| **Darkest Dungeon** | Attrition: hull damage carries between levels and repairs are scarce |

### 1.3 Key facts
- **Genre:** Roguelite twin-stick shooter
- **Perspective:** Top-down 3D. Gameplay happens on a flat plane; the camera looks down at about 60°.
- **Platforms:** PC (Steam) first. Consoles come after launch; the game is gamepad-first in design.
- **Players:** Single-player at launch of the slice. **Online co-op for up to 4 players** is a post-slice goal. It is out of scope for now, but the architecture is planned for it from day one ([§14.4](#144-co-op-readiness)).
- **Run length:** about 30–40 minutes for a full winning run (3 galaxies)
- **Team:** Solo developer. The first goal is a vertical slice.

### 1.4 Design pillars
1. **Every choice is a trade-off.** Route, equipment, repair versus upgrade: no free lunches.
2. **Snappy, readable combat.** Tight controls, a responsive dash, and enemy bullets that are always easy to see against the neon environment.
3. **Builds that click.** Ships, weapons and equipment stack into synergies the player can feel by mid-run.
4. **Death is progress.** Every run returns something of permanent value.

---

## 2. Story & Setting

In the near future, Earth is collapsing under cascading environmental catastrophes. A small team of scientists builds the **Pathfinder**, a machine that can open stable wormholes. Humanity has a single chance to find a habitable world before Earth fails.

**Framing for the roguelite loop:** The Pathfinder sits on an orbital lab, the **Hub**. Each run, a pilot ship is sent through a wormhole into uncharted galaxies. Every ship carries a **Black Box** that the Pathfinder can pull back through the wormhole when the ship is destroyed. This explains what persists after death: recovered data and exotic materials (meta-currencies) fund new technology for the next attempt.

**The three galaxies (one run):**
| Tier | Galaxy (working name) | Theme | Boss |
|---|---|---|---|
| 1 | **The Shattered Expanse** | Asteroid fields, mining wrecks, drifting rock | *The Quarry Maw* |
| 2 | **The Graveyard Drift** | Derelict stations, debris, defense grids of a dead civilization | *The Binary Wardens* |
| 3 | **The Veil Nebula** | Living alien nebula, bioluminescent hives | *The Hive Matriarch* |
| — | **New Home** | Run victory: humanity's new planet | — |

Later in production, each tier can draw from a **pool of themes** (for example, tier 2 might roll a Derelict or an Ice Moon galaxy) to increase variety between runs.

**Factions:**
- **The Swarm:** hostile alien organisms. Their attacks are fluid and erratic, with organic bullet patterns.
- **The Remnant:** a rival human splinter group that stole early Pathfinder tech and now scavenges the same galaxies. Their attacks are precise, geometric and mechanical. *(Faction identity is an open question.)*

**Narrative delivery (post-slice):** Short scenes in the Hub between runs, plus lore fragments found in levels. There are no cutscenes in the vertical slice.

---

## 3. Core Gameplay Loop

```
┌──────────────────────── META LOOP (between runs) ────────────────────────┐
│  Hub: spend Dark Matter & Singularity Cores → unlock ships, weapons,     │
│  permanent upgrades → start run                                          │
└──────────────────────────────────┬───────────────────────────────────────┘
                                   ▼
┌──────────────────────── RUN LOOP (~30–40 min) ───────────────────────────┐
│  Select ship → select primary + secondary → for each of 3 galaxies:      │
│     ┌──────────── GALAXY LOOP (~12 min) ────────────┐                    │
│     │ Galaxy Map → pick next node → play level →    │                    │
│     │ collect reward → back to map … → Shop → Boss  │                    │
│     └───────────────────────────────────────────────┘                    │
│  Win = reach New Home. Death = run ends, keep meta-currency.             │
└──────────────────────────────────────────────────────────────────────────┘
                                   ▼
┌──────────────────────── LEVEL LOOP (~2 min) ─────────────────────────────┐
│  Arena: move, aim, shoot, dash → clear waves / objective → exit wormhole │
└──────────────────────────────────────────────────────────────────────────┘
```

### 3.1 What persists, and what doesn't
| Kept within a run (lost on death or victory) | Kept forever |
|---|---|
| Scraps | Dark Matter |
| Equipment and equipment levels | Singularity Cores |
| Current hull HP | Unlocked ships and weapons |
| Galaxy progress | Hub upgrades |
| | Lore and codex entries |

---

## 4. Controls

The game uses Unity's **Input System** package, with one action map for gameplay and one for UI.

| Action | Keyboard & Mouse | Gamepad |
|---|---|---|
| Move | WASD | Left stick |
| Aim | Mouse cursor | Right stick (aim assist: slight magnetism) |
| Fire primary | LMB (hold) | RT (hold) |
| Fire secondary | RMB | LT |
| Dash | Space | A / Cross (also RB) |
| Reload | R (also automatic when the magazine is empty) | X / Square |
| Interact | E | Y / Triangle |
| Galaxy map / pause | Tab / Esc | Select / Start |

- Twin-stick: movement and aim are fully independent, and the ship's hull rotates to face the aim direction.
- If there is no aim input on a gamepad, the ship keeps its last aim direction. *(Auto-aim at the nearest enemy is an optional accessibility setting.)*

---

## 5. Player Ship

### 5.1 Base stats
All ships derive from these values; ship traits modify them. **[tune]**

| Stat | Base | Notes |
|---|---|---|
| Hull (HP) | 100 | Does **not** regenerate. It is restored only by repairs, healing effects or Vampiric kills. |
| Shield | 25 | Regenerates at 4/s after 5 s without taking damage. Absorbs damage before hull. *(Playtest 5: at 10/s after 3 s it refilled between almost every exchange, and the ship felt indestructible.)* |
| Move speed | 8 u/s | Fast acceleration and deceleration (~0.08 s) for responsive control |
| Pickup magnet radius | 3 u | |
| Dash | 1 charge, see [§5.2](#52-dash) | |

**Damage model:** Damage hits Shield first, then Hull. At 0 Hull the ship is destroyed and the run ends. After any Hull hit the ship gets **0.3 s of invulnerability** to prevent instant multi-hit deaths *(0.5 s until playtest 4, which found it too forgiving)*.

### 5.2 Dash
Every ship has the Dash. It is the main defensive tool.

| Property | Base value **[tune]** |
|---|---|
| Distance | 5 u |
| Duration | 0.15 s |
| Charges | 1 (a Hub upgrade raises this to 2) |
| Recharge | 1.2 s per charge |
| Invulnerability | **Yes:** full i-frames for the whole dash (0.15 s). Bullets and contact damage pass through the ship. |

The dash goes in the **movement direction**. With no movement input, it goes in the aim direction.

Dash behavior is extended with **Dash Modules**, see [§8.2](#82-dash-modules).

### 5.3 Ships
Ship selection happens at the start of each run. Each ship has a **stat profile** and **one unique trait**.

| # | Ship | Profile | Unique trait | Unlock |
|---|---|---|---|---|
| 1 | **Titan-Class Cruiser** | Hull 150, Shield 25, Speed 7 | **Reinforced Hull:** +50% hull, and incoming damage is reduced by 10%. The beginner ship. | Starting |
| 2 | **Stormbringer Battleship** | Hull 100, Shield 25, Speed 8 | **Lightning Core:** each primary projectile has an 8% chance to call a lightning strike from above at the impact point (25 dmg, 2 u radius). | Starting |
| 3 | **Bot-Commander Frigate** | Hull 90, Shield 30, Speed 8.5 | **Swarm Protocol:** each kill spawns a guided bot at the corpse. The bot homes on the nearest enemy, deals 12 damage on contact and is destroyed. Max 6 bots active. | 2 Singularity Cores |
| 4 | **Arcanum Explorer** | Hull 85, Shield 25, Speed 9 | **Cargo Hold:** every equipment choice offers **+1 option** (4 instead of 3), and the Shop catalog has +1 slot. | 3 Singularity Cores |
| 5 | **Vampiric Destroyer** | Hull 110, Shield 0, Speed 8 | **Energy Siphon:** each kill restores 2 hull (elites 6, bosses 20 per phase). It takes **+25% damage** and has **no shield**. | 4 Singularity Cores |

### 5.4 Loadout selection
After choosing a ship, the player picks **one Primary** and **one Secondary** from the weapons they have unlocked. The run then starts on the Galaxy 1 map.

---

## 6. Weapons

### 6.1 Primary weapons
Primary weapons fire continuously while the trigger is held, have no cooldown, and work on a **magazine and reload** cycle. They are the main source of damage. **[tune]**

| # | Weapon | Mechanics | DMG | Fire rate | Mag | Reload | Unlock |
|---|---|---|---|---|---|---|---|
| 1 | **Photon Repeater** | Fast single bolts. The all-rounder. | 8 | 10/s | 30 | 1.0 s | Starting |
| 2 | **Plasma Cutter** | Shotgun: fires 6 pellets in a 30° cone with short range (7 u) and damage falloff. | 6×6 | 2/s | 6 | 1.2 s | Starting |
| 3 | **Quantum Charger** | Hold to charge, release to fire a piercing blast. Needs a minimum 0.3 s charge; full charge takes 1.5 s. Damage scales from 15 to 120. There is no magazine; it **overheats** if held at full charge for more than 2 s, causing a 1 s lockout. | 15–120 | — | ∞ | — | 150 DM |
| 4 | **Neutron Bomb Launcher** | Slow projectile that explodes on impact or at max range, dealing area damage in a 2.5 u radius. | 40 AoE | 1.5/s | 4 | 2.0 s | 250 DM |
| 5 | **Bot Dispatcher** | Launches homing mini-bots that steer toward the nearest enemy. | 10 | 5/s | 20 | 1.0 s | 250 DM |

### 6.2 Secondary weapons
Secondary weapons are cooldown-based burst tools that deal much more damage than the primary or offer utility. **[tune]**

| # | Weapon | Mechanics | Cooldown | Unlock |
|---|---|---|---|---|
| 1 | **Seeker Swarm** | Fires a salvo of 6 homing missiles, 20 dmg each. | 8 s | Starting |
| 2 | **Nova Pulse** | Shockwave around the ship (4 u radius): 40 dmg, **destroys enemy bullets** in range, and knocks enemies back. A defensive panic button. | 12 s | Starting |
| 3 | **Railgun** | After a 0.25 s wind-up, an instant line shot across the screen that pierces everything for 150 dmg. | 6 s | 150 DM |
| 4 | **Gravity Well** | Throws a singularity to the aim point. For 3 s it pulls enemies and enemy bullets toward it, then implodes for 80 AoE damage. | 14 s | 200 DM |
| 5 | **Arc Lance** | Channels a piercing beam in the aim direction for 1.5 s, dealing 120 dmg/s. Move speed is halved while channeling. | 10 s | 200 DM |

**Implemented in M2** **[tune]**:
- **Plasma Cutter:** the 6 pellets fan evenly across the 30° cone with a little jitter. They fly at 26 u/s, and damage falls off to 40% at the 7 u maximum range.
- **Quantum Charger:** the shot flies at 36 u/s with a 24 u range and **pierces every enemy in its path**. Its hit radius grows from 0.2 to 0.55 u, and its visual scale up to 2.2×, with charge. The ship's lights brighten as it charges and flicker when it is about to overheat.
- **Seeker Swarm:** the missiles launch in a 140° fan, fly at 14 u/s and turn at 300°/s. Each missile is assigned a different nearby enemy so the salvo doesn't overkill one target. A missile whose target dies retargets the nearest enemy.
- **Firing:** each press of the secondary button fires one salvo; holding it doesn't repeat, so a second charge (Twin Charge) needs a second press *(playtest 5)*.
- **Nova Pulse:** a shove of 14 u/s that decays quickly, pushing enemies about 1.75 u whatever their size. Heavy enemies resist knockback: the Crusher moves only 15% as far, and the bosses (the Quarry Maw and the Binary Wardens) not at all.

---

## 7. Combat & Enemies

### 7.1 Arena & camera
- A level is a **bounded arena**, about 2–3 screens in area (e.g. 60 × 40 u). The camera is top-down, follows the player smoothly with a slight lead toward the aim direction, and is clamped to the arena bounds.
- Arenas are built from **handcrafted layout templates** for each theme (walls, chokepoints, spawn zones). **Procedural placement** fills in obstacles, hazards, spawn points and pickups. Fully procedural layouts are post-slice.
- Theme hazards: drifting asteroids that deal contact damage and can be destroyed (Expanse), electrified debris and rotating laser grids (Graveyard), and spore clouds that slow and drain shield (Veil).

### 7.2 Wave director
Enemy spawns are driven by a **budget**:
- Each wave has a point budget equal to `base × galaxyMultiplier × rowMultiplier`.
- Each enemy archetype has a cost. The director buys a random mix from the theme's enemy pool until the budget is spent.
- The next wave starts when 80% of the current wave is dead, or after a timeout.
- Spawns are telegraphed with a 1 s warp-in effect, and enemies never spawn within 6 u of the player.
- Elites are normal enemies with a modifier (see below). Their chance to appear increases per row and per galaxy.

**Implemented in M2** **[tune]**:
- **Budget:** `round(6 × galaxy[1.0 / 1.4 / 1.8] × (1 + 0.15 × (row − 1)) × (1 + 0.25 × (wave − 1)))`. Each pick is weighted among the enemies that are unlocked and still affordable (Shattered Expanse pool: Drone 5, Gunner 3, Spinner 2 from wave 2, Lancer 2 from wave 2, Carrier 1 from wave 4). A wave holds at most 40 spawns.
- **Galaxy gating:** each pool entry has a first galaxy. From galaxy 2 the pool adds the Scorcher (weight 2), Arc Drone (3) and Crusher (1.5, from wave 2). The endless combat sandbox (galaxy 1) brings them in from wave 6, so they can be tried there.
- **Elite chance:** `min(0.35, 0.03 + 0.02 × (wave − 1) + 0.04 × (row − 1) + 0.05 × galaxy)`. An elite costs ×2 budget.
- **Pacing:** the first wave comes after 1.5 s. The next wave starts at 80% dead or after 25 s. Levels have a fixed wave count; the combat sandbox is endless.
- The director's randomness is a seeded stream keyed by galaxy and row, and every enemy gets its own AI and loot streams keyed by spawn order. Seeded replays therefore match exactly.

### 7.3 Enemy archetypes
Archetypes are defined by their behavior. Each faction or theme re-skins and slightly re-tunes them. **[tune]**

| Archetype | Behavior | HP | Cost |
|---|---|---|---|
| **Drone** | Chases the player and deals contact damage. Comes in swarms. | 15 | 1 |
| **Gunner** | Keeps 8–12 u away and fires aimed 3-shot bursts. | 35 | 2 |
| **Spinner** | Slow; fires radial bullet rings in rotating patterns. | 60 | 3 |
| **Lancer** | Shows its path as a lane of chevrons, then jousts twice in straight lines at high speed. | 60 | 3 |
| **Carrier** | Slow tank that spawns Drones periodically. Weak point at the rear. | 150 | 6 |
| **Scorcher** *(galaxy 2+)* | Flamethrower grunt: closes in, marks a cone, sprays fire across it. | 45 | 3 |
| **Arc Drone** *(galaxy 2+)* | Hovers at mid range; locks a line onto the ship, then zaps along it instantly. | 20 | 2 |
| **Crusher** *(galaxy 2+)* | Slow, heavy and knockback-proof; lunges and slams the ground for huge damage if it reaches you. | 110 | 4 |
| **Turret** | Static; its sweeping laser or bullet stream is placed by the level template. | 80 | 2 |
| **Shielder** | Projects a damage-absorbing shield over nearby allies. | 50 | 4 |

**Elite modifiers:** *Armored* (+100% HP, slower), *Hasty* (+50% speed and fire rate), *Splitting* (splits into 2 smaller copies on death), *Volatile* (explodes on death and leaves a bullet ring).

**Implemented (M1–M2)** **[tune]**:
- **Drone:** 5.5 u/s, weaves while closing in. Contact damage 10, then it bounces off with a 0.75 s contact cooldown.
- **Gunner:** 4 u/s. Contact damage 10. Every 2 s (±0.3 s) it glows for 0.45 s as a warning, then fires a 3-shot burst (0.15 s apart) of 8-damage bullets at 11 u/s, aimed at the target's current position. It moves at 30% speed while attacking.
- **Spinner:** 2 u/s, holds 6–13 u and drifts sideways. Every 1.8 s its emitters flare for 0.35 s, then it fires a ring of 10 bullets (8 damage, 6 u/s). Each ring is rotated 11° from the last, so the gaps move.
- **Lancer:** 60 HP, 4.5 u/s. Within 9 u it plants itself and shows a **chevron lane** along its locked direction for 0.7 s. The chevrons light up from the Lancer outward as the wind-up completes, then all flash just before launch. It then charges 14 u at 24 u/s, hitting for 30 (2.5× its 12 contact damage). The charge ends early if it hits the ship or a wall. **Every charge ends in a 0.6 s wind-down:** it skids to a halt (about 5 u) before it can do anything else. **A miss earns a second joust:** after the wind-down it re-aims at the ship and charges again after a 0.6 s wind-up. After its last charge it recovers for 1 s at 30% speed and waits 1.8 s before the next attack. Wind-down and recovery are its vulnerable windows. *(Playtest 2: the old red line looked like a bullet, and the Lancer felt weak. Playtest 3: it re-aimed the instant a charge ended, which felt clunky.)*
- **Carrier:** 1.8 u/s, keeps about 10 u away and turns at only 60°/s, so it can be flanked. Its armoured front takes 60% damage and its yellow rear weak point takes 200%. Every 4 s its flank bays flare and it launches 2 Drones from the rear, with at most 6 alive. Launched Drones drop no Scraps, so Carriers can't be farmed.
- **Scorcher** *(galaxy 2)*: 45 HP, 3.2 u/s. Within 4.5 u it plants itself for **0.6 s** and an orange **cone** (±18°, 5.6 u) fills in on the floor. Then it sprays for **1.4 s**: a flame every 0.06 s (4 damage, 9 u/s, 5 u range) that billows to 2.4× its size, while it turns after the ship at 60°/s and creeps at 25% speed. Flames wash over the ship instead of stopping at it (each can burn it once), so post-hit invulnerability paces the damage at about 13/s for a pilot who stays in the fire. Then a 2.2 s cooldown at half speed. Flames are a hazard, not bullets: alpha-blended orange, and silent (the Scorcher plays one roar per spray).
- **Arc Drone** *(galaxy 2)*: 20 HP, 5 u/s, strafes at 6–8.5 u. Within 11 u it **locks onto where the ship is** and charges for **0.7 s**, showing a flickering red-orange line through that spot to its full 12 u reach (or the wall). Then it discharges instantly along the locked line: **14 damage** to the first ship within 0.3 u of it, with a jagged white-and-orange bolt. Step off the line, or dash through it (dashing ships phase through, and Phase Dash counts it). 2.5 s cooldown.
- **Crusher** *(galaxy 2)*: 110 HP, 2.8 u/s, and only **15% knockback**. Within 3.2 u it rears up for **0.4 s** behind a red ring at its 3.3 u reach, with a fill growing out to it, and **lunges at the ship at 3.5 u/s**. Then it **slams: 40 damage** to every ship within the ring, a shockwave and a camera kick. It recovers, motionless, for 1 s: the time to hurt it. A pilot who moves away the moment the ring appears gets clear; one who reacts late has to dash. *(Playtest 5: the first version, 0.55 s and creeping in, was too easy to walk away from.)*
- **Elites:** *Armored* has ×2 HP and ×0.75 speed. *Hasty* has ×1.5 speed and ×1.5 attack rate (cooldowns, wind-ups and bursts). Both are ×1.2 larger, carry a gold ring marker and always drop ×3 Scraps. *Splitting* and *Volatile* come post-slice.
- **Spawns** are telegraphed for 1 s by a red glow on the floor (staggered by 0.08 s within a wave) and are never within 6 u of a player.

**Scaling per galaxy:** enemy HP ×1.0 / ×1.6 / ×2.4, wave budget ×1.0 / ×1.4 / ×1.8. **[tune]**

### 7.4 Drops
- Every enemy has a chance to drop **Scraps** (Drone 1, Gunner 2, Carrier 8, elites ×3).
- Scraps are pulled into the ship by the magnet radius and despawn after 10 s if not collected.
- **Implemented in M2** **[tune]**: drop chances are Drone 50%, Gunner 75%, Spinner 3 at 85%, Lancer 2 at 80%, Carrier 100%, and elites always drop. Galaxy 2 adds Scorcher 2 at 80%, Arc Drone 1 at 70%, Crusher 4 at 90%. A drop bursts into up to 12 gold pieces that settle on the floor. Within the 3 u magnet radius they accelerate into the ship (up to 24 u/s). They blink for their last 2 s. Collection is decided by the authority, and Scraps are counted per pilot.

### 7.5 Bosses
Each boss has three phases with a separate health bar segment for each. A boss fight lasts about 3–4 minutes.

| Boss | Galaxy | Concept | Phases |
|---|---|---|---|
| **The Quarry Maw** | 1 | A colossal rock-worm that tunnels through asteroid debris | 1: burrows and erupts, leaving rock showers · 2: segmented body sweeps across the arena, with weak points on its segments · 3: enrages and spews bullet streams from its mouth |
| **The Binary Wardens** | 2 | The twin defense cores of a derelict station, one AI split in two | Not phased: two cores circle the arena joined by a damaging tether. A destroyed core only goes offline and reboots, unless its twin falls while it is down: destroy them together |
| **The Hive Matriarch** | 3 | An alien queen, half-fused with the nebula | 1: spawns Swarm waves and spore clouds · 2: charges and erupts in bullet hell · 3: arena-wide pattern that requires dash timing; her final cry triggers the ending |

**Boss rewards:** Singularity Cores (1 / 2 / 3 per galaxy), Hull restored by 30%, and a **guaranteed Rare-or-better equipment choice**.

**The Quarry Maw** **[tune]** *(M5, reworked after playtest 2: the phases were underwhelming and the weak points didn't read as weak points; re-paced after playtest 3: it vanished too fast, moved too fast, and its body hid the weak points behind its head)*:
- **Body:** a head plus **12 segments** that trail it 1.1 u apart, all sharing one **5500 HP** pool. It moves at 4.5 u/s and can't be knocked back. **Its shell is armour:** shots that hit the head or an armoured segment are stopped with a grey spark and deal no damage. Every 3rd segment carries a **weak point**, a pulsing yellow crystal ringed in yellow. **Its maw opens** (a big yellow crystal on its head) while it dives, emerges and rests between sweeps, and all through the enrage: then the head takes full damage too. Otherwise weak points are the only place shots hurt. Their hit circle is 30% larger than a segment, and a shot that clips the shell within 0.8 u of a weak point counts as hitting it. Seekers, Ricochet and Lightning Core skip armour and find the weak points. Every part deals 15 contact damage.
- **Phase changes** happen at 2/3 and 1/3 health. The boss bar shows these marks. Each phase opens with a 1.5 s **roar**, during which even the weak points are shut.
- **Phase 1, Burrow:** surfaced for 5 s, it **slithers around the ship side-on** at about 8 u, so the weak points on its flank face the guns, spitting a 3-bullet aimed burst every 1.6 s. Then it **dives over 1.4 s**: it stops, faces the ship, opens its maw and visibly sinks into the ground with a shudder and a burst of dust, shootable all the way down. Underground it is intangible. A red glow tracks the ship for 1.1 s, then locks. After 1.8 s it erupts there, dealing 30 damage within 3 u and throwing **two rings of 16 rocks** offset by half a step (6 u/s, then 3.8 u/s), and **rises over 1.2 s**, maw open. A phase change waits until it is back on the surface.
- **Phase 2, Sweep:** volleys of **2 charges**, each **from wherever the head is, through the ship's current position**, to the far side of the arena. It never teleports. Each charge is shown first by a large chevron lane (1.4 s for the first, 1 s for the second). It charges at 13 u/s with 1.6× contact damage and **sheds a rock to each side every 0.18 s**, leaving expanding walls of bullets (3.5 u/s) to slip through or dash across. After a volley it **rests for 4.5 s, maw open**, coiling slowly around the ship side-on: the big window to hurt it.
- **Phase 3, Enrage:** the maw stays open. It circles the ship at 9 u at **1.6× its speed** (7.2 u/s, close to the ship's own 8) *(playtest 5)*, firing a 5-bullet stream (36° spread, 10 damage, 9 u/s) every 0.6 s, with a 16-rock shower every 5 s. Every 8 s it stops, flares for 0.6 s, and spins a **3-arm spiral** for 2.4 s (140°/s, a bullet per arm every 0.1 s at 6 u/s).
- **Death:** the killing blow can land on any weak point. The body shatters and drops 30 Scraps. **Every enemy bullet still in the air turns harmless, slows and fades out over 0.5 s**, so a stray bullet can't kill a pilot who just won. The reward choice opens 1.5 s later, once the death has played out. Once it is made, the wormhole **pulls the ship in**, spinning and invulnerable, with a mild camera shake: no need to fly there *(playtest 4)*.
- **Balance check:** a perfect-aim, god-mode bot with the default loadout kills it in about 95 s (a PlayMode test logs the time; 65 s before playtest 5 sped up its enrage). Real fights, with dodging and misses, should take about 2 minutes.

**The Binary Wardens** **[tune]** *(galaxy 2; the playtest-4 idea of a boss in two parts that must die together)*:
- **Split:** the boss arrives as one core and splits in two, **Alpha** and **Beta**, which fly apart to opposite sides of the arena centre. They are armoured for the 2 s split. Each core has **2200 HP** (×1.6 in galaxy 2, so 3520) and its own health bar; the Alpha is the level's boss and spawns the Beta. Alpha has a ring of six spinning emitters, Beta a cross of blades, so they read apart.
- **Orbit and tether:** the cores circle the centre on opposite sides at 22°/s, the orbit breathing between 5.5 and 8.5 u over 16 s. A crackling **tether** joins them: it always runs through the arena centre and sweeps round like a clock hand, dealing 15 damage to a ship it crosses. Whenever a link forms (after the split or a reboot) the tether flickers thin and harmless for 1.2 s first.
- **Attacks:** each core flares for 0.4 s before a volley. **Alpha** fires a ring of 14 bullets every 2.6 s, each ring turned 9° from the last. **Beta** fires a 5-bullet, 36° fan at the ship every 2 s. Bullets deal 10. Touching a core deals 15.
- **Offline:** a core brought to zero **while its twin is up only goes offline**. It sinks part-way into the floor, goes dark, is armoured (grey sparks) and harmless to touch, and a grey ring fills on the floor as it **reboots in 5 s at 50% health**. Meanwhile the tether is down and its twin **enrages** (attacks 1.5× faster). The HUD shows the downed bar with a countdown.
- **Defeat:** if the second core goes down **while the first is still offline**, the link breaks and **both fall together**. So the pilot has to wear both down evenly and finish them within 5 s of each other. The fight opens with the hint "A lone core only reboots: wear both down, then destroy them together". Death plays out like the Quarry Maw's: bullets defuse, 30 Scraps drop, and the reward follows.
- **Balance check:** a god-mode bot in the middle of the arena that always shoots the core with more health left (so both wear down together) kills them in about 50 s at galaxy 1 health, about 80 s at galaxy 2's. Real fights also lose time to reboots.
- **Galaxy 3** brings back the Quarry Maw at galaxy 3 health (×2.4) until the Hive Matriarch exists. Each galaxy's boss is set in the run config.

---

## 8. Equipment (In-Run Build System)

Equipment is the core of a build. It is found in Equipment levels, Mystery levels, after bosses, and in the Shop. Equipment is lost at the end of a run.

> This system unifies the source docs' "perks and abilities", "upgrades" and "Dash equipment" into one slot-based system.

### 8.1 Structure
- **Slots:**

  | Category | Slots | Affects |
  |---|---|---|
  | **Dash Module** | 1 (Hub upgrade: 2) | Changes how the dash behaves |
  | **Weapon Mod** | 2 | Primary weapon behavior |
  | **Secondary Mod** | 1 | Secondary weapon behavior |
  | **Hull System** | 3 | Passive stats and effects on the ship |

- **Rarity:** Common / Rare / Epic. Rarity sets the base strength and how often an item appears.
- **Level:** I → II → III. Levels are upgraded in the Shop with Scraps, and each level strengthens the item's effect.
- **Picking up equipment when its category's slots are full:** the player must **swap** an equipped item out (the old item is lost) or skip the new one.
- **Duplicates:** picking an item you already have raises its level by +1 for free.

### 8.2 Dash modules
These come from the source doc, with clarifications.

| Module | Effect | Level scaling |
|---|---|---|
| **Phase Dash** *(was "Immunity During Dash")* | The base dash already has i-frames, so this module **extends** them past the dash end. Each enemy bullet the ship phases through restores 2 shield. | Extra i-frames 0.2 / 0.3 / 0.4 s |
| **Blink Dash** | Teleports instantly instead of travelling, and passes through walls and enemies. | Distance +0 / +1 / +2 u |
| **Decoy Drone** | Leaves a decoy (40 HP) at the dash start point. Enemies within 8 u target it until it is destroyed. | Decoy HP 40 / 70 / 100 |
| **Bomb Dash** | Leaves a bomb at the dash start that explodes after 1 s for 60 AoE damage. **It also damages the player** (at 25%) if they are within the radius. A risk/reward module. | Damage 60 / 90 / 120 |
| **Pushback Dash** | Shockwave at the dash end that knocks back enemies within 3 u and deals 10 damage. | Radius 3 / 3.5 / 4 u, damage 10 / 20 / 30 |
| **Bullet Dash** | At the dash end, fires a ring of 8 primary projectiles. The projectiles take on the equipped primary's properties, e.g. the Neutron Bomb Launcher fires bombs. | Projectiles 8 / 12 / 16 |
| **Shield Dash** | At the dash end, grants a 1 s barrier that **reflects** enemy bullets. | Duration 1 / 1.5 / 2 s |
| **Boost Dash** | +40% move speed for 2 s after dashing. | 2 / 3 / 4 s |

### 8.3 Weapon mods (initial pool)
| Mod | Effect |
|---|---|
| **Piercing Rounds** | Projectiles pierce +1 enemy (scales +1 per level). |
| **Ricochet** | Projectiles bounce to a nearby enemy. |
| **Split Shot** | Every 4th shot fires 3 projectiles in a fan. |
| **Overclock** | +15% fire rate per level. |
| **Extended Mag** | +30% magazine size per level, and +10% damage on the first bullet of each magazine. |
| **Incendiary** | Hits apply a burn of 3 dmg/s for 3 s that stacks. |

### 8.4 Secondary mods (initial pool)
| Mod | Effect |
|---|---|
| **Rapid Cycler** | −15% secondary cooldown per level. |
| **Twin Charge** | The secondary holds 2 charges. |
| **Kill Refund** | Kills reduce the remaining secondary cooldown by 0.3 s. |
| **Amplifier** | +25% secondary damage per level. |

### 8.5 Hull systems (initial pool)
| System | Effect |
|---|---|
| **Armor Plating** | +20 max hull per level. |
| **Capacitor** | +15 shield, and shield regen starts 0.5 s sooner. |
| **Salvage Magnet** | +2 u magnet radius and +15% Scraps. |
| **Critical Optics** | 10% crit chance (×2 damage), +5% per level. |
| **Adrenal Thrusters** | +10% move speed per level. |
| **Last Stand** | Once per level, a hit that would destroy the ship instead leaves it at 1 hull. |

*Synergy examples:* Stormbringer + Overclock (more lightning procs) · Bot-Commander + Bot Dispatcher + Kill Refund (a self-feeding swarm) · Vampiric + Bomb Dash + Pushback Dash (aggressive melee brawler).

### 8.6 Implementation notes (M4)
- **Stats:** every stat is `(base + Σ flat) × (1 + Σ percent)`, floored at zero. Percentages from different sources add together. Ship definitions provide the bases. Equipment, traits and run effects (Reinforce) add modifiers.
- **Rebuild on change:** whenever the loadout changes, the ship rebuilds its stats and item effects from scratch: trait, then each equipped item at its level, then run modifiers. Max hull and shield gains heal by the same amount. The primary weapon reloads to full.
- **Hooks:** effects react to *shot fired*, *primary hit*, *kill*, *dash start/end*, *bullet phased through*, *lethal hit* and *level start*. On-hit effects trigger only for **primary projectile** hits, so procs (lightning, ricochets) and burns can never chain off each other.
- **Crits** (Critical Optics) deal ×2 and are rolled when the shot is fired, from the pilot's weapon stream, so hit order can't change outcomes.
- **20 items are implemented:**
  - **Dash Modules:** Phase (Rare), Blink (Rare), Pushback (Common), Bullet (Epic).
  - **Weapon Mods:** Overclock (Rare), Extended Mag (Common), Piercing Rounds (Rare), Ricochet (Rare), Split Shot (Common), Incendiary (Rare).
  - **Secondary Mods:** Rapid Cycler (Common), Twin Charge (Epic), Kill Refund (Rare), Amplifier (Common).
  - **Hull Systems:** Armor Plating, Capacitor, Salvage Magnet and Adrenal Thrusters (all Common), Critical Optics (Rare), Last Stand (Epic).

  Decoy and Bomb Dash come post-slice.
- **Level scaling where the tables above don't say:**
  - Ricochet bounces deal 50/75/100% damage.
  - Split Shot triggers every 4th/3rd/2nd shot.
  - Incendiary burns for 3/4/5 damage per second, capped at **5 stacks** per enemy, since a 10/s weapon would otherwise stack 30.
  - Kill Refund returns 0.3/0.45/0.6 s per kill.
  - Last Stand grants a 0.5/1/1.5 s grace after the save.
  - Twin Charge adds +1 charge at every level, plus 0/−10/−20% cooldown.
- **Phase Dash** keeps the ship *intangible* for the extra time, so bullets pass through and restore shield.
- **Offers:** choices roll a rarity per slot from 70/25/5 (galaxy 1), 55/33/12 (galaxy 2) or 40/40/20 (galaxy 3), then pick an item of that rarity. Picks never repeat within an offer, and items already at level III are never offered. Mystery caches and boss rewards guarantee Rare or better.
- **Traits:** *Reinforced Hull* (Titan) is pure stats: 150 hull, speed 7, ×0.9 damage taken. *Lightning Core* (Stormbringer) is an always-on effect. Ships also have an `ExtraEquipmentChoices` stat for the Arcanum Explorer.
- **Hub loadout:** Start Run lets the player pick a ship, primary and secondary. Choices are stored as content IDs and resolved through a `ContentDatabase`. Since M5, content locked by a meta unlock (the Quantum Charger) is skipped until it is bought ([§11.3](#113-implementation-notes-m5)).

---

## 9. Run Structure: Galaxy Map

### 9.1 Shape
Each galaxy is a **reversed tree**: several entry nodes at the base converge upward toward a single Shop node and then the Boss. It is displayed as an interactive UI map (Slay the Spire-style, vertical).

```
                 [BOSS]          Row 6
                   │
                 [SHOP]          Row 5   (single node, all paths converge)
              ╱    │    ╲
           [?]   [R]    [E]      Row 4
           │ ╲  ╱  │     │
          [S]  [E] [DM]  [S]     Row 3
           │  ╱    │   ╲ │
          [E] [S]  [?]  [DM]     Row 2
           │  │  ╲ │    │
          [S] [E]  [DM]          Row 1   (2–4 entry nodes; the player picks one)
```
`S` = Scraps · `E` = Equipment · `DM` = Dark Matter · `R` = Repair · `?` = Mystery

### 9.2 Generation rules
- **Rows:** 4 playable rows, then the Shop (row 5), then the Boss (row 6). A path through one galaxy is therefore **4 levels + Shop + Boss**.
- **Width:** 2–4 nodes per row.
- **Edges:** Each node connects to 1–2 nodes in the next row: its "lane" neighbours. Edges never cross.
- **Bridges:** About 15% of nodes get an extra diagonal edge to a neighbouring lane. These are the "side branches" from the source doc that let the player switch paths mid-galaxy.
- **Reachability:** Every node can be reached from at least one entry node, and every node leads to the Shop.
- **Seeded:** Each run has a seed, and all map and level generation derives from it ([§14.3](#143-randomness)). This makes runs reproducible for debugging, allows seeded runs later, and lets co-op clients generate identical maps.

**Node type rules:**
| Type | Rule |
|---|---|
| **Equipment** | At least one in row 1. Weighted 35%. |
| **Scraps** | Weighted 30%. |
| **Dark Matter** | Weighted 20%. Never in row 1. |
| **Mystery** | Weighted 15%, max 2 per galaxy, never in row 1. |
| **Repair** | **Max 1 per galaxy, 60% chance to exist at all.** Only in rows 3–4 (never in the first two rows). |
| **Shop** | Always the single node in row 5. |
| **Boss** | Always the single node in row 6. |

- No path may contain three consecutive nodes of the same type.
- Node types are **visible on the map** (except for Mystery), so route planning is informed.

**Implemented in M3:**
- **Edges** are built row by row with a left-to-right "staircase" sweep. Both lane indices only ever increase, so edges never cross, and every node gets at least one edge in each direction. That guarantees reachability and that every path reaches the Shop. A node can have up to 3 outgoing edges when a narrow row feeds a wide one.
- **Bridges** add an extra diagonal edge to the next lane over, but only when it crosses nothing.
- **Types** are assigned row by row from the weights. A type is excluded for a node if a parent and grandparent on some path already share it. If row 1 rolls no Equipment, one of its nodes is converted. The single Repair node (60% chance) then replaces a random node in rows 3–4.
- **Seeds:** galaxy *n*'s map comes from `seed → "map" → n`. Level content comes from `seed → "level" → galaxy → node`, so a node plays the same whatever route led to it. Mystery outcomes are seeded per node too.
- 500-seed EditMode tests check every rule above.

---

## 10. Level Types

Every level ends by flying into the **exit wormhole**, which opens once the level's objective is complete. Unless noted otherwise, combat levels last about 2 minutes.

### 10.1 Scraps Level ⚙
- **Goal:** Fill the war chest for the Shop.
- **Objective:** Survive and clear **4 waves** with a higher budget than normal (×1.3) and more Scraps drops (×2).
- **Completion bonus:** +30 / 45 / 60 Scraps (by galaxy).
- **Flow:** Enter → clear the waves while collecting Scraps → the bonus is awarded → the exit opens.

### 10.2 Equipment Level ⬢
- **Goal:** Grow the build.
- **Objective:** Clear **3 waves**. The final wave always contains an **elite Cache Carrier** that drops an Equipment Cache when destroyed.
- **Reward:** Opening the cache offers a **choice of 3 random equipment items** (4 with the Arcanum Explorer). The player picks one or skips. Skipping grants 15 Scraps.
- **Rarity odds:** 70% Common / 25% Rare / 5% Epic, with Rare and Epic odds improving each galaxy.

### 10.3 Dark Matter Level ◆ *(the "Persistent Currency Reward" level)*
- **Goal:** Invest in meta progression instead of power for the current run.
- **Objective:** A larger arena with **8–12 Dark Matter nodes**. Each node takes 1 s of standing within range to harvest. Enemy pressure is light but constant.
- **Wormhole Instability:** a **90 s timer**. The exit is available from the start, and the player can leave whenever they like. When the timer expires, enemy spawns escalate sharply. This is a greed-versus-safety decision.
- **Reward:** about 5 DM per node (×1.5 in Galaxy 2, ×2 in Galaxy 3), plus a small amount of Scraps from kills.

### 10.4 Repair Level ✚
- **Goal:** Recover.
- **No enemies.** The level contains a **Repair Station**. The player picks **one** of:
  - **Repair:** restore 40% of max hull.
  - **Reinforce:** +15 max hull, permanently for this run, with no healing.
- At most one per galaxy. It may not exist at all (see [§9.2](#92-generation-rules)).

### 10.5 Mystery Level ?
- Shown as "?" on the map. **About 70% of Mystery nodes are events** (below). The rest are revealed as Scraps, Equipment, Dark Matter or Repair. A Mystery is never a Shop or Boss.
- **Events** *(added after playtest 2)*: a short scene with two or three choices, each trading something for something else, in the spirit of Slay the Spire's events. Each choice spells out its effects before you pick: gains in green, costs in red, and a gamble's odds. After the choice, the result is shown and the run moves on to the map; an event that ends in a fight opens the exit when the fight is won.
  - Possible effects: gain or lose Scraps, repair or lose hull, raise or lower max hull, Dark Matter, an equipment choice (sometimes Rare or better), upgrade a random item, lose a random item, a run-long stat change, or an ambush fight with double rewards.
  - Rules: event costs never kill (hull stops at 1); unaffordable choices are shown but disabled, with the reason; an event isn't repeated within a run until all have been seen.
  - The slice has 10 events: Derelict Freighter, Distress Beacon, Unstable Singularity, Wandering Engineer, Spore Bloom, Remnant Toll Gate, Sealed Weapons Cache, Echo of Earth, Scrap Trader, Gravity Well.
- **Revealed levels' rewards are ×1.5** compared to a normal level of the same type. A Mystery Equipment level gives a guaranteed Rare-or-better choice.
- A Mystery level can turn into a Repair level only if it is in row 3 or later and the galaxy doesn't already have one.
- A revealed combat level has a 10% chance to be an **Ambush**: a harder combat variant with an elite wave and double rewards.

### 10.6 Shop Level $
- **Always the node directly before the Boss.** There are no enemies.
- **Services:**
  | Service | Cost **[tune]** |
  |---|---|
  | Buy equipment (catalog of 4 items, 5 with the Arcanum Explorer) | Common 40 / Rare 75 / Epic 120 Scraps |
  | Upgrade equipped item (I→II, II→III) | 35 / 60 Scraps |
  | Repair hull | 1 Scrap per 1 hull |
  | Reroll catalog | 15 Scraps, +10 for each reroll after that |
- **Flow:** Enter → browse and buy → exit to the Boss.

### 10.7 Boss Level ☠
- The final node of each galaxy. See [§7.5](#75-bosses) for the bosses.
- **On victory:** Singularity Cores, a 30% hull repair and an equipment choice, then a **wormhole jump to the next galaxy** in the same run. The player starts at the base of a new galaxy map.
- **Defeating the Galaxy 3 boss** wins the run: New Home is reached, the ending plays (the first time; it is shorter on later wins), and the player returns to the Hub.

### 10.8 Implementation notes (M3)
- **Arena flow:** every level reuses the same arena. The ship enters at the bottom (0, −14), arriving with a cyan wormhole flash and sound while the camera cuts straight to it *(playtest 5: the camera used to slide down from the last exit)*. Once the objective is met, a cyan **exit wormhole** opens at the top (0, 14), and flying into it ends the level. Leaving clears the arena: remaining enemies vanish without loot, and leftover Scraps are collected automatically. **When a fight is won, every Scrap still on the floor flies to the ship** at once (it was the pilot's anyway; this only saves a lap of the arena) *(playtest 4)*.
- **Hull carries over** between levels. The shield refills naturally between fights.
- **Scraps level:** 4 waves at ×1.3 budget with ×2 Scraps drops. The bonus is paid to every living pilot.
- **Equipment level:** 3 waves. When the final wave starts, an **Armored elite Gunner** joins as the Cache Carrier. Its cache opens the **choice of 3** (skipping pays 15 Scraps). The waves must also be cleared before the exit opens.
- **Dark Matter level:** 8–12 indigo crystals, at least 5 u apart and away from the entry and exit. Standing within 1.6 u fills a harvest ring (1 s; it drains at half speed when left). Light pressure means a wave every 12 s at ×0.5 budget. After the 90 s timer it escalates to a wave every 5 s at ×1.5 budget. The exit is open from the start.
- **Repair level:** a green station at the arena centre. Press Interact for the choice; it can be used once. The exit is open from the start.
- **Shop level:** a gold terminal with a **4-item catalog** (Common 40, Rare 75, Epic 120). It sells upgrades for equipped items (35 for I→II, 60 for II→III), hull repairs (1 Scrap per hull), and rerolls (15, then +10 each). Buying into a full category opens the swap screen, and the pilot pays only if the swap is confirmed.
- **Mystery:** the HUD announces the revealed type, and rewards are ×1.5. An ambush has a ×1.5 budget and ×2 rewards. An event is a choice dialog in the empty arena; an event ambush is 3 waves at the ambush budget, and beating it pays the Scraps completion bonus at ×2.
- **Boss level:** **the Quarry Maw** ([§7.5](#75-bosses)) with no escorts. In M3–M4 it was a placeholder elite Carrier. It pays Singularity Cores, a 30% heal and a **Rare-or-better equipment choice**. Galaxy 2 is guarded by **the Binary Wardens**; galaxy 3 reuses the Maw at its health multiplier until the Hive Matriarch arrives. Each galaxy's boss is listed in the run config.
- **Galaxy scaling:** enemy health ×1.0 / ×1.6 / ×2.4 per galaxy, on top of the director's galaxy budget multiplier. All three galaxies use the Shattered Expanse arena and enemy pool until their themes arrive; from galaxy 2 the pool adds the Scorcher, Arc Drone and Crusher.

---

## 11. Economy & Meta Progression

### 11.1 Currencies
| Currency | Scope | Sources | Spent on |
|---|---|---|---|
| **Scraps** | Run only | Enemy drops, Scraps levels, skipped picks | The Shop: equipment, upgrades, repairs, rerolls |
| **Dark Matter (DM)** | Permanent | Dark Matter levels (main source), plus 10% of the Scraps collected in a run, converted when the run ends | Hub upgrades, weapon unlocks |
| **Singularity Cores** | Permanent | Bosses only (1 / 2 / 3) | Ship unlocks, major Hub upgrades |

*Expected income in an average run that dies in Galaxy 2:* about 80–120 DM and 1 Core. **[tune]**

### 11.2 The Hub (Pathfinder Lab)
The Hub is a small menu-driven space for the slice. Later it can become a walkable ship interior.

**Lab Upgrades (Dark Matter)** form a linear track with tiers. **[tune]**
| Upgrade | Tiers | Effect per tier | Cost per tier |
|---|---|---|---|
| Hull Plating | 5 | +10 max hull | 30 / 60 / 90 / 120 / 150 |
| Shield Matrix | 3 | +10 shield | 50 / 100 / 150 |
| Salvage Protocols | 3 | Start each run with +25 Scraps | 40 / 80 / 120 |
| Reroll Credits | 2 | +1 free equipment-choice reroll per run | 75 / 150 |
| Field Repairs | 3 | Restore 10 hull at the end of each galaxy | 60 / 120 / 180 |
| Weapon Unlocks | — | See the weapon tables | 150–250 |

**Core Projects (Singularity Cores):**
| Project | Cost |
|---|---|
| Unlock Bot-Commander / Arcanum / Vampiric | 2 / 3 / 4 |
| Second Dash charge | 2 |
| Second Dash Module slot | 3 |
| Epic Research: Epic items appear more often | 3 |
| *(Post-launch)* **Pathfinder Protocols:** optional difficulty modifiers for bonus rewards, similar to Hades' Heat | — |

### 11.3 Implementation notes (M5)
- **Banking:** when a run ends (victory or defeat), the Dark Matter and Cores earned are banked, plus **10% of all Scraps collected** in the run, rounded down. Spent Scraps count; starting Scraps from Salvage Protocols don't, so the upgrade can't pay for itself. The run summary lists what was banked. The profile also tracks runs played, runs won and the best galaxy reached.
- **Slice upgrades** are data assets (`MetaUpgradeDefinition`), each with a currency, a cost per tier and one effect: a stat bonus, starting Scraps, or a content unlock.

  | Upgrade | Currency | Tiers and costs | Effect |
  |---|---|---|---|
  | Hull Plating | DM | 30 / 60 / 90 / 120 / 150 | +10 max hull per tier |
  | Shield Matrix | DM | 50 / 100 / 150 | +10 max shield per tier |
  | Salvage Protocols | DM | 40 / 80 / 120 | +25 starting Scraps per tier |
  | Second Dash Charge | Cores | 2 | +1 dash charge |
  | Quantum Charger | DM | 150 | Unlocks the weapon in the loadout |

  Stat bonuses are applied as run modifiers on the pilot when the run starts, so they stack with ship traits and equipment through the normal stat formula ([§8.6](#86-implementation-notes-m4)).
- **The Hub Lab** is a page in the Hub menu. It lists each upgrade with its tier, its next cost and what it does, and buys with Confirm.
- **Save file:** `meta.json` in `Application.persistentDataPath`, with a format version. It is written through a temp file that replaces the old save, after every banking and every purchase. If the file is corrupt, it is copied to `meta.json.corrupt` and a fresh profile starts, so a bad file never blocks play. Upgrades are saved by content ID, so new upgrades don't break old saves.
- **Co-op note:** the profile belongs to the local player, and run bonuses are applied per pilot. For now the run banks every pilot's Scraps into the one local profile; co-op will bank each player's share into their own profile.


### 11.4 Balance check (M6)
A PlayMode probe has a god-mode bot with perfect aim play real levels and log what they pay **[tune]**. Galaxy 1, row 3:

| Level | Scraps | Dark Matter | Bot time |
|---|---|---|---|
| Scraps | 86 (incl. the 30 completion bonus) | — | 27 s |
| Equipment | 35 (incl. 15 for skipping the cache) | — | 20 s |
| Dark Matter | 9 | 50 (all 12 nodes) | 23 s |

- **Scraps at the Shop:** a galaxy has 4 playable rows before the Shop, so a typical path (one Scraps, one Equipment, one Dark Matter, one Mystery) arrives with about **120–150 Scraps**: a Rare plus a Common, or one Epic, or an upgrade and a repair. That's a real decision, not a shopping spree. No price changes.
- **Dark Matter per run:** about 120–150 for a run that reaches galaxy 2 (two Dark Matter levels plus 10% of Scraps), against about 1,140 to finish the slice's Lab, so roughly **8 runs** of progression. Slightly above the 80–120 estimate in §11.1; kept until real playtests say otherwise.
- **Length:** the bot clears a level in 20–30 s. Players, dodging and missing, take about a minute, so a galaxy of 6 levels is roughly 8–10 minutes and a full run sits near the 30–40 minute target. The Quarry Maw takes the bot about 95 s (§7.5).

---

## 12. UI / UX

### 12.1 Screen flow
```
Boot → Main Menu → Hub ──► Ship Select → Loadout Select → Galaxy Map ⇄ Level
                     ▲                                        │
                     └──── Run Summary ◄── Death / Victory ◄──┘
```
*M5 state:* Boot → Hub (menu: **Start Run** → loadout page, **Pathfinder Lab**, **Combat Sandbox**) → Run scene (galaxy map ⇄ levels → galaxy cleared → … → victory or defeat summary with banked rewards) → back to the Hub. The **Sandbox** scene keeps the endless combat playground for tuning.

*UI tech (playtest 2):* the galaxy map, station and event dialogs, and equipment cards are **uGUI built from code** on a 1920×1080 reference canvas that scales to fit. Layout groups size rows and cards to their text, so descriptions wrap instead of being cut off, and long lists (a full Shop) scroll. Dialog rows show a label, a right-aligned price or state, and a detail line; the Shop groups them under Catalog, Upgrades and Services. Since M6 the HUD and Hub are uGUI too, so no IMGUI is left; menus sort above the HUD.

*Pause and options (M6):* Esc / Start opens the **pause menu** (also over the galaxy map, but not over a dialog or choice cards, which use Esc themselves): Resume, **Options**, **Controls** (keyboard and gamepad), and **Abandon run** (back to the Hub; the run counts as lost and banks what it earned) or Quit to Hub in the Sandbox. The Hub has the same Options page. Options: master, music and sound-effect volume (10% steps), screen shake (off to 100% in 25% steps, for motion-sensitive players), fullscreen, and an FPS counter. They belong to the machine, so they live in PlayerPrefs, not the meta save.

### 12.2 HUD (in level)
- **Top left:** Hull bar (with the numeric value) with the Shield bar layered over it.
- **Bottom center:** Ammo / reload ring around the crosshair (for the Quantum Charger, the charge level), secondary cooldown, and dash charge pips.
- **Top right:** Scraps, plus Dark Matter collected this run.
- **Top center:** Level objective (waves remaining, DM nodes, instability timer). Boss levels show the boss health bar here instead.
- **On pickups:** equipment choice cards in a modal. In single-player the modal pauses the game. In co-op nothing pauses: choices are made at the end of the level while the exit wormhole charges.
- *Implemented in M6 (uGUI):* hull bar with its value and a thin shield bar above it, primary and secondary gauges with their state, dash pips, ship trait and equipment list (coloured by rarity) top left; Scraps, Dark Matter, Cores and the seed top right; level, objective and the boss bar with phase marks top centre; banners and prompts; pause, galaxy-cleared and run-over overlays. Text carries a drop shadow so it reads over explosions. **A red vignette flashes at the screen edges when the hull is hit**, scaled by the damage. A **crosshair** follows the mouse (or sits 150 px along the aim for a gamepad) and replaces the system cursor while flying; its ring shows the primary's ammo, reload or charge, and turns orange on a warning (reloading, overheating).

### 12.3 Galaxy Map
- A vertical scrolling node map with icons for each type and a highlighted current path.
- Hovering or selecting a node shows its type, description and expected reward.
- A side panel shows the current build (equipped items) and hull.

### 12.4 Readability rules
Every colour and shape has a single meaning, so the player can read a crowded screen at a glance. These rules were
tightened after the M1 playtest, where glowing enemy cores were mistaken for bullets, and again after playtest 2, where
dense bullet patterns blew out into glare and the Lancer's charge line looked like a bullet. Bloom is kept moderate
(intensity 1.3, threshold 1.0) so a busy screen stays readable.

| Element | Look | Exclusive |
|---|---|---|
| **Enemy bullets** | A solid white core inside a crisp **magenta** rim, edged with a **dark outline**, and only a faint glow. Round. The outline keeps every bullet separate in dense patterns; the glow is alpha-blended, so overlapping bullets don't add up into a haze *(playtest 2)*. | Magenta and round orbs are used for nothing else. |
| **Enemies** | Solid, lit **crimson** bodies with small red-orange accent strips, on a faint **red floor glow**. Never a glowing orb. | The red floor glow marks units only. |
| **Enemy attack warnings** | Accent strips flare toward white-orange. Charge paths are **lanes of red-orange chevrons** on the floor that fill in as the wind-up completes: flat, segmented and pointed, never round. Spawns and eruptions are a soft red glow on the floor. **Areas** about to be hit are outlined on the floor with a fill growing out to the edge (the Scorcher's orange cone, the Crusher's red ring); the outline flickers just before it lands. **Beams** charge as a thin flickering line that thickens and steadies before the bolt. | Chevrons mean "a charge is coming this way". |
| **Hazards** | Flames are soft, alpha-blended **orange** puffs that billow as they fly. Lightning is a jagged white core in an orange glow. The Wardens' tether is a crackling orange line. | Orange fire never looks like a round magenta bullet. |
| **Armour & weak points** | Hits on armour throw a dull **grey spark** and do nothing. Weak points are **yellow**: crystals, rings, a cracked maw. | Yellow on an enemy always means "shoot here". |
| **Player & allies** | **Cyan / blue** hull lights and projectiles. Player projectiles are elongated bolts, not orbs. | Cyan is never used by enemies. |
| **Pickups** | **Gold** for Scraps; Dark Matter gets its own colour later. They spin and bob. | |
| **Environment** | Teal/blue grid and walls, kept dim. Hazards use orange-yellow warning stripes. | |


### 12.5 Localization
Every player-facing string lives in plain-text **.loc files** in `Assets/StreamingAssets/Localization/`, one per language (`en.loc`, `de.loc`...). They ship next to the build as ordinary files, so text can be tuned or translated without code or a rebuild.

- **Format:** one `key = value` per line. `#` starts a comment. `@language = English` names the language in the Options menu. Values are trimmed; wrap a value in double quotes to keep leading or trailing spaces. `\n` is a line break.
- **Placeholders:** named, like `{amount}`, optionally with a number format: `{seconds:0.0}`. Translators can move them anywhere in the sentence. Numbers always use the invariant culture (a dot for decimals). An unknown placeholder stays visible instead of crashing.
- **Keys:** UI text is grouped by screen (`hud.*`, `shop.*`, `hub.*`, `pause.*`, `options.*`, `objective.*`, `banner.*`...). Content uses its ID: `{id}.name` for everything, `{id}.desc` for item descriptions (with placeholders for the numbers), `{id}.trait` / `{id}.trait.desc` for ship traits, and `{id}.body`, `{id}.choice.N`, `{id}.result.N`, `{id}.fail.N` for Mystery events. Stat items build their descriptions from `stat.*` names and `stat.part.*` patterns.
- **Fallbacks:** a key missing from the current language uses English; a key missing from English shows as `[key]` and logs a warning once. Content with no key falls back to the text authored on its asset, so new content works before it is localized.
- **Languages:** the Options menu (Hub and pause) lists every .loc file in the folder. The choice is saved per machine. Menus rebuild in the new language at once.
- **Editing live:** F9 (development builds and the Editor) reloads the files while the game runs.
- **Safety net:** an EditMode test checks that every key the code uses, every built key (level types, stats, rarities, controls) and every piece of content's text exists in `en.loc`, and that the file parses without warnings.

---

## 13. Art & Audio Direction

### 13.1 Visuals
- **Style:** Stylized 3D in the spirit of Alienation and Helldivers: chunky readable silhouettes, dark environments with **strong neon accents**, emissive materials, bloom, and dynamic point lights from projectiles and explosions.
- **Camera:** perspective, 60° pitch, 20 u from the focus point, 40° FOV. It leads 2 u toward the aim direction and shakes on hits. A slight zoom-out in boss fights.
- **Rendering:** URP with the Forward+ renderer, so many dynamic lights are cheap. Post-processing: bloom, vignette, light color grading, and chromatic aberration on hits.
- **Juice:** hit-stop on heavy hits, screen shake (with a setting to turn it off), hit flashes, damage numbers (optional setting), and debris particles.
- **Production plan:** The slice uses **primitives and simple emissive shapes**, so gameplay can be judged on feel alone. Final 3D assets come from asset packs or commissions after the slice proves the fun.

### 13.2 Audio
- **Music:** dark synthwave and electronic tracks, one per galaxy, plus a boss track with intensity layers that rise with the wave count.
- **SFX priority:** player weapon feel (punchy, layered), dash whoosh, a distinct "hull hit" sting, and warning tells for Lancers and bosses.
- **Implemented in M6 (placeholder):** 37 sound effects synthesized by the content builder from oscillators, noise, envelopes and a sweeping low-pass filter, so the slice has sound without any third-party assets. Each gameplay moment asks for a **cue**: weapons, enemy shots, enemy hits, armour deflects (a metallic tink), shield and hull hits, small and large explosions, dash, pickups, telegraphs, the Quarry Maw's roar and eruption, menu move/confirm/back, exit open, boss defeated and run over; a chime when a secondary charge is back and a warp-in as each level starts *(playtest 5)*; and for galaxy 2, flame roars, zaps, slams and the Wardens' power-down and reboot. **Each enemy type winds up with its own sound** (Lancer whine, Gunner lock-on beep, Spinner warble, Carrier horn, Scorcher ignition, Arc Drone charge buzz, Crusher servo groan), with pitch variation, so a crowd doesn't drone *(playtest 4)*. A sound director plays them from 28 pooled voices **centred on the pilot's ship**: panned by which side of it a sound happens and quieter with distance (full volume within 6 u, 30% at 32 u) *(playtest 4)*. Every cue has a voice limit and a minimum interval, so a bullet storm stays a texture instead of a wall of noise. Final audio replaces the clips in the sound library without code changes.
- **Placeholder music (M6, playtest 5):** five synthesized loops. **Hub** (A minor, 80 BPM, slow pads and a soft arpeggio) plays in the menus. **Each galaxy has its own track**, on its map and in its levels alike, so it never restarts between them: the **Shattered Expanse** (A minor, 110 BPM: saw bass, detuned pads, square arpeggio, drums), the **Graveyard Drift** (D minor, 100 BPM: cold square bass, half-time drums, plucked arpeggio over i–♭VI–iv–V) and the **Veil Nebula** (E major sevenths, 92 BPM: soft bass, bright shimmering arpeggio, a gentle half-time pulse). The **boss** track (A minor, 128 BPM, i–♭II–i–V, sixteenth-note bass, four-on-the-floor) plays in every boss fight; once a galaxy is cleared, the next galaxy's track comes in under the "jumping" screen. Tracks crossfade over 1 s and fade out on victory or defeat. **Music outlives scene loads**: asking for the track that is already playing carries on instead of restarting *(playtest 5: the Hub track restarted when a run began)*. Loops are rendered with wrap-around notes and a primed filter, so the seam is silent.

---

## 14. Technical Design (for the build phase)

### 14.1 Project setup
- **Unity 6000.0.67f1, URP.** The project was created from the **2D template**, so it must be switched to a **3D Universal Renderer** (Forward+). Unneeded packages can be removed: `com.unity.feature.2d`, `com.unity.visualscripting` and `com.unity.multiplayer.center`.
- **Input System** (already installed): one `PlayerControls.inputactions` asset with Gameplay and UI maps.
- **Coordinates:** gameplay happens on the **XZ plane** with Y as up. Ship movement uses a kinematic Rigidbody or a custom controller, locked to Y = 0.

### 14.2 Architecture principles
- **Data-driven content via ScriptableObjects:** `ShipDefinition`, `WeaponDefinition` (primary and secondary), `EquipmentDefinition`, `EnemyDefinition`, `BossDefinition`, `GalaxyThemeDefinition`, `LevelTypeDefinition`, `WaveBudgetConfig`.
- **Stat & modifier system:** ship and weapon stats are calculated from the base value, then flat bonuses, then percentage bonuses. Equipment and traits register modifiers, and gameplay code never hard-codes item effects into stats.
- **Event hooks for equipment effects:** `OnKill`, `OnHit`, `OnDashStart`, `OnDashEnd`, `OnReload` and similar. Ship traits (Swarm Protocol, Energy Siphon, Lightning Core) and most equipment subscribe to these hooks.
- **RunState** (plain C# object, serializable, survives scene loads) has two parts:
  - **Shared:** seed, galaxy index, the map and the current node.
  - **Per player:** ship, loadout, equipment, hull and Scraps.

  In single-player there is just one per-player entry. Serializability also allows a mid-run save later.
- **MetaState**: currencies, unlocks and upgrades, stored per player profile. Saved as JSON in `Application.persistentDataPath`.
- **Simulation tick:** fixed 60 Hz. `CombatWorld` advances everything in a fixed order: players, then enemies, then projectiles. Gameplay collision is circle-vs-circle on the plane, with swept projectiles; no physics engine is involved. Transforms interpolate between ticks for smooth rendering.
- **Pooling** for projectiles, enemies, pickups and VFX. Projectile visuals already use per-prefab pools.
- **Scenes:** `Boot`, `Hub`, `Run`. The Galaxy Map is a UI overlay inside the Run scene, and arenas are loaded additively as prefabs or templates.

### 14.3 Randomness
Runs must be **reproducible from a seed** for debugging, seeded and daily runs, and co-op. In co-op all clients build the same map from a shared seed. This rules out:
- **Cryptographic RNGs** (`RandomNumberGenerator`), which cannot be seeded.
- **`System.Random`**, whose algorithm is not guaranteed to be identical across .NET/Mono versions or between Mono and IL2CPP, and whose quality is mediocre.
- **`UnityEngine.Random`**, which is global shared state: any system calling it shifts every other system's results.

**Approach:**
- **Generator:** our own **PCG32** (PCG-XSH-RR 64/32). It is a small, fast, well-tested, platform-independent algorithm with built-in support for independent *streams*. It is wrapped in an `IRandom` interface (`NextUInt`, `Range(int,int)`, `Range(float,float)`, `Chance(p)`, `WeightedPick`, `Shuffle`) so the algorithm can be swapped without touching gameplay code.
- **Run seed:** a 64-bit value. When the player doesn't enter a seed, it is generated with `System.Security.Cryptography.RandomNumberGenerator`, the one place a cryptographic RNG is used. Seeds are shown as short shareable codes on the run summary screen.
- **Deriving sub-streams:** each consumer gets its own generator, seeded by **hashing context into the run seed**: `Hash64(runSeed, "map", galaxyIndex)` or `Hash64(runSeed, "level", galaxyIndex, nodeId)`. The hash is SplitMix64 or xxHash64 over stable inputs. Never use `string.GetHashCode()`, which is not stable across runtimes. Because each level's seed depends only on its own position, **a level's content doesn't change based on what the player did earlier.**
- **Streams per level:** `layout`, `waves`, `loot` and `cosmetic` are separate, so purely visual randomness (particle jitter, debris) can never desync gameplay.
- **Ownership:** only the authority ([§14.4](#144-co-op-readiness)) draws from gameplay streams. Clients may draw from `cosmetic` freely.

### 14.4 Co-op readiness
Co-op (up to 4 players, online, host-authoritative) ships after the slice. Retrofitting multiplayer into a single-player codebase is very expensive, so the slice follows these rules from M0 onward:

| Rule | What it means in practice |
|---|---|
| **No player singletons** | No `Player.Instance`. `CombatRegistry` holds 1–N players (and enemies, by team). Enemy targeting, the camera, pickups and the wave director all work on that list. Enemies target the nearest player or a threat-weighted player. |
| **Input → commands** | Ships never read the Input System directly. A `PlayerInputSource` produces a `ShipCommand` struct per tick (move, aim, fire, secondary, dash, reload, interact), and the ship consumes commands. The source can be a local device, a network peer or a bot. |
| **Authority boundary** | Damage resolution, deaths, spawns, loot rolls, currency changes and RNG draws happen only in **authority code** (an `IsAuthority` check, always true in single-player). Clients send intents and the authority confirms outcomes. |
| **Simulation vs presentation** | Gameplay state lives in plain components that are updated in `FixedUpdate` on a fixed tick. VFX, audio and UI subscribe to gameplay **events** and never mutate state. |
| **Stable IDs for content** | Every ScriptableObject definition has a stable string ID. Saves and (later) network messages reference IDs, never object references or list indices. |
| **Runtime entity IDs** | Spawned enemies, projectiles and pickups get a runtime ID from the authority, so they can be referenced across the network later. |
| **No global time manipulation** | Don't use `Time.timeScale` for gameplay pauses or hit-stop that affects simulation. Hit-stop is presentation-only. Pausing goes through a `GameClock` that single-player can freeze but co-op ignores. |
| **Per-player UI** | The HUD is built around a "player view" component. Hull, ammo, dash and equipment panels bind to a specific player, and remote players get compact team frames. |

**Netcode choice (decide before co-op work starts):** Netcode for GameObjects with Unity Relay/Lobby is the default candidate, because it fits host-authoritative co-op and Unity 6. Its alternatives are Mirror and Fish-Net. The slice adds **no** netcode packages. It only follows the rules above.

**Co-op design questions (post-slice):** shared or individual Scraps and loot, how the group picks the next map node (vote or host decides), downed and revive mechanics, enemy HP and budget scaling per player, and how meta currencies are awarded.

### 14.5 Testing
- EditMode tests for map generation rules ([§9.2](#92-generation-rules)), the stat and modifier math, and economy calculations.
- **Determinism tests:** the same seed must always produce an identical galaxy map and level content, with golden values checked into the tests. Changing the order in which levels are played must not change any level's content.
- A debug menu for development builds: spawn enemies, grant items or currency, jump to a node, god mode, and **set the run seed**. M2 added Editor and development-build hotkeys: **F1** cycle primary, **F2** cycle secondary, **F3** god mode, **F4** kill all enemies, **F5** next wave. M3 added **F6** skip level, M4 added **F7** equipment choice, and playtest 3 added **F8** +100 Scraps (a grant: it doesn't bank back as Dark Matter, and an open Shop refreshes so new items become affordable). **F9** reloads the text files and **F10** spawns the next enemy type in front of the ship, cycling through every enemy and boss. The run seed is set on **Combat World**.

---

## 15. Design Decisions & Open Questions

**Decided (2026-10-09):**
| # | Question | Decision |
|---|---|---|
| 1 | Should the base dash have i-frames? | **Yes:** full i-frames for the dash duration. Phase Dash extends them ([§5.2](#52-dash), [§8.2](#82-dash-modules)). |
| 2 | Second faction | **The Remnant** (rival humans), as a placeholder |
| 3 | Narrative depth (Hub dialogue, scientist cast) | **Post-slice** |
| 4 | Co-op | **Out of scope for now, but the architecture plans for it** ([§14.4](#144-co-op-readiness)) |
| 5 | Currency and galaxy names | The working names in this doc |
| 6 | Art sourcing | Decide after M5. The slice uses primitives and emissive placeholders. |
| 7 | Post-game mode | **Pathfinder Protocols** (difficulty modifiers), post-launch |
| 8 | RNG | Seedable **PCG32** with derived per-subsystem streams. Crypto RNG is used only to generate the run seed ([§14.3](#143-randomness)). |

**Still open:**
| # | Question | Default until decided |
|---|---|---|
| 9 | Co-op player count (2 or up to 4?) | Plan for 4 |
| 10 | Netcode library | Netcode for GameObjects; decide before co-op work starts |
| 11 | Co-op loot, map voting and revive rules | Post-slice design task |

---

## 16. Changes from the Source Docs

| Source doc said | Issue | Resolution |
|---|---|---|
| "Classic gameplay of Warblade" + "move freely, top-down" + Alienation | Fixed vertical shooter vs. twin-stick | **Twin-stick arena shooter** with Warblade's arcade spirit |
| 3D art style, but the project uses the 2D template | Mismatch | **3D top-down**; switch the project to the 3D URP renderer |
| Boss: "player then begins a new run in the next galaxy" | Contradicts the run structure | A run spans **3 galaxies**; a boss kill continues the same run |
| "Procedurally generated galaxies" with no count | Undefined | 3 galaxy tiers, with theme pools per tier later |
| Mystery "can take the form of any other level type except Shop" | That would include Boss | Mystery resolves to **Scraps / Equipment / Dark Matter / Repair** only |
| Repair "never on the first two branches" | Ambiguous wording | Never in **rows 1–2** of the map |
| Equipment level: random drops **and** a choice of 3 | Two overlapping reward mechanisms | One **Cache Carrier** elite drops a cache that offers the choice of 3 |
| Arcanum Explorer: "roll 4 after each level" | Only some levels give equipment | +1 option on **every equipment choice** and +1 Shop slot |
| Perks, abilities, upgrades, Dash equipment | Several overlapping systems | **One slot-based Equipment system** with 4 categories ([§8](#8-equipment-in-run-build-system)) |
| Secondary weapons were placeholders | Missing | 5 secondaries defined ([§6.2](#62-secondary-weapons)) |
| Persistent and Boss currencies unnamed | Missing | **Dark Matter** and **Singularity Cores** |
| "Upgrade shields" with no health model | Missing | Hull (no regeneration) + regenerating Shield |
| Quantum Charger: "no magazine" | No limiting factor | Overheats if held at full charge |
| "Immunity During Dash" was a piece of equipment | The base dash now has i-frames (decision #1) | Reworked as **Phase Dash**, which extends the i-frames and restores shield when phasing through bullets |
| Shield Dash "ricochet bullets back" | Wording | Reflects enemy bullets |
| Persistent level: just "collect nodes" | No tension | **Instability timer**: a greed-versus-safety decision |
| Platform: "PC, consoles" | Scope for a solo developer | PC first, gamepad-first design, consoles after launch |
| No enemy or boss specifics | Missing | 7 archetypes, 4 elite modifiers, 3 bosses defined |

---

## 17. Production Plan: Vertical Slice

**Slice goal:** one complete, polished galaxy that proves the full loop: Hub → ship and loadout selection → map → levels → Shop → Boss → death or victory → meta spending → new run.

**Slice content scope:**
| Area | In the slice |
|---|---|
| Galaxies | 1 (The Shattered Expanse) |
| Ships | 2 (Titan-Class Cruiser, Stormbringer) |
| Primaries | 3 (Photon Repeater, Plasma Cutter, Quantum Charger) |
| Secondaries | 2 (Seeker Swarm, Nova Pulse) |
| Dash modules | 4 (Phase, Blink, Pushback, Bullet) |
| Other equipment | about 10 (from [§8.3–8.5](#83-weapon-mods-initial-pool)) |
| Enemies | 5 archetypes (Drone, Gunner, Spinner, Lancer, Carrier) + 2 elite modifiers |
| Boss | The Quarry Maw |
| Level types | All 7 |
| Meta | Dark Matter Lab track (3 upgrades) + 1 Core project, save/load |
| Art | Primitive and emissive placeholders with bloom |

**Milestones:**
| # | Milestone | Done when… |
|---|---|---|
| **M0** ✅ | Project setup | 3D URP and Forward+ are configured, the folder structure exists, the Input actions are set up, and Boot/Hub/Run scenes exist. The PCG32 `IRandom` and seed-derivation code exist, with determinism tests. |
| **M1** ✅ | Core combat feel | The ship moves, aims, shoots (Photon Repeater) and dashes with i-frames, and one arena has Drones and Gunners. Hull, shield and death work. Built on the co-op rules ([§14.4](#144-co-op-readiness)): `ShipCommand` input, `CombatRegistry` and an authority boundary. *Test: are the first 2 minutes fun?* |
| **M2** ✅ | Combat breadth | 3 primaries, 2 secondaries, 5 archetypes, the wave director, Scraps drops and pickups. |
| **M3** ✅ | Run structure | Galaxy map generation with its rules and tests, the map UI, all 7 level types, and RunState. |
| **M4** ✅ | Build system | Equipment slots, the modifier system, event hooks, choice cards, the Shop, and the 2 ships with their traits. |
| **M5** ✅ | Boss + meta | The Quarry Maw, run end and summary, the Hub, meta upgrades, and save/load. |
| **M6** (in progress) | Slice polish | VFX, juice, audio pass, balance pass, and an external playtest. *Done:* playtest 2 and 3 fixes, synthesized SFX and music with a sound director, explosions with shockwaves, camera kicks, muzzle flash, engine trail, hull-hit vignette, crosshair ammo ring, uGUI HUD and Hub, pause menu with options, the economy probe, a one-click Windows playtest build, and a **run log** (`run-log.jsonl` next to the profile: seed, loadout, progress, play time, cause of death) for testers to send back. *Left:* the external playtest and tuning from its feedback. |

**Started ahead of the slice:** galaxy 2's enemies (Scorcher, Arc Drone, Crusher) and its boss, the Binary Wardens, playable from galaxy 2 of a run and in the sandbox.

**After the slice:** Galaxies 2–3 themes and galaxy 3's boss, the remaining ships, weapons and equipment, The Remnant faction, narrative, final art, **online co-op**, Pathfinder Protocols, and console ports.
