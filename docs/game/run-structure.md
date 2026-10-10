# Run structure

[Docs home](../README.md) › [Game reference](README.md) › Run structure

A run crosses **three galaxies**. Each galaxy is a branching map of levels that narrows to a Shop and a Boss. Beat
the third boss and humanity has a new home. ([GDD §9–§10](../GDD.md#9-run-structure-galaxy-map))

## The galaxies

| # | Galaxy | Guardian | Enemy health | Wave budget | Boss reward |
|---|---|---|---|---|---|
| 1 | The Shattered Expanse | [The Quarry Maw](bosses.md#the-quarry-maw) | ×1.0 | ×1.0 | 1 Singularity Core |
| 2 | The Graveyard Drift | [The Binary Wardens](bosses.md#the-binary-wardens) | ×1.6 | ×1.4 | 2 Cores |
| 3 | The Veil Nebula | [The Hive Matriarch](bosses.md#the-hive-matriarch) | ×2.4 | ×1.8 | 3 Cores |

From galaxy 2 the **Scorcher**, **Arc Drone** and **Crusher** join the enemy mix. The galaxies share one arena and
look for now; their own themes and hazards are planned.

## The galaxy map

![A generated galaxy map: entry nodes at the bottom converge on the Shop and the Boss](../images/m3-map-graph.png)

- **Four playable rows**, then the **Shop** (row 5), then the **Boss** (row 6). A path through a galaxy is 4 levels,
  the Shop and the Boss.
- **2 to 4 nodes per row.** Each node links to 1 or 2 nodes in the next row, and lanes never cross. Some nodes get a
  diagonal bridge to a neighbouring lane, so you can switch paths.
- **Every node is reachable**, and every path reaches the Shop.
- **Types are visible**, except Mystery. No path has three of the same type in a row.
- **Seeded:** the same seed gives the same maps and levels, and a level plays the same whatever route led to it.

| Node | Weight | Rules |
|---|---|---|
| Equipment ⬢ | 35% | At least one in row 1 |
| Scraps ⚙ | 30% | |
| Dark Matter ◆ | 20% | Never in row 1 |
| Mystery ? | 15% | At most 2 per galaxy, never in row 1 |
| Repair ✚ | | At most 1 per galaxy, 60% chance to exist, rows 3–4 only |
| Shop $ | | Always the single node in row 5 |
| Boss ☠ | | Always the single node in row 6 |

## Level types

Every level ends by flying into the **exit wormhole**, which opens once the objective is done. Hull damage carries over
between levels; the shield refills.

| Level | Objective | Reward |
|---|---|---|
| **Scraps** ⚙ | Clear 4 waves at ×1.3 budget, with double Scraps drops | +30 / 45 / 60 Scraps bonus (by galaxy) |
| **Equipment** ⬢ | Clear 3 waves. The last wave has an elite **Cache Carrier**: destroy it and grab its cache | A choice of 3 items, or 15 Scraps to skip |
| **Dark Matter** ◆ | Harvest 8–12 crystals (stand near one for 1 s) under light pressure. The exit is open from the start; after 90 s the wormhole destabilises and waves escalate | About 5 / 7.5 / 10 Dark Matter per crystal |
| **Repair** ✚ | No enemies. Use the station once | Restore 40% hull, or +15 max hull for the run |
| **Mystery** ? | Usually an event (below); otherwise a level with ×1.5 rewards, sometimes an ambush | Varies |
| **Shop** $ | No enemies. Spend Scraps at the terminal | See [Economy](economy.md#the-shop) |
| **Boss** ☠ | Defeat the galaxy's guardian | Singularity Cores, a 30% repair, a Rare-or-better item; then the wormhole pulls you onward |

## Mystery events

About 70% of Mystery nodes are **events**: a short scene and two or three choices, each trading something for
something else, Slay the Spire style. Every choice spells out its effects first, gains in green, costs in red, and a
gamble's odds. Event costs never kill (hull stops at 1); unaffordable choices are shown but disabled.

The other 30% reveal a Scraps, Equipment, Dark Matter or Repair level with **×1.5 rewards**. A revealed combat level
has a 10% chance to be an **ambush**: an extra elite wave and **double rewards**.

| Event | The choice |
|---|---|
| Derelict Freighter | Strip the cargo (+45 Scraps, −15 hull), scan from afar (+15 Scraps), or leave |
| Distress Beacon | Answer (50%: a Rare item; otherwise a Remnant ambush), or log it |
| Unstable Singularity | Skim the horizon (+30 Dark Matter, −10 max hull), or steer clear |
| Wandering Engineer | Upgrade an item (40 Scraps), patch the hull (+40 hull for 25 Scraps), or decline |
| Spore Bloom | Let them grow (heal 35%, −8% speed for the run), or burn them off (+20 Scraps) |
| Remnant Toll Gate | Pay 35 Scraps, or run the blockade (an ambush) |
| Sealed Weapons Cache | Crack it (60%: an item; otherwise −25 hull), cut it open (20 Scraps for an item), or leave |
| Echo of Earth | Listen (+25 hull), or record it for the Lab (+15 Dark Matter) |
| Scrap Trader | Trade an item for a Rare one, sell an item for 60 Scraps, or move on |
| Gravity Well | Burn out (−20 hull), jettison salvage (−30 Scraps), or slingshot (50%: +20 Dark Matter; otherwise −35 hull) |

An event isn't repeated within a run until you've seen them all.
