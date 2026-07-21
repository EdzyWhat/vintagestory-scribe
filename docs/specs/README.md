# Implementation specs (roadmap exploration)

Architect-level implementation specs derived from `ROADMAP.md` (exploration pass,
2026-07-21). Each file is a *design/exploration* document — NOT an OpenSpec change and NOT
implemented code. When a tier or feature is picked up, its spec here becomes the input to a
real `openspec-propose`.

Each spec follows this structure:

- **Summary** — one paragraph: what it is, why, which roadmap item(s) it merges.
- **VS API hooks** — the concrete Vintage Story API surface (types, methods, events,
  block/item behaviors, GUI elements, network channels) the feature needs. Cite where each
  was confirmed (VSAPI-NOTES.md, wiki, anegostudios source, or a decompile finding).
- **C# data structures** — the Core models (game-agnostic; NO VS API) and the Mod-side
  adapters/packets, with field-level sketches. Respect the `src/Core` vs `src/Mod` split.
- **Implementation spec** — the step-by-step approach: block/item defs, GUI composition,
  persistence (Sign pattern), sync, and the ordering of work.
- **Dependencies & sequencing** — what must land first (other tiers, the row-list rework,
  external mods), and where this sits in the staged plan.
- **Open questions** — decisions deferred or needing playtest/user input.

Guardrails these specs must respect (from CLAUDE.md):
- `src/Core/` MUST NOT reference the Vintage Story API.
- Vanilla `VintagestoryAPI` only; ConfigLib is the sole optional soft dependency. No new
  hard mod dependencies without a flagged decision.
- Persistence/sync follows the vanilla Sign block pattern.
