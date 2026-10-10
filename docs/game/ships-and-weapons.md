# Ships & weapons

[Docs home](../README.md) › [Game reference](README.md) › Ships & weapons

You choose a ship, one primary and one secondary weapon at the start of each run. ([GDD §5–§6](../GDD.md#5-player-ship))

## The ship

| Stat | Base | Notes |
|---|---|---|
| Hull | 100 | **Doesn't regenerate.** Restored only by repairs and healing effects. At 0 the run ends. |
| Shield | 25 | Absorbs damage first. Regenerates at 10/s after 3 s without damage. |
| Speed | 8 u/s | Reaches top speed almost instantly |
| Magnet radius | 3 u | Scraps inside it fly to the ship |
| Post-hit invulnerability | 0.3 s | After any hull hit, so a burst can't land all at once |

### The dash

| | |
|---|---|
| Distance | 5 u in 0.15 s |
| Charges | 1, recharging in 1.2 s (the Lab's Second Dash Charge adds one) |
| Invulnerability | **Full**, for the whole dash: bullets, beams and contact damage pass through |
| Direction | Where you're moving, or where you're aiming if you're still |

Dash modules change how it behaves; see [Equipment](equipment.md#dash-modules).

## Ships

| Ship | Hull | Shield | Speed | Trait | Status |
|---|---|---|---|---|---|
| **Titan-Class Cruiser** | 150 | 25 | 7 | **Reinforced Hull:** +50% hull and 10% less damage taken. The beginner ship. | Built |
| **Stormbringer Battleship** | 100 | 25 | 8 | **Lightning Core:** each primary hit has an 8% chance to call a lightning strike (25 damage in 2 u). | Built |
| Bot-Commander Frigate | 90 | 30 | 8.5 | Swarm Protocol: kills spawn homing bots | Planned (2 Cores) |
| Arcanum Explorer | 85 | 25 | 9 | Cargo Hold: +1 option on every equipment choice and Shop catalog | Planned (3 Cores) |
| Vampiric Destroyer | 110 | 0 | 8 | Energy Siphon: kills restore hull; +25% damage taken, no shield | Planned (4 Cores) |

## Primary weapons

Fire while held; magazine and reload. The main source of damage.

| Weapon | How it works | Damage | Rate | Magazine | Reload | Status |
|---|---|---|---|---|---|---|
| **Photon Repeater** | Fast single bolts. The all-rounder. | 8 | 10/s | 30 | 1.0 s | Built, starting |
| **Plasma Cutter** | Shotgun: 6 pellets in a 30° cone, 7 u range, damage falls to 40% at max range | 6 × 6 | 2/s | 6 | 1.2 s | Built, starting |
| **Quantum Charger** | Hold to charge (0.3 s minimum, 1.5 s full), release for a blast that **pierces every enemy** in its path. Overheats if held at full charge for 2 s: 1 s lockout. | 15–120 | | No magazine | | Built, unlock: 150 Dark Matter |
| Neutron Bomb Launcher | Slow shells that explode for area damage | 40 | 1.5/s | 4 | 2.0 s | Planned |
| Bot Dispatcher | Homing mini-bots | 10 | 5/s | 20 | 1.0 s | Planned |

## Secondary weapons

Cooldown-based bursts or utility.

| Weapon | How it works | Cooldown | Status |
|---|---|---|---|
| **Seeker Swarm** | A salvo of 6 homing missiles (20 damage each), each assigned a different nearby enemy. They fly over armour to reach weak points. | 8 s | Built, starting |
| **Nova Pulse** | A 4 u shockwave around the ship: 40 damage, **destroys enemy bullets** in range, knocks enemies back (heavy ones resist). A panic button. | 12 s | Built, starting |
| Railgun | A piercing line shot across the screen after a 0.25 s wind-up, 150 damage | 6 s | Planned |
| Gravity Well | A singularity that pulls enemies and bullets, then implodes | 14 s | Planned |
| Arc Lance | A channelled piercing beam, 120 damage per second | 10 s | Planned |

## Damage and crits

- Damage hits the shield, then the hull.
- **Critical Optics** gives a chance to deal ×2 damage. Crits are rolled when the shot is fired, from the pilot's own
  random stream.
- Equipment that triggers on hits (Ricochet, Incendiary, Lightning Core) only reacts to **primary projectile** hits,
  so effects never chain into each other.
