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

- **v1 — Lectern slice** *(current)*: one lectern block (reuses the vanilla
  "lecturn-book-open" shape — plain wood, not an "aged" scavenged variant) with a task
  checklist + short note, server-authoritative and multiplayer-safe. Goal: something
  playable to test. Built modularly so later tiers slot in without rework.
- **v2 — Notebook (collection):** leather-bound held item, infinite pages. First held
  artifact → introduces the `docId`-on-item store the clay tablet later reuses. Two
  decisions carried over from v1 exploration:
  - **A real scrollable/clipped GUI region is a hard prerequisite, not a follow-up.**
    `add-lectern-block` task 8.15 (rows stacked by absolute Y, no scrollbar, currently
    just stopgapped via a text-size cap) must land before "infinite pages" can be an
    honest claim — otherwise content silently renders off-screen past a certain length,
    which is worse than v1's bounded-document problem.
  - **The single-editor lock (server-tracked by block position) likely does not carry
    over.** A held item has no fixed position, and only one player can ever hold a given
    item stack at a time — the contention the lock exists to prevent may not apply to a
    docId-keyed held document. Confirm this explicitly when scoping v2 rather than
    copy-pasting the lock pattern out of habit.
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
- **~~Optional in-game settings panel (ConfigLib, soft dependency)~~ — adopted
  2026-07-19** (`add-imgui-configlib-tuning`): `src/Mod/assets/scribe/config/
  configlib-patches.json` exposes seven `ScribeClientConfig` layout fields
  (`VisibleListHeight`, `RowSpacing`, `TopContentGap`, `ReadListWidth`, `EditorListWidth`,
  `RowDividerThickness`, `RowDividerBrightness`) via the manifest's `"file"` key, which
  reads/writes the existing config file directly — confirmed by decompiling the actually
  installed `configlib_1.12.0.zip` (`ConfigLibModSystem.LoadConfig`), not just the public
  wiki, since the wiki's own JSON-API page doesn't document the `"file"`-key path at all
  (only the asset-patching path). **Correction to this entry's original research: no
  usable NuGet package exists for ConfigLib** — referenced instead via a `HintPath`
  against the DLL vendored from the installed mod `.zip` (`src/Mod/lib/`, see its
  README). Confirmed hard-depends on `vsimgui`, as originally noted.
- **~~ImGui mod (mods.vintagestory.at/imgui) — for live GUI-layout tuning~~ — adopted
  2026-07-19** (`add-imgui-configlib-tuning`): `GuiDialogScribeLectern`'s `#if
  DEBUG`-gated `RegisterDebugSliders()` binds the same seven layout fields via
  `VSImGui.Debug.DebugWidgets.FloatSlider`, letting a developer drag a slider and see the
  dialog recompose live. **Correction to this entry's original research**: no forced
  per-frame recompose is needed — `DebugWidgets` entries are drawn automatically by
  VSImGui's own always-on debug-window handler (`ImGuiModSystem`'s `Draw` event, confirmed
  via decompiling the installed `vsimgui_1.2.7.zip`, not the published (stale, net7.0)
  NuGet package). Excluded from Release builds via a `Configuration == 'Debug'` Condition
  on `Mod.csproj`'s VSImGui `<Reference>` `ItemGroup`, not just `#if DEBUG` at call
  sites — confirmed via build output inspection that no VSImGui/ImGui DLL reaches
  `bin/Release/`.
- **ToastLib (mods.vintagestory.at/toastlib) — investigated as a possible base for the v5
  pinned-task HUD, rejected.** Researched 2026-07-19: stale for 1.22.x (targets
  1.21.1–1.21.5), hard-depends on ImGui, and its API/lifecycle (`ShowToastAdv`, slide-in/
  out, auto-dismiss) is purpose-built for transient messages with no "persist and update
  every tick" primitive — not a fit for an always-on HUD. If/when the v5 HUD gets built,
  go straight to ImGui rather than through this.
