# 0001: Modular architecture enforced by Assembly Definitions

- **Status:** Proposed
- **Date:** 2026-11-23
- **Author:**

## Context
Five developers working in parallel tend to drift: each person invents their own managers and singletons, and modules start depending on each other in tangled ways.

## Decision
- Split code into modules (Core, Player, Combat, Enemies, Level, Run, UI, Bootstrap), each with its own `.asmdef`.
- Feature modules reference only Core. Bootstrap references everything.
- Cross-module communication uses Core interfaces, `EventBus<T>`, and `Services`. No singletons.
- Tunable content uses ScriptableObject Definitions.

## Consequences
- The compiler rejects illegal cross-module references.
- Core becomes a bottleneck, so changes to it need 2 approvals and should be batched early (M0/M1).
- Some indirection (an event or interface) is needed even for simple interactions.
