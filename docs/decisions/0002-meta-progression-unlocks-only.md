# 0002: Meta-progression is item-pool unlocks only

- **Status:** Proposed
- **Date:** 2026-11-23
- **Author:**

## Context
Our notes said both "stats fully wipe on death, keep nothing" and "die → permanent item unlock". We needed one rule.

## Decision
Follow the usual roguelike model:
- **Run state** (stats, attachments, currency, timer, stage) is wiped completely on death.
- **Meta state** is only a saved set of unlocked item IDs. Unlocks add items to the pool that can drop or appear in the shop. They never raise starting power.
- Meta lives in its own `Roguelike.Meta` module behind `IUnlockService` in Core.

## Consequences
- Every run starts equally strong, so balancing stays simple.
- Every `AttachmentDefinition` needs a stable `Id` and an `UnlockedByDefault` flag.
- We need a save file (JSON in `persistentDataPath`) from M2 or M3 onward.
- Unlock conditions are still TBD (DESIGN.md 6.4).
