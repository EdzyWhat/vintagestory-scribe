# Roadmap

Scribe grows one tier at a time. Each tier becomes one or more **OpenSpec changes**
(`openspec/changes/`) when we reach it; this file is the high-level map.

Detailed architect-level implementation specs for the tiers and feature clusters below now
live in **`docs/specs/`** (written 2026-07-21). Each links its VS API hooks, C# data
structures, and sequencing. When a tier is picked up, its spec is the input to an
`openspec-propose`. This file stays the map; the specs hold the "how."

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

- **v1 — Lectern slice** *(current)*: one lectern block (reuses the vanilla
  "lecturn-book-open" shape — plain wood) with a task checklist + short note,
  server-authoritative and multiplayer-safe. Built modularly so later tiers slot in. The
  **row-list rework** (S1 shipped, S2 = `lectern-edit-in-place-rows` proposed) is finishing
  the GUI onto a single custom-drawn row element — this is a hard prerequisite for the held
  tiers below (a real clipped/scrollable region + a unified read/edit renderer).
- **v2 — Notebook (collection)** → `docs/specs/v2-notebook.md`. Leather-bound held item,
  infinite pages. Introduces the **`docId`-on-item + server-side document store** that v3
  reuses. Two carried-over decisions are now **resolved** in the spec: the scroll/clip
  prerequisite is delivered by the row-list rework (v2 must wait for **S2**); and the
  single-editor lock does **not** carry over (a held stack has one holder — matches vanilla
  `ItemBook`, which uses no lock).
- **v3 — Clay tablet (scratch)** → `docs/specs/v3-clay-tablet.md`. Soft/unfired item, 3-line
  UI, clayform-a-flat-slab, stylus in offhand, wets out in water, rack-storable. Mostly JSON
  (clayforming pattern, `dissolveInWater`, `scrollrackable`, stylus = `writingTool` item). The
  fire-to-permanent-archive trade-off has a real gotcha (kiln firing drops stack attributes —
  see the spec and VSAPI-NOTES).
- **v4 — Writing desk (organization)** → `docs/specs/v4-writing-desk.md`. Private owner-gated
  block; consolidates notes + categories; **kanban tabs** (Active / Backlog / Completed) as
  the fuller home for the completed-task funnel. Also the home for the **faction/shared
  task-assignment** idea — VS ships a first-party player-group system, so this may need no
  external dependency (see open decision below).
- **v5 — Backpack (portability)** → `docs/specs/v5-backpack-hud.md`. Hotkey-accessed;
  always-on **pinned-task HUD** (≤3 pins, native `HudElement` — not ImGui); plus a
  **quick-add hotkey** for one-line capture without opening the full document.
- **v6 — Bulletin board (social)** → `docs/specs/v6-bulletin-board.md`. Public shared block +
  signatures + a guestbook (append-only) variant. The **drawable chalkboard** is recommended
  as a v6.1 sub-change (no vanilla drawable precedent → from-scratch stroke GUI).

## Near-term, actionable (not tied to a future tier)

- **BUG — editor view doesn't auto-close on walk-away.** Playtest 2026-07-21 (TESTING.md
  `9c04c5c7` / add-lectern-block 7.8): opening the editor and walking hundreds of blocks away
  never closes the dialog; expected behavior is auto-close + force-flush the pending edit. The
  range-check-and-close appears not to fire. A distinct defect in the existing dialog
  lifecycle — not row-list-rework scope. Needs a code fix, then a retest of close + flush.
- **Lectern GUI polish** → `docs/specs/lectern-gui-polish.md`. Merges: face-the-player on
  placement, "Edit" → "Edit Tasks" relabel, damped icon-gutter widths at large text size,
  the side-rail option bar + fold-switch-into-toggle + skeuomorphic collapse control chain,
  the loose-leaf-paper model swap, and the icon-font audit. The relabel and the placement
  facing are **trivial and have zero row-list-rework dependency** — candidates to pull out as
  a quick standalone change now (see open decision). The gutter-width item rides S2.

## Presentation & polish (deferred, mostly asset-gated)

