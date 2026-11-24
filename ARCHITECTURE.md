# Architecture

This is the **technical contract** for the codebase. Its purpose is to let 5 people (and their AI assistants) work in parallel without stepping on each other or slowly drifting into 5 different architectures.

**Rule of thumb:** if you're about to add a new *kind* of thing (a new system, manager, event pattern, package or assembly), stop and write a short ADR in `docs/decisions/` first.

---

## 1. Module layout

Code is split into **modules**. Each module is its own **Assembly Definition (`.asmdef`)**. The compiler enforces the dependency rules, so a reference that's not allowed fails to build.

```
                    ┌──────────────┐
                    │  Bootstrap   │  wires everything together (only module that references all)
                    └──────┬───────┘
     ┌──────────┬──────────┬──────────┼──────────┬──────────┬──────────┐
     ▼          ▼          ▼          ▼          ▼          ▼          ▼
  Player     Combat     Enemies     Level       Run        Meta        UI
     │          │          │          │          │          │          │
     └──────────┴──────────┴──────────┼──────────┴──────────┴──────────┘
                                ▼
                          ┌──────────┐
                          │   Core   │  interfaces, events, stats, shared data types. NO game logic.
                          └──────────┘
```

| Module | Namespace | Owns | May reference |
|---|---|---|---|
| **Core** | `Roguelike.Core` | Interfaces (`IDamageable`, `IWeapon`, …), event bus and event structs, stat system, service locator, shared enums | Unity only |
| **Player** | `Roguelike.Player` | Character controller, camera look, sprint, jump, slide, utility, input handling | Core |
| **Combat** | `Roguelike.Combat` | Gun, raycast firing, damage application, attachments, loot definitions | Core |
| **Enemies** | `Roguelike.Enemies` | Enemy AI, spawners, enemy definitions | Core |
| **Level** | `Roguelike.Level` | Room prefabs, room generator, doorways, room clear detection | Core |
| **Run** | `Roguelike.Run` | Run state, timer, difficulty curve, stage flow, shop logic, death and reset | Core |
| **Meta** | `Roguelike.Meta` | Permanent unlocks: save/load, unlock rules, exposing the unlocked item pool | Core |
| **UI** | `Roguelike.UI` | HUD, menus, shop UI | Core |
| **Bootstrap** | `Roguelike.Bootstrap` | Scene startup, registering services, connecting modules | Everything |
| **Tests** | `Roguelike.Tests.*` | EditMode / PlayMode tests | The module it tests and Core |

### Hard rules
1. **Feature modules never reference each other.** Player must not `using Roguelike.Enemies;`. If two modules need to talk, they use either:
   - an **interface in Core** (for example, Combat raycasts and calls `IDamageable.TakeDamage()`, which Enemies implements), or
   - an **event in Core** (for example, Enemies publishes `EnemyKilled` and Run and UI subscribe to it).
2. **Core contains no behavior.** It holds interfaces, data structs, the event bus and the stat math, and nothing that "plays the game".
3. **Changes to Core need 2 approvals** (see CONTRIBUTING.md), because everyone depends on it.

---

## 2. Communication patterns (use these, don't invent new ones)

### 2.1 Events: `EventBus<T>`
For "something happened" notifications that other modules might care about.

```csharp
// Core/Events/EnemyKilled.cs   (one file per event)
namespace Roguelike.Core.Events
{
    public readonly struct EnemyKilled
    {
        public readonly Vector3 Position;
        public readonly EnemyTier Tier;
        public EnemyKilled(Vector3 position, EnemyTier tier) { Position = position; Tier = tier; }
    }
}

// Publisher (Enemies)
EventBus<EnemyKilled>.Raise(new EnemyKilled(transform.position, tier));

// Subscriber (Run, UI, ...)
void OnEnable()  => EventBus<EnemyKilled>.Subscribe(OnEnemyKilled);
void OnDisable() => EventBus<EnemyKilled>.Unsubscribe(OnEnemyKilled);
```

- Event types are `readonly struct`s in `Core/Events/`, **one file per event**. This avoids the merge conflicts you'd get from one big `GameEvents.cs` file.
- Always unsubscribe in `OnDisable`/`OnDestroy`.
- Name events in the past tense: `RoomCleared`, `PlayerDied`, `ShopEntered`.

