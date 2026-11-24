# Game Design Document

> **Status:** Living document. Anything marked **TBD** is undecided. Anything under [Open Questions](#9-open-questions) needs a team decision before someone builds it.
> To change a design decision, edit this file in a PR. Don't settle it in chat and leave this file out of date.

## 1. Pitch

A **first-person, techno-medieval roguelike shooter**. You fight through procedurally built castle and dungeon rooms against robot peasants and their elite leaders. A clock that never stops keeps making the enemies harder. Gun attachments are your build. When you die you lose your build, but the items you've unlocked can keep appearing in future runs.

- **Genre:** First-person shooter roguelike ("shoot 'em up")
- **Tone / setting:** Techno-medieval. Medieval architecture and characters with robotic and technological parts.
- **Engine:** Unity **6000.5.9f1**
- **Team:** 5 developers

### Design pillars
1. **Pressure through time.** The timer always pushes you forward. Taking it slow has a cost.
2. **Your gun is your build.** Attachments change how you shoot *and* your stats (health, speed, damage, …).
3. **Clean slate, growing pool.** Your power resets every run, so every run starts equal. The only thing that carries over is **unlocks**: new items that can now appear. They add variety, not strength.
4. **Movement is a weapon.** Sliding and sprinting are central to staying alive, not extras.

---

## 2. Core Loop

```
Start Run ─► Enter Stage ─► Clear Room ─► Pick Doorway ─► Clear Room ─► ... ─► Shop Room ─► Next Stage
                 ▲               │                                               (timer frozen)    │
                 │               └── loot drops                                                    │
                 │                                                                                 │
                 └───────────────────────────────── Die ─► Run wiped ─► Restart ◄──────────────────┘
```

1. **Enter stage.** The run timer is running.
2. **Clear room.** The room spawns a set group of enemies based on the current difficulty. Kill them all to clear it. Loot drops.
3. **Choose a doorway.** A room has 1 or more exits. Some paths are dead ends.
4. After **N rooms** (TBD) you reach the **Shop Room**. The timer and difficulty **freeze**. Spend currency on attachments.
5. Leave the shop to start the **next stage**. The timer resumes.
6. **Die.** The run resets completely: stats, attachments and currency. Unlocks you earned stay (see 6.4). Start a new run.

---

## 3. Player Character

### 3.1 Controller
| Action | Input (default) | Notes |
|---|---|---|
| Move | WASD | Walk is the default speed |
| Sprint | Shift (**toggle**) | Not hold-to-sprint |
| Jump | Space | |
| Slide | Left Ctrl | Movement ability, see 3.2 |
| Look | Mouse | First-person camera |
| Fire | LMB | Hitscan (raycast) |
| Utility | TBD | See 3.3 |
| Interact | E (TBD) | Shop, doors, pickups |

All input goes through **Unity's Input System package** using one shared `InputActions` asset (see ARCHITECTURE.md).

### 3.2 Movement ability: Slide
- Starts from sprint (TBD: or from any movement?).
- Keeps or boosts momentum and lowers the player's hitbox.
- Tunables: duration, speed multiplier, cooldown. **All TBD.**

### 3.3 Utility
- **TBD.** Candidates: grenade, dash, shield, grapple.

### 3.4 Base stats
Every stat lives in the shared stat system so attachments can change any of them.

| Stat | Base | Notes |
|---|---|---|
| MaxHealth | TBD | |
| MoveSpeed | TBD | Walk speed. Sprint is a multiplier. |
| SprintMultiplier | TBD | |
| Damage | TBD | Per shot |
| FireRate | TBD | Shots per second |
| MagazineSize | TBD | Reloading? **TBD** |
| ReloadTime | TBD | |
| CritChance | TBD | Optional |

---

## 4. Weapons & Items

### 4.1 Gun
- **Hitscan** (raycast) to start. Projectile weapons can come later behind the same `IWeapon` interface.
- One gun with attachment slots. Multiple guns are **TBD**.

### 4.2 Attachments
- An attachment changes **weapon behavior** (spread, pierce, fire mode, …) and/or **stats** (health, speed, damage, …).
- **Rarity:** Common, Uncommon, Rare.
- Each attachment is a **ScriptableObject** asset, so designers can add one without writing code.
- Stacking rules (do duplicates stack? slot limits?) are **TBD**.

| Rarity | Drop weight | Shop price | Power budget |
|---|---|---|---|
| Common | TBD | TBD | Small, single-stat |
| Uncommon | TBD | TBD | Moderate or 2 stats |
| Rare | TBD | TBD | Strong or changes behavior |

### 4.3 Loot
- Rooms drop loot when cleared (currency and/or attachments, TBD).
- Currency is spent in the Shop Room.

---

## 5. Enemies

| Type | Description | Behavior |
|---|---|---|
| **Fodder: Robot Peasant** (with cap) | Basic, plentiful | TBD: melee rush? |
| **Elite / Lead** | Stronger, leads groups | TBD: buffs nearby fodder? ranged? |
| **Boss** *(maybe)* | End-of-stage encounter | TBD. Not in MVP scope. |

- Enemies use `NavMeshAgent` (AI Navigation package) for pathing.
- Enemy stats are multiplied by the current **difficulty** value (see 6.2).

---

## 6. Progression & Difficulty

### 6.1 Run structure
- **Stage** = a sequence of rooms followed by a **Shop Room**.
- Rooms per stage: **TBD**.
- Number of stages / win condition: **TBD** (endless?).

### 6.2 Timer & difficulty
- One **run timer** counts up from the start of the run.
- **Difficulty = f(run time, stages completed).** Both push it up. The exact formula and curve are TBD and live in a `DifficultyCurve` asset.
- The timer and difficulty **freeze in the Shop Room**.

### 6.3 Enemy spawning
- When the player **enters a room**, the room reads the current difficulty and spawns a **set group** of enemies at its spawn points.
- Difficulty decides: **how many** enemies, **which types** (the elite chance rises), and their **health and damage multipliers**.
- A room is **cleared** when all its enemies are dead. Then its doorways open.
- Because difficulty is read when you enter the room, playing slowly makes every *later* room harder. That's how the timer pressures you.
- (Possible later addition: reinforcements if you stay in a room too long. Not in MVP.)

### 6.4 Death & meta-progression
This follows the usual roguelike model: **your power resets, the item pool grows.**

| On death you **lose** | On death you **keep** |
|---|---|
| All stats and stat upgrades | **Unlocked items**: attachments that are now allowed to drop or appear in the shop |
| All equipped attachments | (Later, maybe: run stats/records shown in a menu) |
| All currency | |

- Every run starts with the **same base character**. Unlocks never make you stronger at the start of a run. They only add **variety** to what can show up.
- A new player starts with a **starter pool** of Common attachments. Uncommon and Rare items mostly need to be unlocked.
- **How you unlock items: TBD.** Options include picking up an item for the first time, reaching stage N, killing an elite or boss, or achievements.
- Unlocks are saved to disk and survive quitting the game.
- After death: show a run summary and any new unlocks, then restart (or go to the main menu, TBD).

---

## 7. Levels

- **Indoor procedural rooms** with a **castle / dungeon** theme.
- Rooms are **hand-built prefabs** that a generator stitches together at runtime. They are not fully procedural geometry.
- Each room prefab declares its **doorway sockets** (position plus facing).
- A cleared room can have **1 or more doorways**. Some branches are **dead ends**.
- Room types: `Combat`, `Shop`, `DeadEnd`, (later) `Boss`, `Treasure`?

---

## 8. Scope / Milestones

| Milestone | Goal |
|---|---|
| **M0: Skeleton** | Project setup, folders, assembly definitions, bootstrap scene, input asset |
| **M1: Greybox loop** | Move, shoot a dummy, one room, door to next room, die and restart |
| **M2: Core run** | Room generator, timer and difficulty, fodder enemy, loot, shop room |
| **M3: Build depth** | Attachments (all 3 rarities), elite enemy, slide, utility |
| **M4: Polish** | Art pass, audio, UI, balancing |
| **Stretch** | Boss, multiple guns, more room types |

---

## 9. Open Questions

> Settle these as a team. Record the answer here, and add an ADR in `docs/decisions/` if it affects code structure.

**Open**
1. What is the utility ability?
2. How many rooms per stage? Is the game endless, or does it have a final stage or boss?
3. What unlocks an item permanently? (See 6.4.)
4. What is the difficulty formula: how much do time and stages each count?
5. Can you slide without sprinting?
6. Do we have reloading and ammo, or infinite ammo?
7. Do duplicate attachments stack? How many slots?
8. Can you backtrack out of a dead end, or is it a trap?

**Resolved**
- ~~Permanent unlocks vs. full wipe~~ → The run's power wipes fully, and unlocks only expand the item pool (6.4, ADR 0002).
- ~~Slide input~~ → Left Ctrl.
- ~~Continuous spawning vs. waves~~ → A set group of enemies per room, chosen on entry from difficulty (time + stages). Cleared when all are dead (6.3).
