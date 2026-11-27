# 3D Roguelike (working title)

A first-person **techno-medieval roguelike shooter** built in **Unity 6000.5.9f1**.
Fight through procedurally connected castle and dungeon rooms. Kit out your gun with attachments, and stay ahead of a clock that keeps making enemies harder. If you die, your build is gone, but the items you've unlocked can keep showing up in future runs.

## Docs
| Doc | What it's for |
|---|---|
| [DESIGN.md](DESIGN.md) | Game design: loop, mechanics, enemies, progression, open questions |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Technical contract: modules, communication patterns, folder layout, conventions |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Setup, ownership, branches/PRs, avoiding Unity merge conflicts |
| [CLAUDE.md](CLAUDE.md) | Rules for AI coding assistants |
| [docs/decisions/](docs/decisions/) | Architecture Decision Records (ADRs) |

## Getting started
1. Install **Unity 6000.5.9f1** via Unity Hub and **Git LFS**.
2. Clone, then `git lfs install`.
3. Follow the one-time setup in [CONTRIBUTING.md](CONTRIBUTING.md#1-one-time-setup-everyone) (Smart Merge, serialization settings).
4. Open the project in Unity Hub → open `Assets/_Project/Scenes/Bootstrap.unity` → Play.

## Controls (default)
| Action | Key |
|---|---|
| Move | WASD |
| Dash | Shift |
| Jump | Space |
| Slide | Left Ctrl |
| Fire | Left mouse |
| Look | Mouse |

## Team
| Name | Area |
|---|---|
| Jake Recharte (Tendie) | Level: rooms, doors. Meta: unlocks, save (temporary) |
| James Shumate (JCoding8127) | Player: movement, Level: procedural generation (temporary) |
| Matthew Nguyen (DiploMatt02) | Player: movement testing and review. UI: HUD, menus, shop UI (temporary) |
| Micheal Saldivar (MSaldivar2005) | Core, Enemies, Combat (temporary) |
| Ross Hacket | Run: timer, difficulty, shop, death and reset (temporary) |