### 2.2 Services: `Services.Get<T>()`
For "I need to ask a system something" (for example, getting the current difficulty).

```csharp
// Core defines the interface
public interface IDifficultyProvider { float Current { get; } }

// Run implements it, Bootstrap registers it
Services.Register<IDifficultyProvider>(difficultyClock);

// Enemies consumes it without knowing about the Run module
float diff = Services.Get<IDifficultyProvider>().Current;
```

- **No ad-hoc singletons** (`public static X Instance`). Use services instead.
- **No `FindObjectOfType` / `GameObject.Find` / string tag lookups** in gameplay code. Use serialized references, services or events.

### 2.3 Direct references
Inside a single module, use plain `[SerializeField]` references between components. That's normal and fine.

---

## 3. Data: ScriptableObjects for content, code for behavior

All tunable content lives in ScriptableObject assets under `Assets/_Project/Data/`:

| Asset type | Module | Examples |
|---|---|---|
| `AttachmentDefinition` | Combat | Name, rarity, icon, `StatModifier[]`, optional behavior |
| `EnemyDefinition` | Enemies | Prefab, base health, damage, speed, tier |
| `RoomDefinition` | Level | Prefab, room type, weight |
| `DifficultyCurve` | Run | AnimationCurve of time → difficulty |
| `PlayerBaseStats` | Player | Starting stat values |

**Do not hardcode tuning numbers in MonoBehaviours.** Put them in the relevant Definition asset so balancing doesn't need a code change.

---

## 4. Stat system (Core)

Every number that an attachment can change goes through the stat system. That's what keeps "attachments can upgrade health/speed/damage" from becoming 15 special cases.

```csharp
public enum StatType { MaxHealth, MoveSpeed, SprintMultiplier, Damage, FireRate, MagazineSize, ReloadTime, CritChance }

public enum ModifierKind { Flat, PercentAdd, PercentMult }

[Serializable]
public struct StatModifier { public StatType Stat; public ModifierKind Kind; public float Value; }

// final = (base + ΣFlat) × (1 + ΣPercentAdd) × Π(1 + PercentMult)
public class StatBlock
{
    public float Get(StatType stat);
    public void AddModifier(StatModifier mod, object source);
    public void RemoveAllFrom(object source);
    public event Action<StatType> Changed;
}
```

- The player owns one `StatBlock`. Attachments add modifiers to it, with the attachment as the `source`.
- Systems **read** stats at the time they use them (`stats.Get(StatType.MoveSpeed)`) and don't cache copies.
- Adding a new `StatType` counts as a Core change (2 approvals).

---

## 5. Run lifecycle

The **Run** module is the single owner of run state. Nothing else keeps state that survives between rooms.

```
RunStarted ─► StageStarted ─► RoomEntered ─► RoomCleared ─► (repeat) ─► ShopEntered ─► ShopExited ─► StageStarted ...
                                                                                                   │
PlayerDied ─► RunEnded ─► (full wipe: StatBlock reset, attachments cleared, currency = 0) ─► RunStarted
```

- **Timer and difficulty pause** on `ShopEntered` and resume on `ShopExited`.
- Because the run is fully wiped on death, the run reset should **reload the run scene** (or rebuild it from scratch) rather than trying to "undo" state piece by piece. Fewer reset bugs.

### 5.1 Difficulty
```csharp
// Core
public interface IDifficultyProvider { float Current { get; } }   // implemented by Run
```
- Run computes `Current` from **run time and stages completed** using a `DifficultyCurve` asset.
- **Level / Enemies read it once, when a room is entered**, to choose that room's enemy group (count, types, elite chance, multipliers). They do not re-read it during the fight.

### 5.2 Meta-progression (permanent unlocks)
Two kinds of state must never mix:

| | **Run state** (Run module) | **Meta state** (Meta module) |
|---|---|---|
| Contains | Stats, attachments, currency, timer, stage | Set of unlocked item IDs |
| Lifetime | One run. Wiped on death. | Forever. Saved to disk. |
| Affects player power? | Yes | **Never.** It only controls *which* items can appear. |

