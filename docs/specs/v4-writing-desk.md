# v4 — Writing desk (organization tier)

> **Status:** design/exploration spec (2026-07-21). NOT an OpenSpec change, NOT implemented
> code. When v4 is picked up, this file becomes the input to `openspec-propose`. See
> `docs/specs/README.md` for the format and the guardrails these specs must respect.

## Summary

The **writing desk** is the organization-tier block (`ROADMAP.md`): a **private, owner-gated**
block that consolidates all of a player's notes/tasks into one richer home than the lectern.
This spec merges five roadmap items into one coherent design:

1. **Writing desk** — private owner-gated block (organization tier).
2. **Kanban in the desk UI** — Active / Backlog / Completed sections (tabs or columns), with a
   move-between-sections interaction, reusing the row-list-rework shared renderer
   (`ScribeRowElement` + `RowTextLayout`).
3. **Funnel completed tasks into a Done view** — completed tasks optionally leave the working
   list and surface in a dedicated view; the "hide completed from read/edit lists" behavior is
   a toggle exposed **only in the editor view's options** (never read view). At the desk tier
   this becomes the "Completed" column of the board; the lectern (v1) gets only a lighter
   hide-completed toggle.
4. **Sort completed toward the bottom / "Done (N)" group** — a cheaper presentation-only
   alternative to full funneling, built on the existing `MoveBlock` primitive.
5. **Writing desk as faction task-assignment** (the big research item) — the desk becomes a
   tool for a group to assign tasks to members, with group leaders able to lock the assignment
   feature; it pairs with "pin a faction-assigned task to your own lectern".