- **Custom checkbox visual with stamp/erase animation + sound.** From playtesting
  feedback (2026-07-19): replace the plain checkbox with a custom visual that scales
  with text size (building on the checkbox-scaling fix already shipped), plus a
  satisfying "stamp" animation and sound on check, an "eraser" sound on uncheck, both
  with randomized variations so repeated use doesn't feel mechanical. Purely
  presentational reward-for-completion polish — no data-model changes. Needs actual art/
  audio assets, not just code, so this waits until closer to a polish pass rather than
  competing with core-tier work.
- **Icon-font audit session.** From playtesting feedback (2026-07-19): a dedicated
  session to open the engine's built-in icon-font options for the user, list every icon
  currently in use across the lectern GUI (drag handle, pin, delete, add-task, collapse,
  switch-view), and ask whether any should change. Distinct from a code task — mostly a
  presentation/decision session that may produce small follow-up icon-swap tasks.
- **~~Configurable text-size minimum~~ — done 2026-07-20.** Added
  `ScribeClientConfig.MinTextSizePercent` (default 20) mirroring `MaxTextSizePercent`; both
  the constructor clamp and the slider's floor now read it instead of the old hardcoded 50%.
  Per playtesting feedback the default range was retuned to 20%–120% (`MaxTextSizePercent`
  lowered from 300 to 120) since the user wanted smaller fonts, not larger; both bounds stay
  editable in `scribe-client-config.json`.
- Lectern model polish: swap the vanilla book shape for loose-leaf paper + a quill/pen,
  so the model reads as "editing the paper here" rather than managing/taking a book.
- Freeform text-section blocks (`ScribeBlockKind.Text`, `ScribeDocument.AddTextSection`) are
  reserved for a future item/recipe, not the lectern — the lectern's "Add Note" toolbar
  button was removed deliberately (task 8.18 in `add-lectern-block`); the Core capability
  and `ScribeBlockRowCell`'s rendering of text-section rows are kept fully working so that
  future recipe can reuse them with no Core changes.
- ~~Replace the dynamically-sized GUI rows with a static-sized, skeuomorphic open-book UI
  (fixed page layout, turn-page paging instead of a growing scroll list)~~ — superseded by
  `skeuomorphic-lectern-gui`: delivered a custom-drawn (placeholder-art) backdrop and a
  portrait reshape, but explicitly rejected pagination in favor of a continuous scrollable
  region (design.md decision 4) — the document model's per-row dynamic height and
  drag-reorder made pagination's cost not worth it. See
  https://mods.vintagestory.at/show/mod/42149 for the prior-art reference that motivated
  the backdrop mechanism, still relevant for future real per-tier art.
- Move the editor's option bar (text-size slider, collapse toggle, add-task/add-note, the
  read/edit mode switch button) off the main content area entirely, into a side rail --
  similar to how the vanilla Survival Handbook keeps its category tabs docked to the side of
  the main content pane rather than stacked above/below it. Saves vertical space that
  currently competes with the document's own rows, and groups all chrome in one place.
- Fold the "Switch to Read/Edit" button into the collapse/expand toggle itself (or otherwise
  merge them) to save a row of screen space, once the side-rail move above makes that layout
  decision concrete.
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
  unbreakable lock. **Decided: pursue only if it's a cheap interop** with the existing
  **Envelopes** mod (github.com/SiiMeR/vs-envelopes); otherwise skip or build native later.
  Not urgent.
- **Milestone-suggested tasks — decided: self-detect, no dependency.** When the player
  crosses a tech milestone (e.g. first bronze smelt), surface **one single,
  easily-dismissed suggested task** ("Build a proper forge") — never a queue, never a nag.
  Researched and rejected three third-party trigger sources: **Achievements**
  (mods.vintagestory.at/achievements) has no public API — only an undocumented internal
  attribute found by decompiling its DLL, and a tiny (~2k) install base; **XSkills/XLib**
  has a real API but skill-XP/ability-tiers are a poor fit for discrete milestones, and its
  1.22.x support runs through a community fork-of-a-fork; **Survival Expanded**
  (mods.vintagestory.at/survivalexpanded) is Achievements' own abandoned predecessor — a
  dead end. **Scribe detects vanilla milestones itself** (first bronze/steel/fired-clay,
  etc.) against first-party VS game events/inventory state — zero dependency, works for
  every player. Either mod could later become optional *enrichment* (not the trigger).
  Later/park tier, not v1.