```csharp
// Core
public interface IUnlockService
{
    bool IsUnlocked(string itemId);
    void Unlock(string itemId);              // Meta saves to disk and raises ItemUnlocked
}
```
- Combat's loot table and Run's shop **filter the item pool** with `IUnlockService.IsUnlocked(...)`.
- Each `AttachmentDefinition` has a stable `Id` (string, never renamed) and an `UnlockedByDefault` flag for the starter pool.
- Meta listens for events (`EnemyKilled`, `StageCompleted`, …) to decide when to unlock something. Unlock rules are TBD (DESIGN.md 6.4).
- Save format: a small JSON file in `Application.persistentDataPath`, owned **only** by Meta.

---

## 6. Scenes & prefabs (the #1 source of merge pain)

Unity scene and prefab files are YAML that is hard to merge. These rules exist to avoid that:

| Scene | Purpose | Who edits |
|---|---|---|
| `Scenes/Bootstrap.unity` | Loads first, registers services | Bootstrap owner only |
| `Scenes/MainMenu.unity` | Menu | UI owner only |
| `Scenes/Run.unity` | The actual game, kept nearly empty and filled at runtime | Claim it before editing (see CONTRIBUTING.md) |
| `Scenes/Sandbox/<YourName>.unity` | **Your personal test scene.** Break whatever you want. | Only you |

- **Build in prefabs, not scenes.** Scenes should contain a few root prefabs. Edit the prefab, not the scene instance.
- Prefer **prefab variants** and **nested prefabs** over copying and pasting.
- Do your experimenting in your Sandbox scene.

---

## 7. Folder structure

```
Assets/
  _Project/                     ← everything we make (underscore keeps it at the top)
    Art/        (Models, Materials, Textures, Animations, VFX)
    Audio/
    Data/       (ScriptableObject assets: Attachments/, Enemies/, Rooms/, ...)
    Input/      (InputActions asset)
    Prefabs/    (Player/, Enemies/, Rooms/, Weapons/, UI/)
    Scenes/     (Bootstrap, MainMenu, Run, Sandbox/)
    Scripts/
      Core/       Roguelike.Core.asmdef
      Player/     Roguelike.Player.asmdef
      Combat/     Roguelike.Combat.asmdef
      Enemies/    Roguelike.Enemies.asmdef
      Level/      Roguelike.Level.asmdef
      Run/        Roguelike.Run.asmdef
      Meta/       Roguelike.Meta.asmdef
      UI/         Roguelike.UI.asmdef
      Bootstrap/  Roguelike.Bootstrap.asmdef
    Settings/   (URP assets, physics layers doc, etc.)
  Tests/
    EditMode/
    PlayMode/
  ThirdParty/                   ← imported asset store / external packages, never modified
```

---

## 8. Code conventions

- **C# naming:** `PascalCase` for types, methods and properties. `_camelCase` for private fields. `camelCase` for locals and parameters.
- **Fields:** `[SerializeField] private`, never `public` fields. Expose read-only properties if needed.
- **Namespaces** must match the module: `Roguelike.Player`, `Roguelike.Combat.Attachments`, …
- **One public type per file**, and the file name matches the type name.
- **No magic strings.** Use constants or generated code for tags, layers, animator params and input action names.
- **Physics layers** are defined once in `Settings/Layers.md` and in a `Core/Layers.cs` constants class. Don't invent new layers without updating both.
- **Hitscan** uses `Physics.Raycast` against a `LayerMask` (serialized), never `~0`.
- **Update loops:** don't poll for things that could be events.
- **Comments:** explain *why*, not *what*.
- `.editorconfig` at the repo root sets the formatting. Let your IDE apply it.

---

## 9. Packages

Packages are set in `Packages/manifest.json`. **Adding or upgrading a package needs an ADR**, because it changes the project for everyone.

Planned baseline:
- Input System
- AI Navigation (NavMesh)
- Universal Render Pipeline (URP)
- Cinemachine (optional, for camera effects)
- Test Framework

---

## 10. Architecture Decision Records (ADRs)

`docs/decisions/NNNN-short-title.md`. Write one when you:
- add a module, package, or a new cross-module pattern
- change something in this document
- make a design call that changes code structure (for example, "meta-progression: yes or no")

Use `docs/decisions/0000-template.md`. Keep it short. Ten lines is fine.