6. **Search / find within the desk document** — a text-search box that filters the desk's row
   list to tasks/notes whose text matches a typed query. The desk is the tier where a player
   first has enough consolidated content that scrolling stops scaling and *finding* a note
   matters. **Within-document scope** (the currently-open desk's own blocks) — deliberately the
   cheap version: it reuses the exact **filtered row list** the kanban tabs already use (item 6
   under Implementation phases below / the `where <predicate>` filter), swapping the section
   predicate for a case-insensitive text-match on `ScribeBlock.Text`. No Core change, no
   server/network change — it's the same client-side "filter the shared `ScribeRowElement` list,
   map filtered row → true block index for mutations" mechanism. **Global cross-document search**
   ("where did I write that?" across *all* your documents / bound desks) is explicitly OUT of
   scope here — that needs a server-side cross-document index/scan, the same class of problem as
   the item-14 "find faction tasks assigned to me" query, and should get its own later change if
   wanted. Search composes with the kanban tabs (search within the active tab's filtered set) and
   with the hide-completed toggle.

The central research finding (see VS API hooks below) reshapes item 5: **Vintage Story ships a
first-party player-group system** (`ICoreServerAPI.Groups` / `IGroupManager`, with
Owner/Op/Member roles). So the faction feature needs **no third-party mod dependency and no
invented "shared owner-UID list"** — it can ride VS's own groups. This is the headline decision
for the user (see Open questions).

This is a large tier. It is deliberately specced so the **personal writing desk (items 1–4)
can ship first** and the **group/faction layer (item 5) is a strictly additive follow-on** that
does not block the tier.

---

## VS API hooks

### Owner-gated private block

There is **no existing owner-gating in this codebase** — the lectern (v1) is a public block
keyed only by position, gated solely by the single-editor lock (`lockHolderUid` in
`BlockEntityScribeLectern`, which is transient session state, not persisted ownership). So the
desk introduces the first *persisted* ownership.

- **Ownership model to follow:** store an `ownerUid` string in the block entity's tree
  attributes (Sign pattern — `ToTreeAttributes`/`FromTreeAttributes`), stamped on placement from
  the placing player. Confirmed the placing player is available in the block-placement path:
  `BlockScribeLectern.OnBlockInteractStart(world, byPlayer, blockSel)` already receives
  `IPlayer byPlayer`; block placement itself is `Block.DoPlaceBlock` / `TryPlaceBlock` on the
  server with `byPlayer` in scope. Read `byPlayer.PlayerUID` there and persist it on the new
  block entity. (`IServerPlayer.PlayerUID` confirmed via decompile — the field the lock already
  uses.)
- **Gate on interaction, server-authoritative:** `OnBlockInteractStart` runs on both sides;
  the *server* decides access (mirroring the existing `RequestAccess` flow) and refuses a
  non-owner. Refusal reuses the existing `ScribeEditDocumentMessage.RefusalReason` +
  `capi.TriggerIngameError` path already wired for lock refusals. No new refusal mechanism.
- **Vanilla precedent for private/owned blocks:** the vanilla **land-claim** system
  (`Vintagestory.API.Common.LandClaim`, decompiled) gates block *use/break* via
  `PermittedPlayerUids` (a `Dictionary<string, EnumBlockAccessFlags>`) and
  `PermittedPlayerGroupIds` (`Dictionary<int, EnumBlockAccessFlags>`). We do **not** need land
  claims for the desk — we own the block entity and can gate interaction ourselves on
  `ownerUid` — but `LandClaim`'s shape is direct precedent that "owner UID + permitted-UID set +
  permitted-group-id set" is exactly how the engine itself models shared block ownership. That
  validates both the shared-owner-list fallback *and* the player-group path below.

### Player-group system (the faction finding — confirmed by decompile)

**Vintage Story has a first-party player-group system.** The roadmap's premise ("VS has NO
first-party faction/guild system") is only half right: there is no *faction/guild/territory*
concept, but there **is** a persisted, role-bearing player-group system (it backs the in-game
chat groups). Confirmed by decompiling `VintagestoryAPI.dll`:

- **`ICoreServerAPI.Groups` → `IGroupManager`** (`Vintagestory.API.Server`):
  - `Dictionary<int, PlayerGroup> PlayerGroupsById { get; }`
  - `PlayerGroup GetPlayerGroupByName(string name)`
  - `void AddPlayerGroup(PlayerGroup)` / `void RemovePlayerGroup(PlayerGroup)`
- **`PlayerGroup`** (`Vintagestory.API.Server`): `int Uid`, `string Name`, `string OwnerUID`,
  `string JoinPolicy`, `List<IPlayer> OnlinePlayers`, plus chat-history fields we ignore.
- **`IPlayer.Groups` → `PlayerGroupMembership[]`**, plus `GetGroups()` and
  `GetGroup(int groupId)`. **`PlayerGroupMembership`** (`Vintagestory.API.Common`):
  `EnumPlayerGroupMemberShip Level`, `string GroupName`, `int GroupUid`.
- **`EnumPlayerGroupMemberShip`**: `None, Member, Op, Owner` — **this gives us the
  "leaders lock the assignment feature" role distinction for free** (Owner/Op = leader,
  Member = assignee).
- Server-side membership is also on `IServerPlayer.ServerData.PlayerGroupMemberships`
  (`Dictionary<int, PlayerGroupMembership>`).

Implication: a "faction desk" can be modeled as *a desk bound to a `PlayerGroup.Uid`*, with
assignment gated by the interacting player's `EnumPlayerGroupMemberShip` in that group — **zero
new dependencies, zero invented group primitive.** The main caveat is that VS player groups are
UI-surfaced as *chat* groups (created via `/group` chat commands), so they read as "chat rooms"
to players, not "factions" — acceptable, but worth the user's awareness (Open questions).

### Larger GUI (tabs / columns)

- **Tabs exist as first-party composer helpers** (confirmed via decompile of
  `GuiComposerHelpers`):
  - `AddHorizontalTabs(GuiTab[] tabs, ElementBounds, Action<int> onTabClicked, CairoFont, CairoFont selectedFont, string key)`
  - `AddVerticalTabs(...)` and `AddVerticalToggleTabs(...)`
  - **`GuiTab`** (`Vintagestory.API.Client`): `int DataInt`, `string Name`, `double PaddingTop`,
    `bool Active`. The Survival Handbook's category rail (referenced in ROADMAP) uses the
    vertical-tab pattern — direct precedent for a docked section switcher.
- **Row list / shared renderer:** the desk reuses `ScribeRowElement` (custom `GuiElement` that
  bakes its own texture and blits it in the interactive pass, natively clipped inside
  `BeginClip`), `RowTextLayout` (single layout authority), and `ScribeRowListScrollbar`. All the
  hard-won scroll/clip lessons in `VSAPI-NOTES.md` ("TWO passes with TWO Y coordinates",
  cull-vs-clip, drag handoff across recompose) apply unchanged. **This is why the desk depends on
  the row-list-rework landing first** (S1 read view is archived; S2 edit-in-place is in flight).

### Persistence / sync

Unchanged from v1: `ToTreeAttributes`/`FromTreeAttributes`, `SendBlockEntityPacket`/`MarkDirty`,
server-authoritative, via the existing `ScribeDocumentCodec` byte format (versioned magic-header
codec). New persisted state (owner UID, optional group binding, per-section membership) is added
to the codec with a **version bump** (codec is at `Version = 3`; the reader rejects mismatched
versions today, so a v4 bump needs a migration branch — see Implementation).

---

## C# data structures

### Core (game-agnostic — MUST NOT reference the VS API)

The existing Core model is already remarkably close. Key existing facts:

- `ScribeBlock` already has `bool Done`, `bool Pinned`, and — critically —
  **`string? AssignedToUid`** (currently reserved/unread, per its doc comment: *"Reserved for a
  future assignment capability (player/group UID). Unset by default; no mutation method exists
  yet."*). The codec already round-trips it (v3). **v4 is what that field was reserved for.**
- `ScribeDocument` already has `ToggleTask`, `MoveBlock`, `DeleteBlock` — the primitives the
  kanban/sort/funnel features build on.

**Design question 1 — does "Done" drive funneling, or is a separate completed-collection
needed?** Recommendation: **`Done` is sufficient; do NOT add a separate completed-collection.**
Rationale:

- A "column" (Active / Backlog / Completed) is a *derived view over the single ordered block
  list*, not a separate stored list. Completed = `IsTask && Done`. This keeps `MoveBlock` (which
  operates on the one list) working for both reorder and the "sort completed toward the bottom"
  UX with no new mutation path.
- The only distinction "Active" vs "Backlog" needs that `Done` can't express is a
  **not-done-but-parked** state. That is one new orthogonal flag, not a new collection. Model it
  as a small enum on the block rather than a second list, so a task is always in exactly one
  section and `MoveBlock`/`ToggleTask` semantics are preserved:

```csharp
// src/Core/ScribeBlock.cs — additive
public enum ScribeTaskLane : byte   // persisted as a byte; append-only, never renumber
{
    Active    = 0,   // default — matches today's behavior (all tasks are "active")
    Backlog   = 1,
    // NOTE: "Completed" is NOT a lane value — completion is the existing `Done` flag, so a
    // task can be Done in either Active or Backlog and the Completed view is `where Done`.
    // Keeping completion orthogonal to lane avoids a task being "in Completed but not Done"
    // or vice-versa (the desync a separate collection would invite).
}

public sealed class ScribeBlock
{
    // ... existing fields (Kind, Text, Done, Depth, Pinned, AssignedToUid) ...
    public ScribeTaskLane Lane { get; set; } = ScribeTaskLane.Active;  // NEW, default Active
}
```

Section membership rule (a pure Core function, unit-testable):
- **Completed** section = `IsTask && Done` (regardless of `Lane`).
- **Active** section = `IsTask && !Done && Lane == Active`.
- **Backlog** section = `IsTask && !Done && Lane == Backlog`.
- Text sections (`Kind == Text`) are lane-agnostic; the desk shows them in a place TBD (Open
  questions) — simplest is "Active only" or a dedicated Notes tab.

Core mutation methods to add (mirroring the existing `Toggle*`/`Move*` style — return `bool`,
never throw):

```csharp
public bool SetLane(int index, ScribeTaskLane lane);          // Active <-> Backlog move
public bool AssignTask(int index, string? uidOrGroupRef);     // writes AssignedToUid; null clears
```

`MoveBlock(from, to)` stays the single reorder primitive; "sort completed to bottom" is
implemented as a Core helper that computes a stable target order and calls `MoveBlock` (or a new
`SortCompletedToBottom()` convenience that reuses the same list ops) — no new storage.

**The shared-owner-UID list, modeled Core-side without factions.** Even though VS has a group
system, keep the *authorization data the Core reasons about* faction-agnostic, so Core never
needs a VS concept. Model it as a plain set of strings plus an optional group binding:

```csharp
// src/Core/ScribeAccess.cs (new) — pure data, no VS types
public sealed class ScribeAccess
{
    public string? OwnerUid { get; set; }                 // the placer; null until placed
    public HashSet<string> SharedOwnerUids { get; } = new();   // faction-agnostic co-owners
    public int? BoundGroupUid { get; set; }               // optional: a VS PlayerGroup.Uid
    public bool AssignmentLocked { get; set; }            // leaders-only-assign toggle
}
```

- Core stores `BoundGroupUid` as an opaque `int?` and **never resolves it** — resolving a group
  id to a membership/role is a Mod-side concern (it needs `IPlayer.GetGroup`). Core only answers
  faction-agnostic questions like "is this UID the owner or a shared owner?".
- This is the crucial split: **the "shared owner-UID list" and the "VS player-group binding" are
  not either/or — they coexist.** `SharedOwnerUids` is the dependency-free baseline (works with
  zero groups); `BoundGroupUid` is the optional enrichment that lets the Mod layer expand
  membership/roles from VS groups at interaction time. The user's decision (dependency vs shared
  list) collapses to *"do we wire the optional `BoundGroupUid` resolution in v4, or ship only
  `SharedOwnerUids` first?"* — and neither adds a mod dependency.

### Mod (adapter — `src/Mod/`)

- **`BlockEntityScribeWritingDesk`** (new; parallels `BlockEntityScribeLectern`): holds a
  `ScribeDocument` + a `ScribeAccess`, persists both via the codec (version-bumped). Server-side
  interaction decision extended: on right-click, resolve the interacting player's authorization
  from `ScribeAccess` **plus**, if `BoundGroupUid` is set, their `IPlayer.GetGroup(uid)?.Level`
  (Owner/Op/Member) to decide read vs edit vs assign.
- **`BlockScribeWritingDesk`** (new; parallels `BlockScribeLectern`): thin; stamps `OwnerUid`
  from `byPlayer.PlayerUID` on placement, forwards interaction to the block entity.
- **`GuiDialogScribeWritingDesk`** (new, or a generalization of `GuiDialogScribeLectern`): adds a
  tab/column section switcher over the **same** `ScribeRowElement` row list. Reuses
  `RowTextLayout`, `ScribeRowListScrollbar`, the `BeginClip` idiom, and the read/edit
  `ScribeRowMode`.
- **Packets:** reuse `ScribeEditDocumentMessage` (full-doc, lock-gated) for bulk edits;
  reuse/extend `ScribeToggleTaskMessage` for lock-free done-toggles. Add narrow messages
  following the same one-field-patch pattern the toggle established:
  - `ScribeSetLaneMessage { Pos, BlockIndex, byte Lane }` (move between Active/Backlog).
  - `ScribeAssignTaskMessage { Pos, BlockIndex, string AssigneeUid }` (faction layer only;
    server rejects unless the sender is owner/Op or `!AssignmentLocked`).

---

## Implementation spec

Sequenced so the personal desk is shippable before the faction layer.

### Phase A — Personal writing desk (block + ownership)
1. **Core:** add `ScribeTaskLane`, `ScribeBlock.Lane`, `ScribeAccess`, `SetLane`, and the
   section-membership query helpers. Add unit tests (`tests/Core.Tests`) for lane transitions,
   the completed-section derivation, and codec round-trip. **No VS references.**
2. **Codec:** bump `ScribeDocumentCodec.Version` to 4; write `Lane` per block and the
   `ScribeAccess` block-level fields. **Add a migration branch:** the reader currently rejects
   any non-current version (`if (version != Version) return false;`). Change it to accept v3 by
   defaulting `Lane = Active` and an empty `ScribeAccess`, so existing lecterns/saves survive.
   (This is a real behavior change to a load-bearing method — call it out in the OpenSpec
   proposal.)
3. **Block/BE:** create `BlockScribeWritingDesk` + `BlockEntityScribeWritingDesk` by cloning the
   lectern pair. Stamp `OwnerUid` on placement. Gate `OnBlockInteractStart` server-side: only the
   owner (Phase A) may open the desk at all; refuse others via the existing refusal path. Persist
   `ScribeAccess` alongside the document.
4. **Asset:** new `blocktypes/writingdesk.json` (a desk shape — reuse a vanilla clutter shape as
   the lectern did; the ROADMAP notes loose-leaf/quill model polish is a later cosmetic item).
   Register the new block/BE classes in `ScribeModSystem.Start`.

### Phase B — Kanban sections in the desk UI
5. **GUI shell:** add a section switcher. **Recommendation: tabs, not side-by-side columns** —
   VS's GUI + the row-list renderer are single-column by construction, and the ROADMAP's own PM
   analysis explicitly warns "a full multi-column Kanban board is a likely mismatch for VS's
   single-column GUI list." Use `AddVerticalTabs` (Handbook-style docked rail; also frees the
   vertical space the ROADMAP wants reclaimed) or `AddHorizontalTabs` across the top. Tabs:
   **Active / Backlog / Completed** (+ optionally **Notes** for text sections).
6. **Filtered row list:** each tab composes the same `ScribeRowElement` list filtered to that
   section's blocks (map filtered row → true block index for mutations). No new renderer.
7. **Move-between-sections interaction:** a per-row control (or drag onto a tab) sends
   `ScribeSetLaneMessage` (Active↔Backlog) or toggles `Done` (→ Completed) via the existing
   toggle message. Completing a task in Active/Backlog makes it *leave that tab and appear in
   Completed* — the "card leaves the column" satisfaction the ROADMAP calls out — with no
   separate collection, because Completed is a derived view.

### Phase C — Funnel completed + lectern's lighter toggle
8. **Desk (fuller home):** the Completed **tab** *is* the funnel — completed tasks are absent
   from Active/Backlog by the section-membership rule, present in Completed. No toggle needed at
   the desk; the board structure does it.
9. **Lectern (lighter):** add a **"hide completed" toggle exposed only in the editor view's
   options bar** (the `ToolbarOptions()` iterator in `GuiDialogScribeLectern`, where add-task /
   collapse already live) — never in read view, per the requirement. When on, the read+edit row
   lists filter out `Done` tasks. This is presentation-only client state (a
   `ScribeClientConfig` bool, or per-block-entity synced state — Open question) and needs **no
   Core change**: the filter is `where !(IsTask && Done)`.
10. **Cheaper alternative (either/or with the toggle, or as well):** "Sort completed toward the
    bottom" / a collapsed **"Done (N)"** group, implemented purely via `MoveBlock` (or the Core
    `SortCompletedToBottom` helper) — no schema change. Good for the lectern where a full tabbed
    board is overkill.

