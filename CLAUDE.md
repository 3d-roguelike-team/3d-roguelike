# CLAUDE.md

Instructions for AI coding assistants (Claude Code, Copilot, Cursor, etc.) working in this repo.
**Five people share this codebase. Your job is to make changes that fit the existing architecture, not to invent new structure.**

## Project
- First-person techno-medieval roguelike shooter. Design: [DESIGN.md](DESIGN.md)
- **Unity 6000.3.29f1**, C#, URP, Input System, AI Navigation
- Architecture contract: [ARCHITECTURE.md](ARCHITECTURE.md). **Read it before writing code.**
- Team workflow: [CONTRIBUTING.md](CONTRIBUTING.md)

## Non-negotiable rules
1. **Stay inside the module you were asked to work on.** Modules are `Assets/_Project/Scripts/<Module>/`, each with its own `.asmdef`.
2. **Feature modules (Player, Combat, Enemies, Level, Run, Meta, UI) must never reference each other.** Cross-module communication goes **only** through:
   - interfaces in `Roguelike.Core`, or
   - `EventBus<T>` events (a `readonly struct` in `Core/Events/`, one file per event), or
   - `Services.Get<T>()` for interfaces registered by Bootstrap.
   Never fix a compile error by adding an asmdef reference between feature modules.
3. **Do not modify `Core` unless explicitly asked.** If a task seems to need a Core change (a new interface, event or `StatType`), **stop and tell the user**. Core changes need team review.
4. **Do not add new patterns.** No new singletons (`static Instance`), managers, event systems, DI frameworks or service locators. Use the ones in ARCHITECTURE.md §2.
5. **Do not add or upgrade packages**, and don't edit `Packages/manifest.json` or `ProjectSettings/` unless explicitly asked.
6. **Never hand-edit `.unity`, `.prefab`, `.asset` or `.meta` files.** Tell the user what to change in the Unity Editor instead. Never delete or regenerate `.meta` files, because that breaks references (GUIDs).
7. **Tuning values go in ScriptableObject Definitions**, not hardcoded constants in MonoBehaviours.
8. **Stats that attachments can modify must be read through `StatBlock.Get(StatType)`**, not stored as plain fields.
9. **Run state and meta state never mix.** Anything that lasts across runs belongs in the Meta module and must never increase player power. It only controls which items can appear.
10. **No `FindObjectOfType`, `GameObject.Find`, `SendMessage`, or string-based tag/layer lookups** in gameplay code.

## Code style
- Namespace matches module: `Roguelike.<Module>[.Sub]`.
- `[SerializeField] private` fields, `_camelCase` private fields, `PascalCase` members.
- One public type per file, and the file name matches the type.
- Subscribe to events in `OnEnable`, unsubscribe in `OnDisable`.
- Follow `.editorconfig`.
- Prefer small, focused classes. Don't create god-objects like `GameManager` that do everything.

## When writing code
- Before creating a new class, **search for an existing one** that does the job (Grep the `Scripts/` folder).
- If you create a new MonoBehaviour that needs scene setup, finish by listing the **exact Editor steps** (which prefab, which component, which fields to assign).
- Add EditMode tests under `Assets/Tests/EditMode/` for pure logic (stat math, difficulty curve, room generation rules).
- Keep diffs small and focused on the task. Don't reformat or "clean up" unrelated files. Other people own them.

## When unsure
Ask the user. Don't guess at design. If DESIGN.md marks something **TBD** or lists it as an open question, don't silently pick an answer. Point it out.
