# Contributing

How 5 of us work in one Unity repo without breaking each other's work.

## 1. One-time setup (everyone)

1. Install **Unity 6000.3.29f1** exactly, using Unity Hub. Don't open the project in a different version. It silently upgrades and rewrites files.
2. Install **Git LFS** and run it once:
   ```sh
   git lfs install
   ```
3. Configure **Unity Smart Merge** (UnityYAMLMerge), which merges scenes and prefabs much better than git's default:
   ```sh
   # Windows (adjust path to your Unity install)
   git config merge.unityyamlmerge.name "Unity SmartMerge"
   git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.29f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
   git config merge.unityyamlmerge.recursive binary
   ```
   (`.gitattributes` already routes `.unity`, `.prefab` and `.asset` files to this driver.)
4. In Unity, check **Edit → Project Settings → Editor**:
   - Asset Serialization Mode: **Force Text**
   - Version Control Mode: **Visible Meta Files**
5. Create your personal sandbox scene: `Assets/_Project/Scenes/Sandbox/<YourName>.unity`.

## 2. Ownership

Each module has **one owner** who reviews every PR that touches it. Anyone can contribute anywhere, but the owner has the final say on structure. Fill this in:

| Module / Area | Owner | Backup |
|---|---|---|
| Core (shared contracts) | TBD (tech lead) | everyone reviews |
| Player (controller, movement, input) | TBD | |
| Combat (gun, raycast, attachments, loot) | TBD | |
| Enemies (AI, spawners) | TBD | |
| Level (rooms, generator, doors) | TBD | |
| Run (timer, difficulty, shop, death) + Meta (unlocks, save) + UI | TBD | |
| `Scenes/Run.unity`, `Bootstrap.unity` | TBD | |

Mirror this in `.github/CODEOWNERS` so GitHub requests the right reviewers automatically.

## 3. Branches & PRs

- `main` is always playable. **No direct pushes.** Protect it in GitHub settings.
- Branch names: `<module>/<short-description>`, for example `player/slide`, `combat/rare-attachments`, `level/dead-end-rooms`.
- Keep PRs **small, open for less than about 2 days**. Big long-lived branches are where drift and conflicts come from.
- **Pull `main` daily** and merge it into your branch.
- Each PR needs:
  - 1 approval (the module owner)
  - **2 approvals if it touches `Scripts/Core/`, `Packages/`, `ProjectSettings/`, or a shared scene**
  - The project opens with no compile errors and no new console errors in Play mode
- Use **squash merge**.
- Commit messages: `<module>: <what changed>`, for example `combat: add pierce attachment behavior`.

## 4. Avoiding Unity merge conflicts

- **Claim shared files before editing them.** Post in the team chat: "🔒 editing Run.unity". Post again when you've pushed. Only one person edits a shared scene or prefab at a time.
- Work in **your sandbox scene** and in **prefabs** whenever possible.
- **Never** move or rename assets outside the Unity Editor (Explorer, VS Code, etc.). The `.meta` file has to move with the asset, or references break.
- Always commit the `.meta` file together with its asset.
- If you get a conflict on a `.unity` or `.prefab` file you didn't mean to touch, **take theirs** and redo your change. Don't try to hand-merge YAML.
- `ProjectSettings/` changes (layers, tags, input, physics) go in their own small PR with a clear description.

## 5. Making architectural changes

If you need a new module, package, cross-module event or interface, a new `StatType`, or anything that changes [ARCHITECTURE.md](ARCHITECTURE.md):

1. Write an ADR in `docs/decisions/` (copy `0000-template.md`).
2. Open the PR with the ADR (and code if small) and tag the team.
3. Merge after 2 approvals.

Design changes (gameplay rules, numbers, open questions) are made by editing [DESIGN.md](DESIGN.md) in a PR.

## 6. Using AI assistants

[CLAUDE.md](CLAUDE.md) holds the rules every AI tool should follow in this repo. If you use Cursor, Copilot, etc., point them at it (for example, copy it to `.cursorrules` or `.github/copilot-instructions.md`, or reference it).
Review AI-written code the same way you'd review a teammate's code. Watch especially for new singletons, cross-module references, and edits to files outside the task.