### Phase D — Faction/group task-assignment (additive, gated behind the user's decision)
11. **Bind a desk to a group (optional):** owner sets `ScribeAccess.BoundGroupUid` from their own
    `IPlayer.Groups` (a dropdown of groups they're Owner/Op of). Persist it.
12. **Assignment:** an "assign" affordance on a task writes `AssignedToUid` (a player UID drawn
    from the bound group's members) via `ScribeAssignTaskMessage`. Server gates: allowed if the
    sender is the desk owner, or the sender's `GetGroup(BoundGroupUid).Level` is Owner/Op, or
    `!AssignmentLocked`. This is the **"leaders lock the assignment feature"** requirement,
    satisfied by VS's own Owner/Op/Member roles.
13. **Read access for members:** when `BoundGroupUid` is set, group members (not just the owner)
    may open the desk in read view and see tasks assigned to them — gate the Phase A owner-only
    check on group membership too.
14. **Pin faction task to your own lectern (pairs with v1):** a player's personal lectern surfaces
    tasks where `AssignedToUid == self` from desks bound to their groups. This needs a
    server-side cross-block query ("find desks bound to groups I'm in with tasks assigned to me")
    — a real design sub-problem (indexing/scanning) that should get its **own** OpenSpec change;
    flag it as out of scope for the first desk change.

---

## Dependencies & sequencing

- **Hard prerequisite: the row-list-rework shared renderer.** The desk's kanban list *is*
  `ScribeRowElement` + `RowTextLayout` + `ScribeRowListScrollbar`. S1 (read view) is archived;
  **S2 (`lectern-edit-in-place-rows`) must land** so the desk inherits a unified read/edit row
  element rather than forking the old editor path. S3 (drag feedback) and S4 (checkbox animation)
  are nice-to-have, not blocking. Do **not** start the desk GUI before S2 merges to `main`.
- **Relationship to the lectern's lighter hide-completed toggle:** the desk (Phase B/C) is the
  *fuller* home (Active/Backlog/Completed tabs); the lectern gets only the Phase C step-9 toggle.
  These can be built in either order, but building the lectern toggle first is a cheap way to
  validate the `where !Done` filter and the "editor-options-only" placement before the desk's
  bigger UI. The **Funnel-completed** ROADMAP item and this spec should be reconciled: this spec
  is that item's fuller design.
- **Tier order:** v4 follows v2 (notebook) and v3 (clay tablet) in the ROADMAP. The desk
  "consolidates all note-items + categories," so it benefits from v2's `docId`-on-item store
  existing — but the *block itself* (position-keyed, like the lectern) can be built without it;
  "consolidation across items" is the part that leans on v2. Scope the first desk change to a
  self-contained block (its own document) and treat cross-item consolidation as a follow-on.
- **No new mod dependency.** Vanilla `VintagestoryAPI` only. The faction layer rides VS's
  built-in `IGroupManager`/`PlayerGroup`. ConfigLib remains the sole optional soft-dep (the
  lectern's hide-completed toggle default could be a ConfigLib knob, consistent with the existing
  seven layout fields).
- **Persistence invariant:** the codec version bump + v3→v4 migration branch is the one
  load-bearing change; everything else follows the Sign pattern already in place.

---

## Open questions

- **"Active vs Backlog" — is the second lane worth it, or is Active/Completed enough for v1 of
  the desk?** Adding `ScribeTaskLane` is cheap but introduces a "parked" state players must
  understand. Could ship Completed-only first (pure `Done` derivation, zero Core enum) and add
  Backlog later.
- **Text sections in a tabbed desk:** do notes get their own "Notes" tab, live only in Active, or
  appear in every tab? (Leaning: dedicated Notes tab, since notes are lane-agnostic.)
- **Lectern hide-completed toggle — client-local or synced?** A `ScribeClientConfig` bool is
  per-player and needs no sync (each viewer chooses); a block-entity flag is shared and synced
  (the desk/lectern owner sets it for everyone). The requirement ("toggle in editor options")
  fits either; per-player client state is simpler and avoids a new synced field.
- **Cross-block "pin faction task to my lectern" (Phase D step 14):** needs a server-side index
  of group-bound desks; deferred to its own OpenSpec change. Confirm it's out of scope for the
  first desk change.

## Clarifying questions for the user

1. **Faction backing — the headline decision.** VS ships a first-party player-group system
   (`ICoreServerAPI.Groups`, with Owner/Op/Member roles) that needs **no mod dependency** and
   directly satisfies "leaders lock assignment." Given that, do you want to (a) build the faction
   layer on VS's built-in groups (recommended — no dependency, real roles, but groups read to
   players as *chat groups* created via `/group`), (b) ship only the dependency-free
   `SharedOwnerUids` co-owner list now and defer any group binding, or (c) still target a
   third-party faction mod? My strong recommendation is (a) or (b); (c) reintroduces a hard
   dependency the guardrails resist.
2. **Lectern hide-completed toggle — now or later?** Should the lighter "hide completed from
   read/edit lists" toggle (editor-options-only) ship as part of v4, ship *before* v4 as a small
   standalone lectern change (cheap, validates the filter + placement), or wait?
3. **Desk section switcher — tabs or columns?** I recommend **tabs** (Active/Backlog/Completed),
   because VS's GUI and the shared row renderer are single-column by construction and the ROADMAP
   itself warns against a multi-column board. Confirm tabs, or do you specifically want
   side-by-side columns despite the single-column-renderer cost?
