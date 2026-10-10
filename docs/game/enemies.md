# Enemies

[Docs home](../README.md) › [Game reference](README.md) › Enemies

Every enemy is a crimson machine with red-orange accent lights over a red floor glow. **Before any attack its accents
flare and it plays its own wind-up sound**, so you can read a crowded arena by ear as well as by eye.
([GDD §7.3](../GDD.md#73-enemy-archetypes))

![Galaxy 1's enemies: Drones, Gunners, a Spinner, a Lancer, a Carrier, an elite, bullets and Scraps](../images/m2-enemies.png)

## At a glance

| Enemy | From | HP | Speed | Cost | Threat | Beat it by |
|---|---|---|---|---|---|---|
| [Drone](#drone) | Galaxy 1 | 15 | 5.5 | 1 | Swarms you, contact damage | Keep moving; a few shots each |
| [Gunner](#gunner) | Galaxy 1 | 35 | 4 | 2 | Aimed 3-shot bursts from range | Strafe sideways after its glow |
| [Spinner](#spinner) | Galaxy 1, wave 2 | 60 | 2 | 3 | Rotating bullet rings | Slip through the moving gaps |
| [Lancer](#lancer) | Galaxy 1, wave 2 | 60 | 4.5 | 3 | Telegraphed high-speed charges | Step off the chevron lane; punish the wind-down |
| [Carrier](#carrier) | Galaxy 1, wave 4 | 150 | 1.8 | 6 | Launches Drones; armoured front | Flank it and shoot the yellow rear |
| [Scorcher](#scorcher) | Galaxy 2 | 45 | 3.2 | 3 | Flamethrower cone up close | Stay out of the cone; hit it during the cooldown |
| [Arc Drone](#arc-drone) | Galaxy 2 | 20 | 5 | 2 | Instant zap along a locked line | Step off the line, or dash through |
| [Crusher](#crusher) | Galaxy 2, wave 2 | 110 | 2.8 | 4 | A lunging slam if it reaches you | Move away the moment its ring appears; shoot it while it recovers |

Speeds are in units per second (the ship flies at 8). Cost is the wave director's budget price.

## Galaxy 1: the Shattered Expanse

### Drone

A spinning blade that chases the ship, weaving as it closes in. It deals **10 contact damage**, then bounces off and
can't hit again for 0.75 s. Alone it's nothing; in a swarm it boxes you in.

### Gunner

Keeps **8–12 u** away and strafes. Every 2 s it **glows for 0.45 s** (a two-tone lock-on beep), then fires a
**3-shot burst** of 8-damage bullets at where you are. It slows to 30% while attacking.

> Move *sideways* when it glows: the burst is aimed at your position when it fires.

### Spinner

A slow disc ringed with emitters that holds 6–13 u away. Every 1.8 s the emitters **flare for 0.35 s** (a warble),
then it fires a **ring of 10 bullets** (8 damage, 6 u/s). Each ring is turned 11° from the last, so the gaps move.

### Lancer

An arrowhead that closes in. Within 9 u it plants itself and shows a **lane of chevrons** along its charge for 0.7 s
(a rising whine), the chevrons lighting up from the Lancer outward. Then it charges **14 u at 24 u/s** for **30
damage**. Every charge ends in a **0.6 s skid**; after a miss it re-aims and **jousts once more**. After its last charge
it recovers for 1 s and waits 1.8 s.

> The chevrons are a path, not a bullet: step off the lane. The skid and the recovery are when it's open.

### Carrier

A slow tank that keeps about 10 u away and turns at only 60°/s. Every 4 s its flank bays flare (a low horn) and it
launches **2 Drones**, with at most 6 alive; launched Drones drop no Scraps. Its **armoured front takes 60% damage**;
its **yellow rear weak point takes 200%**.

## Galaxy 2: the Graveyard Drift

These join the mix from galaxy 2. In the combat sandbox they appear from wave 6, and **F10** spawns any enemy.

### Scorcher

<img src="../images/scorcher-windup.png" width="49%" alt="A Scorcher's orange cone telegraph on the floor, over the ship"> <img src="../images/scorcher-spray.png" width="49%" alt="The Scorcher spraying flames that billow past the ship">

A squat flamethrower tank. Within **4.5 u** it plants itself for **0.6 s** while an **orange cone** (±18°, 5.6 u)
fills in on the floor (an ignition hiss). Then it **sprays for 1.4 s**: a stream of flames (4 damage each) that billow
as they fly and wash over the ship, while it turns after you at 60°/s and creeps forward. Standing in the fire costs
about **13 hull per second**. Then it cools down for 2.2 s at half speed.

> Its reach is short. Back off or circle out of the cone during the wind-up, then punish the cooldown.

### Arc Drone

<img src="../images/arc-drone-charge.png" width="49%" alt="An Arc Drone's thin red charge line through the ship"> <img src="../images/arc-drone-zap.png" width="49%" alt="The jagged lightning bolt striking the ship">

A fragile hovering drone with two coil prongs that strafes at **6–8.5 u**. It **locks onto where you are** and
charges for **0.7 s**, showing a thin, flickering red line through that spot out to its 12 u reach (a rising electric
buzz). Then it fires **instantly** along the locked line: **14 damage** to the first ship it touches.

> The line doesn't follow you: one sidestep during the charge and it misses. Dashing through the bolt works too. Only
> 20 HP, so kill it first when you can.

### Crusher

<img src="../images/crusher-windup.png" width="60%" alt="A Crusher's red slam ring filling in around it, the ship at its edge">

A heavy armoured block on treads, slow and almost **immune to knockback** (15%). When it gets within **3.2 u** it rears
up for **0.4 s** behind a **red ring** at its 3.3 u reach (a servo groan), with a fill growing out to the ring, and
**lunges at you**. Then it **slams: 40 damage** to everything in the ring, with a shockwave. It recovers, motionless,
for 1 s.

> Don't let it get close. Move away the instant the ring appears, or dash out of it: drifting away isn't enough, it
> lunges. Its recovery is the time to unload on it.

## Elites

Any enemy can roll as an **elite**: bigger (×1.2), with a **gold ring** under it, and it **always drops ×3 Scraps**.
An elite costs double budget.

| Modifier | Effect |
|---|---|
| **Armored** | ×2 health, ×0.75 speed |
| **Hasty** | ×1.5 speed, and attacks ×1.5 faster (cooldowns, wind-ups and bursts) |

The chance rises per wave, map row and galaxy: `min(35%, 3% + 2% per wave + 4% per row + 5% per galaxy)`. Splitting
and Volatile elites are planned.

## Spawning and scaling

- Each wave has a **budget**: `6 × galaxy (1.0 / 1.4 / 1.8) × (1 + 15% per row) × (1 + 25% per wave)`. The director
  buys a random weighted mix of affordable enemies with it, up to 40 per wave.
- Spawns are announced by a **red glow on the floor for 1 s** and never land within 6 u of a ship.
- The next wave comes when 80% of the current one is dead, or after 25 s.
- **Enemy health scales by galaxy:** ×1.0, ×1.6, ×2.4.

## Drops

| Enemy | Scraps | Chance |
|---|---|---|
| Drone | 1 | 50% |
| Gunner | 2 | 75% |
| Spinner | 3 | 85% |
| Lancer | 2 | 80% |
| Carrier | 8 | 100% |
| Scorcher | 2 | 80% |
| Arc Drone | 1 | 70% |
| Crusher | 4 | 90% |
| Elites | ×3 | always |

Scraps settle on the floor, fly to the ship inside its magnet radius and blink out after 10 s. When a fight is won,
every Scrap left on the floor flies to you.
