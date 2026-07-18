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
- Lectern model polish: swap the vanilla book shape for loose-leaf paper + a quill/pen,
  so the model reads as "editing the paper here" rather than managing/taking a book.
- Cross-world export/import (JSON), à la Wanderer's Sketchbook / Frontier's Map.
- Handbook/wiki authoring pass (guides players through the tiers; documents which features
  live on which item/block). Spans all tiers; do near shipping.
- Credit JeanPierre (Wanderer's Sketchbook) in CREDITS — we borrow its data model + GUI ideas.
- Skeuomorphic collapse/expand control: explore making the edit-options toggle a bookmark,
  ribbon, or similar tactile element instead of a plain button (fits the writing-GUI panel).
- Custom fonts via open-source/licensed font faces: **cuneiform-style block letters** for
  the clay tablet's stamped text, and a **rustic/hand-written script** for books/notebooks.
  Purely presentational (render-time font swap per tier); verify license terms allow
  bundling before picking a specific font.
- Localization (`lang/` files beyond `en.json`) for all player-facing GUI text and item/block
  names, so the mod is translatable. Worth setting up the `lang` key structure early (v1) even
  if only English is authored at first, so later strings don't need retrofitting.

### Immersion ideas (curated — see project plan Reference for the full brainstorm)

Grounded, distinctive, and reasonably cheap ideas worth tracking. Ranked roughly by how
directly they connect archaeology/history to actual gameplay (not just flavor):

- **Firing a tablet is a real crafting decision, not just lore.** A completed soft tablet
  can be fired in a kiln to become a **permanent, read-only, indestructible archive** —
  versus staying soft/editable but forever water-fragile. Mirrors the real reason ancient
  tablets survive (accidental/deliberate firing, often in building fires). The single most
  historically-grounded mechanic on this list — a genuine tier trade-off, not a strict
  upgrade.
- **Fire vs. water asymmetric fragility across tiers.** Clay is fireproof but water-fragile
  (already planned); paper/leather should be the inverse — fire-fragile, more water-resistant.
  Keeps later tiers from being strictly "better," which is more interesting design.
- **Death leaves a last entry.** On player death, auto-append a timestamped/located final
  entry to the player's current document. Combined with normal VS item-drop-on-death, this
  turns a dropped notebook into a real in-fiction artifact someone else might find at your
  gravesite. No other VS mod appears to do this — a distinctive hook.
- **Calendar-stamped entries + passive chronicle-building.** Auto-timestamp entries with
  VS's real in-game calendar (seasons/days); auto-log recurring world events (e.g. "survived
  a temporal storm on Day X"); auto-archive completed task lists into a read-only section
  instead of discarding them. All three are cheap and combine into a "journal of your
  journey" that writes itself over a long playthrough.
- **Signed vs. unsigned notes.** Since server-authoritative writes already know the
  player's identity at write time, optionally persist and display it as a signature on
  shared/board notes — attributed vs. anonymous, cheap to add.
- **Guestbook variant of the bulletin board.** A thin, append-only, signed-entry log
  ("visited on day X") distinct from the task-oriented board — cheap once the board's
  shared-doc infrastructure exists.
- **Wax-seal "soft security"** for private documents/notes — tamper is *possible* but
  leaves evidence, rather than a hard lock. Fits VS's grounded-realism ethos better than an
  unbreakable lock. Consider native support or interop with the existing **Envelopes** mod
  (github.com/SiiMeR/vs-envelopes).
- Lower-priority / needs more investigation before committing: handwriting neatening with
  practice (skill curve), item aging/wear visuals, Survival Handbook cross-referencing
  (bookmark a Handbook entry as a task), milestone/tech-unlock auto-suggested tasks (VS has
  no clean achievement-event hook — needs a research spike first).

### UX lessons from PM/notetaking apps (Notion, Todoist, Bullet Journal, GTD, etc.)

The core insight: **capture speed matters more than organization.** The game's own problem
statement (long branching tech tree → distraction) is the same problem GTD calls "open-loop
anxiety" — relief comes from getting a thought out fast, not from a well-organized system.
Concrete, cheap changes this suggests:

- **A dedicated quick-add hotkey** that jots one line without opening the full document —
  likely the single highest-leverage UX investment for a *held* writing item (notebook/
  tablet), more valuable than for a stationary block like the lectern.
- **Sort completed tasks toward the bottom** (or a collapsed "Done (N)" group) using the
  `MoveBlock` primitive we already have — mirrors Kanban's "card leaves the column"
  satisfaction without adding columns/lanes.
- **Reorder as select + step up/down**, not simulated drag — matches `MoveBlock(from, to)`
  directly and is more robust than drag gestures in a small GUI panel.
- **A "carry forward" migration action** for the clay tablet's 3-line cap: copy undone
  tasks into a fresh tablet, clearing the old one — Bullet-Journal-style migration, and a
  natural fit since the tablet's scarcity already forces the "is this still worth keeping?"
  moment.
- **Discipline reminder:** resist adding due dates, priority, or tags as structured
  `ScribeBlock` fields. If wanted, let players encode them as plain-text conventions
  (Bullet-Journal-style signifiers, e.g. a `!` prefix) — zero schema cost, opt-in, and
  avoids the "empty field I feel obligated to fill in" trap that makes task apps feel like
  admin work. A full multi-column Kanban board is a likely mismatch for VS's single-column
  GUI list — skip it; the writing desk's "categories" already covers grouping better.

For the full design record and rationale, see the project plan.