→ `docs/specs/presentation-and-fonts.md`. Merges the checkbox stamp/erase animation
(this is **S4** of the row-list rework — the seam already exists in
`ScribeRowElement.DrawCheckboxGlyph`), the smooth drag-reorder preview animation (**S3**;
supersedes the on-hold `lectern-drag-reorder-feedback` change's "rows don't shift" non-goal),
custom per-tier fonts (cuneiform for the tablet, rustic script for books — loadable via
FreeType, gated on a license check), and lightly-scoped handwriting-skill / item-aging visuals.
All render-only; several need art/audio assets before they can start.

## Chronicle & integrations (later)

→ `docs/specs/chronicle-and-integrations.md`. Merges: death-leaves-a-last-entry,
calendar-stamped entries + passive chronicle-building, milestone-suggested tasks (self-detected
via inventory polling — zero dependency; three third-party trigger mods were researched and
rejected), one-way Slack push via Incoming Webhooks (config-gated, undiscoverable, secret never
logged/synced), and long-term Handbook bookmarking. Plus program-level meta workstreams:
cross-world JSON export/import, localization (`lang/` beyond `en.json` — start the key structure
early), a handbook/wiki authoring pass near shipping, and crediting JeanPierre (Wanderer's
Sketchbook) in CREDITS.

## Immersion ideas (curated — see project plan Reference for the full brainstorm)

Grounded, distinctive ideas worth tracking. The clay-tier and social-tier ones are folded into
their specs above (`v3-clay-tablet.md`, `v6-bulletin-board.md`); the chronicle-style ones into
`chronicle-and-integrations.md`. Still-open highlights:

- **Firing a tablet is a real crafting decision** — a fired tablet becomes a permanent,
  read-only archive vs. staying soft/editable but water-fragile. The single most
  historically-grounded mechanic on the list (v3 spec).
- **Fire vs. water asymmetric fragility across tiers** — clay is fireproof but water-fragile;
  paper/leather should invert this. Keeps later tiers from being strictly "better."
- **Death leaves a last entry**, **calendar-stamped passive chronicle**, **signed vs. unsigned
  notes**, **guestbook board variant**, **milestone-suggested tasks** — all specced (see
  chronicle + v6 specs).
- **Wax-seal "soft security"** — **decided: not via Envelopes** (that mod is items-only with no
  API to seal a Scribe block/docId). Build native later or skip. Not urgent.
- **Writing desk as faction task-assignment** — folded into `v4-writing-desk.md`; the
  faction-backing choice is an open decision below.
- **Handbook bookmarking** and **Slack push** — both decided-pursue, both in the chronicle spec;
  late-stage.
- Lower-priority / needs investigation: handwriting neatening with practice (skill curve),
  item aging/wear visuals.

### UX lessons from PM/notetaking apps (Notion, Todoist, Bullet Journal, GTD, etc.)

The core insight: **capture speed matters more than organization** — the game's "long branching
tech tree → distraction" problem is GTD's "open-loop anxiety," relieved by getting a thought out
fast. Concrete, cheap directions this suggests (several now folded into specs):

- **A dedicated quick-add hotkey** — highest-leverage UX investment for a held writing item.
  Specced in `v5-backpack-hud.md`.
- **Sort completed tasks toward the bottom** (or a collapsed "Done (N)" group) via the existing
  `MoveBlock` primitive — the lighter cousin of the v4 kanban funnel.
- **Reorder via mouse drag** (already shipped) over select+step buttons — VS is heavily
  mouse-driven, so drag is the consistent choice.
- **Tab / Shift+Tab / Enter to save-and-move-focus between rows** — being delivered by S2
  (`lectern-edit-in-place-rows`); survey adjacent hotkey affordances (Ctrl+Enter to
  commit-and-add-below, etc.) as a small batch once it's built.
- **A "carry forward" migration** for the clay tablet's 3-line cap (Bullet-Journal-style) — a
  Core op, specced in `v3-clay-tablet.md`.
- **Discipline reminder:** resist due dates / priority / tags as structured `ScribeBlock`
  fields; let players encode them as plain-text conventions (e.g. a `!` prefix) — zero schema
  cost, opt-in. A full multi-column Kanban is a mismatch for VS's single-column GUI; the desk's
  categories/tabs cover grouping better.

For the full design record and rationale, see the project plan.

## Open decisions (surfaced by the 2026-07-21 exploration; carried into each spec)

These are the cross-cutting forks the specs couldn't settle without you. They don't block the
specs (each documents its assumed default) but they shape sequencing and scope:

1. **v4 faction-backing** — **DECIDED 2026-07-21: defer.** Ship the personal writing desk first;
   leave faction backing (built-in player groups vs. shared owner-UID list vs. third-party mod)
   as an open question until v4 is actually scoped. The player-group finding (VSAPI-NOTES) means
   the no-dependency path exists whenever we return to it.
2. **Lectern-polish quick wins** — **DECIDED 2026-07-21: hold with the polish cluster**, not a
   standalone change. Reason: the "Edit"/"Edit Tasks" relabel opens a larger question — use
   **icons instead of text** for these labels/controls, which requires first auditing the
   built-in icon font (see the lectern-gui-polish spec's icon-font audit — note there's NO
   built-in pin or grip glyph, so custom-drawn SVGs may be needed) and deciding built-in vs.
   custom-drawn. So the relabel + placement facing wait until that icon direction is settled
   rather than shipping text labels we'd then replace.
3. **v5 HUD pin scope** — when the source document is on an item you're NOT holding, do pinned
   tasks still show (needs a server-pushed "my pins" summary) or only the currently-held
   document's pins?
4. **Notebook document store** — one shared `"scribe:doc:"` store across notebook + desk +
   tablet, or separate stores? And duplicate the lectern's packets for the docId-keyed path
   (leaves v1 untouched) vs. generalize them (DRYer, changes v1's wire format)?
5. **Public board concurrency & signatures** (v6) — keep a lectern-style single-editor lock or
   go lock-free last-write-wins; and are signatures always-on, per-entry toggle, or a
   board-level policy set at placement?

### Done / superseded (kept only as history)

- **Optional in-game settings panel (ConfigLib, soft dep)** — adopted 2026-07-19
  (`add-imgui-configlib-tuning`).
- **ImGui debug-tuning overlay** — adopted 2026-07-19 (`add-imgui-configlib-tuning`); Debug-only,
  Release-excluded, and can't render on Apple Silicon (see VSAPI-NOTES).
- **ToastLib for the HUD** — investigated and rejected 2026-07-19 (stale for 1.22.x, ImGui
  dep, no persist-and-update primitive). v5 uses a native `HudElement` instead.
- **Configurable text-size minimum** — done 2026-07-20 (`MinTextSizePercent`; range retuned to
  20%–120%).
- **Static open-book / turn-page pagination UI** — superseded by `skeuomorphic-lectern-gui`,
  which kept a continuous scrollable region (see that change's design.md decision 4). The mod
  portal reference that motivated the backdrop art: https://mods.vintagestory.at/show/mod/42149.
- **Freeform text-section blocks** (`ScribeBlockKind.Text`, `ScribeDocument.AddTextSection`) are
  reserved for a future item/recipe, not the lectern (the "Add Note" button was removed in
  add-lectern-block task 8.18); the Core capability + `ScribeBlockRowCell` text-row rendering are
  kept working so a future recipe can reuse them with no Core changes.
