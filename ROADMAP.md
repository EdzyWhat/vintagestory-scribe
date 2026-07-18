# Roadmap

Scribe grows one tier at a time. Each tier becomes one or more **OpenSpec changes**
(`openspec/changes/`) when we reach it; this file is the high-level map.

The progression axis is **less access friction**: early tools are clunky handheld
objects; late tools make your tasks ambient. It's grounded in the archaeology of
writing *and* vanilla mechanics (you write *into* clay with a stylus — stone age;
you write *on* paper with ink + pen — metal age). Fully unlocked by the early metal
age (the saw); anything past that is cosmetic.

| Tier | Artifact | Capability |
|------|----------|-----------|
| Scratch | Clay tablet (soft/unfired) | 3 lines; wets out if you fall in water holding it |
| Scratch+ | Reed/cattail paper | a few more lines |
| Collection | Leather-bound notebook | infinite pages; built-in implement holder |
| Organization | Writing desk (private block) | consolidates all your notes + categories |
| Portability | Note-taker's backpack + HUD | whole collection on the go; pin ≤3 tasks to HUD |
| Social | Bulletin board (public) + chalkboard | shared board; chalkboard is drawable |

## Staged plan

- **v1 — Lectern slice** *(current)*: one lectern block (reuses the vanilla "Aged book
  lectern" shape) with a task checklist + short note, server-authoritative and
  multiplayer-safe. Goal: something playable to test. Built modularly so later tiers
  slot in without rework.
- **v2 — Notebook (collection):** leather-bound held item, infinite pages. First held
  artifact → introduces the `docId`-on-item store the clay tablet later reuses.
- **v3 — Clay tablet (scratch):** soft/unfired item, clay-color-tinted 3-line UI,
  clayform-a-flat-slab (no firing), stylus in offhand, wets out in water, storable in
  the vanilla Vertical Rack. Plain text entry for now.
- **v4 — Writing desk (organization):** private owner-gated block; consolidates all
  note-items + categories.
- **v5 — Backpack (portability):** hotkey-accessed; pinned-task HUD (≤3 pins).
- **v6 — Bulletin board (social):** public shared block + drawable chalkboard variant.

### Parked (later)

- Stamping mechanic for the clay tablet (custom UI + animation + sound — a large,
  standalone effort).
- Paper progression (reed/papyrus → parchment).
- Location-tagged entries.
- Optional in-game settings panel (ConfigLib, soft dependency).

For the full design record and rationale, see the project plan.