- **Writing desk as faction task-assignment, not just personal organization — needs research
  before committing.** Idea: the v4 writing desk becomes a tool for a faction to assign tasks
  to its members, not just a personal-notes consolidator; faction leaders could lock the
  assignment feature so only they (not any member) can create/edit faction-wide tasks,
  addressing the obvious griefing risk of any member being able to spam/delete others' tasks.
  Pairs naturally with a "pin a task to your own lectern" feature, so a player's personal
  lectern surfaces tasks the faction assigned them -- a real, distinctive throughline from
  personal (lectern) to organizational (desk) tiers. Open question before scoping further:
  vanilla Vintage Story has no first-party faction/guild system -- confirm whether this should
  target an existing faction mod's data (dependency, like the Envelopes-interop idea above) or
  be scoped down to "shared owner list" (a simpler, faction-system-agnostic group of player
  UIDs) instead of true factions. Needs its own research pass before an OpenSpec change.
- **Handbook bookmarking — decided, pursue long-term.** Bookmark a Survival Handbook entry
  into the notebook as a task ("craft this once I have iron"). Acknowledged as the deepest
  API integration on this list (needs Handbook page-ID access) — its own dedicated OpenSpec
  change well after the core tiers exist, not a near-term item.
- **Slack push integration — decided, pursue, one-way only.** A hidden/advanced config
  option to connect a task list to Slack, pushing task-change notifications out. **One-way
  (Scribe → Slack) only** — bidirectional would require the mod to run as a Slack app
  backend (OAuth install, event subscriptions, a public callback endpoint), a much bigger
  lift for little payoff. Mechanism: **Slack Incoming Webhooks**
  (https://docs.slack.dev/messaging/sending-messages-using-incoming-webhooks) — the player
  generates a webhook URL (via a Slack app with the `incoming-webhook` scope), pastes it
  into config, and Scribe does a plain JSON `HTTP POST` on task changes. **The webhook URL
  IS the secret** — no OAuth token handling needed, but it must never be logged or synced.
  Known limits: destination channel/name/icon fixed at webhook creation (not per-message),
  no delete-via-webhook, no published rate-limit number (debounce pushes, don't fire one
  per keystroke). Framed as business/personal research (a hands-on excuse to learn the
  Slack API) — keep it config-gated and undiscoverable by default, not a mainstream
  feature. Later/park tier, not v1.
- Lower-priority / needs more investigation: handwriting neatening with practice (skill
  curve), item aging/wear visuals.

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
- **Reorder via mouse drag**, not select + step up/down. The generic UX advice favors
  step buttons to avoid fiddly drag gestures, but VS itself is heavily mouse-driven
  (crafting grid, blacksmithing, clayforming), so drag-to-reorder is the more consistent
  choice for this game specifically. `MoveBlock(from, to)` in Core doesn't care which
  interaction calls it, so this is purely a GUI-layer decision.
- **Tab / Shift+Tab to save-and-move-focus between rows while editing**, so a keyboard-only
  player can add/edit many rows in a row without reaching for the mouse: Tab commits the
  active row's text and moves focus to the row below (creating a new row at the end if
  already on the last one is a natural extension, not required for a first cut); Shift+Tab
  does the same moving upward. Worth revisiting what other hotkey affordances pair well with
  this once it's built (e.g. Enter to commit-and-stay, Ctrl+Enter to commit-and-add-below) —
  survey this as a small batch rather than one-off requests.
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
