# v2 — Notebook (collection tier)

> Exploration/design spec (not an OpenSpec change, not implemented code). Input to a future
> `openspec-propose`. Follows `docs/specs/README.md`. Merges these ROADMAP.md items: the v2
> "Notebook (collection)" tier (leather-bound held item, infinite pages), the `docId`-on-item
> store it introduces, and the two v2 exploration decisions (A: scrollable region is a hard
> prerequisite; B: the single-editor lock likely does not carry over).

## Summary

The notebook is Scribe's **first held artifact**: a leather-bound item that opens the same
task/note document UI the lectern uses, but travels in the player's inventory instead of
living at a fixed block position. It is the collection tier — "infinite pages" — so its
document can grow without the bounded-length compromises v1 accepted.

Two pieces of foundational infrastructure arrive with it, both of which the v3 clay tablet
(and every later held/placed artifact) reuse:

1. **The `docId`-on-item store.** v1 keys a document by block position (the block entity *is*
   the document's home). A held item has no fixed position, so the notebook carries a short
   **document id (`docId`)** on its item-stack attributes, and the actual `ScribeDocument`
   lives in a **server-side store keyed by that `docId`**. This is the architecture
   `openspec/config.yaml` already names ("an artifact carries a short id; the actual document
   lives in a server-side store keyed by it… a docId-on-item path arrives with the notebook").

2. **Held-item GUI open + server-authoritative save for a positionless artifact.** The lectern
   leans on `GuiDialogBlockEntity` (per-position dedup, walk-away auto-close) and packets that
   address the target by `PosX/Y/Z`. A held item has neither, so v2 establishes the
   held-item equivalents: `OnHeldInteractStart` to open, a plain `GuiDialog` keyed by `docId`,
   and packets addressed by `docId` instead of block position.

The two roadmap decisions resolve as: **(A)** the row-list-rework's real scrollable/clipped
region is a hard prerequisite (delivered by S1/S2 of that in-flight work — referenced, not
re-specced here); and **(B)** the lectern's position-based single-editor lock is **dropped** —
a held stack has exactly one holder, who is the only possible accessor, so the contention the
lock prevents cannot arise. This matches vanilla `ItemBook`, which has no lock (see below).

## VS API hooks

Confirmed by decompiling the installed `VintagestoryAPI.dll` (v1.22.x) and by reading
`anegostudios/vssurvivalmod`'s `Systems/WritingSystem/` (the vanilla writable-book — the
closest first-party precedent for a held writing item). Where a fact is confirmed is noted
inline.

### Opening the GUI from a held item

- **`CollectibleObject.OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection
  blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)`** —
  overridden on an `Item` subclass, this is the right-click-with-item-in-hand entry point
  (confirmed: `CollectibleObject` decompile, line ~1553; vanilla `ItemBook.OnHeldInteractStart`
  is the reference usage). Set `handling = EnumHandHandling.PreventDefault` to consume the
  interaction (confirmed against `ItemBook`, which sets `PreventDefault` on both its write and
  read paths and falls through to `base` only when it wants default placement behavior).
- The dialog is opened **client-side only**, guarded by `if (api.Side == EnumAppSide.Client)`
  (confirmed: `ItemBook` opens `GuiDialogEditableBook`/`GuiDialogReadonlyBook` inside that
  guard). Held-item interactions fire on both sides; only the client constructs GUI.
- **Shift-modifier convention.** The lectern uses `byPlayer.Entity?.Controls?.ShiftKey` to pick
  editor vs. read (`BlockScribeLectern.OnBlockInteractStart`). `OnHeldInteractStart` receives
  `byEntity` (the `EntityAgent`), so the same `byEntity.Controls.ShiftKey` read is available if
  a read/edit split is kept — but see Decision B: the notebook likely opens straight into the
  editable view, since its holder is its only user.

### Item-stack custom data (where the `docId` lives)

- **`ItemStack.Attributes`** is an `ITreeAttribute` (backing `TreeAttribute`) that is **saved
  and synchronized** with the stack (confirmed: `ItemStack` decompile, "Attributes assigned to
  this particular itemstack which are saved and synchronized"). This is the held-item analogue
  of the block entity's `ITreeAttribute` tree — the same `SetString`/`GetString`,
  `SetBytes`/`GetBytes` surface the Sign pattern uses.
- **`ItemSlot.MarkDirty()`** (confirmed: `ItemSlot` decompile, line ~469) is the held-item
  analogue of `BlockEntity.MarkDirty()` — it persists the stack's attributes and re-syncs the
  slot to the client. Vanilla `ModSystemEditableBook.EndEdit` writes the book text onto
  `slot.Itemstack.Attributes` and then calls `slot.MarkDirty()` (confirmed via source).
- **`ItemStack.TempAttributes`** exists (not saved, not synced) — noted only to *avoid*: the
  `docId` must go in `Attributes`, never `TempAttributes`, or it would not persist.

### Server-side keyed document store

- **`ICoreServerAPI.WorldManager.SaveGame`** exposes **`byte[] GetData(string key)`** and
  **`void StoreData(string key, byte[] data)`** (also generic `T GetData<T>/StoreData<T>`)
  (confirmed: `Vintagestory.API.Server.ISaveGame` decompile, lines 54–78). This is the
  server-side, per-savegame, persistent key→bytes store the `docId` store is built on. The
  documented size limit is "~1 GB for *all* data stored with the savegame" — fine for text
  documents, but a real (if distant) reason not to store binary blobs carelessly.
- The store bytes are exactly `ScribeDocumentCodec.Serialize(doc)` — the same codec v1 already
  uses for block-entity persistence and network sync. No new serialization format.

### Networking

- The lectern registers its message types on the shared `"scribe"` channel in
  `ScribeModSystem.Start` and routes them in `StartServerSide`/`StartClientSide` (confirmed:
  `ScribeModSystem.cs`). The notebook adds new message types on the **same channel**, keyed by
  `docId` rather than `PosX/Y/Z`. Message registration order must match on both sides
  (existing invariant — append notebook types after the lectern types).
- Vanilla precedent for the held-item save round-trip: `ModSystemEditableBook` registers its
  own channel (`"editablebook"`) with `EditbookPacket { bool DidSave, bool DidSign, string
  Text, string Title }`, tracks a transient server-side `nowEditing` map (playerUID → the
  `ItemSlot` being edited) purely to route the save back to the right slot, and on receipt
  writes attributes + `MarkDirty()` (confirmed via source). Scribe's equivalent is
  `docId`-addressed and server-store-backed rather than writing the whole document onto the
  stack, but the shape (transient edit-session map + authoritative save handler) is the same.

### GUI dialog base class

- The lectern uses **`GuiDialogBlockEntity`** specifically for its per-block-position dedup and
  walk-away auto-close (confirmed: `GuiDialogScribeLectern` class doc comment). A held item has
  no block position, so the notebook uses a **plain `GuiDialog`** (confirmed base: `GuiDialog`
  ctor `GuiDialog(ICoreClientAPI capi)`, `TryOpen()`/`OnGuiOpened()`/`OnGuiClosed()` — decompile
  lines 269–353). Dedup (don't open two dialogs for the same notebook) is done by `docId`
  instead of position — see Implementation.
- The entire row-list rendering (`ScribeRowElement`, `RowTextLayout`, the scroll/clip path) is
  **reused unchanged** — it is already position-agnostic (it renders a `ScribeDocument`, not a
  block). The notebook only swaps the dialog's *shell* (base class, open/save plumbing), not
  its row internals.

## C# data structures

### Core (`src/Core/` — MUST NOT reference the VS API)

The document model needs **no change** — `ScribeDocument`/`ScribeBlock`/`ScribeDocumentCodec`
are already position-agnostic and store/serialize an ordered block list with no notion of where
the document "lives". The notebook reuses them verbatim.

The one *optional* Core addition, purely to keep the store's semantics unit-testable without a
game install (honoring the Core-testability invariant):

```csharp
namespace Scribe.Core;

/// A game-agnostic keyed document store. The Mod supplies a SaveGame-backed implementation;
/// Core supplies an in-memory one so store semantics (allocate id, get-or-empty, overwrite,
/// delete) are unit-testable with `dotnet test` and no game install. NO VS API here — this is
/// a plain string→ScribeDocument map contract, not tied to ISaveGame.
public interface IDocumentStore
{
    bool TryGet(string docId, out ScribeDocument document); // false → no such doc
    void Save(string docId, ScribeDocument document);
    void Delete(string docId);
    bool Exists(string docId);
}

/// Test/reference implementation. The real persistence lives in the Mod (see below).
public sealed class InMemoryDocumentStore : IDocumentStore { /* Dictionary<string, byte[]> */ }
```

Storing serialized **bytes** (via `ScribeDocumentCodec`) inside the in-memory impl — rather than
live `ScribeDocument` references — makes the in-memory store behave like the real byte-keyed
SaveGame store (a `Save` then a later `TryGet` returns an independent copy, not the same mutable
object), so tests catch aliasing bugs the real store wouldn't have.

`docId` generation is a Core concern too (game-agnostic): a small `DocIdFactory` returning a
`Guid.NewGuid().ToString("N")` string, or just inline `Guid` use in the Mod. Keep it trivial;
the only requirement is global uniqueness across a savegame.

> Deliberately **not** added to Core: any notion of "held", "item", "stack", or "position".
> The `docId` is an opaque string as far as Core is concerned.

### Mod (`src/Mod/` — the VS API adapter)

**`ItemScribeNotebook : Item`** (registered via `api.RegisterItemClass("ScribeNotebook",
typeof(ItemScribeNotebook))` in `ScribeModSystem.Start`). Analogue of `BlockScribeLectern`;
stays thin.
- Const `DocIdAttributeKey = "scribeDocId"` — the stack-attribute key holding the `docId`.
- `override OnHeldInteractStart(...)` — reads/lazily-allocates the `docId` (server), opens the
  dialog (client), sets `handling = EnumHandHandling.PreventDefault`.
- `override GetHeldItemName` / `GetHeldItemInfo` (optional) — show a title / task-count in the
  tooltip, mirroring `ItemBook`'s use of a `"title"` attribute. Cheap, immersive; not required
  for a first cut.

**`ScribeDocumentStore`** (Mod-side `IDocumentStore` impl, server-only) — wraps
`sapi.WorldManager.SaveGame`:
- `TryGet(docId)` → `SaveGame.GetData("scribe:doc:" + docId)`, `ScribeDocumentCodec.TryDeserialize`.
- `Save(docId, doc)` → `SaveGame.StoreData("scribe:doc:" + docId, ScribeDocumentCodec.Serialize(doc))`.
- Key namespace `"scribe:doc:"` keeps notebook docs from colliding with any future keyed data.
- Lives on the server side of `ScribeModSystem` (constructed in `StartServerSide`).

**`GuiDialogScribeNotebook : GuiDialog`** — the held-item dialog. Wraps the *same*
`ScribeRowElement`/`RowTextLayout` row-list content the lectern dialog uses. The cleanest
factoring is to extract the shared compose/scroll/edit-in-place body from
`GuiDialogScribeLectern` into a reusable helper (a `ScribeDocumentPanel` component, or a shared
base) that both dialogs host; but at minimum the notebook dialog:
- Holds the current `docId` and a local `ScribeDocument` (the synced copy) plus the editor
  scratch copy — same shape as the lectern dialog's `Document` vs `scratchDocument`.
- Throttled autosave-while-dirty tick sending `ScribeNotebookEditMessage` — same pattern as the
  lectern's autosave.
- Uses plain `GuiDialog` open/close; `ToggleKeyCombinationCode`/dedup keyed by `docId`.

**Packets** (new; on the existing `"scribe"` channel, registered after the lectern types):

```csharp
[ProtoContract] sealed class ScribeNotebookOpenMessage {         // client → server: open request
    [ProtoMember(1)] public string DocId;      // "" if the stack has no docId yet (lazy alloc)
    [ProtoMember(2)] public bool WantEditor;   // present for symmetry; may be ignored (Decision B)
    [ProtoMember(3)] public int HotbarSlotId;  // so the server can locate the stack to stamp a new docId + MarkDirty
}
[ProtoContract] sealed class ScribeNotebookDocumentMessage {     // server → client: authoritative doc
    [ProtoMember(1)] public string DocId;      // the (possibly newly-allocated) id
    [ProtoMember(2)] public byte[]? DocumentBytes;
    [ProtoMember(3)] public bool Granted = true;
    [ProtoMember(4)] public string? RefusalReason;
}
[ProtoContract] sealed class ScribeNotebookEditMessage {         // client → server: autosave tick
    [ProtoMember(1)] public string DocId;
    [ProtoMember(2)] public byte[]? DocumentBytes;
}
```

Note these mirror `ScribeEditDocumentMessage` field-for-field **except** the addressing key is
`DocId` (string) instead of `PosX/Y/Z` (ints), and there is **no lock/release message** (no
`ScribeReleaseLockMessage`/`ScribeRequestAccessMessage` analogue — see Decision B). Whether to
generalize the existing lectern packets to carry an optional `DocId` instead of duplicating them
is an Open Question below; duplicating is simpler and keeps v1 untouched.

## Implementation spec

### 1. Assets: item + recipe (Mod-side JSON)
- `assets/scribe/itemtypes/notebook.json` — the leather-bound notebook item, `class:
  "ScribeNotebook"`, `maxstacksize: 1` (a docId-bearing item must not stack — two copies in one
  stack would share/lose the id). Held-item shape + texture (placeholder art acceptable for
  v2, per the lectern's placeholder-art precedent).
- `assets/scribe/recipes/grid/notebook.json` — grid recipe (leather + paper/cordage;
  metal-age-appropriate). Detail deferred; not architecturally load-bearing.
- `lang/en.json` — item name, GUI title, interaction hints (remember the `"scribe:"` domain
  prefix on every `Lang.Get`/`ActionLangCode` — see VSAPI-NOTES "Localization").

### 2. Register the item + store + packets (`ScribeModSystem`)
- `Start`: `api.RegisterItemClass("ScribeNotebook", typeof(ItemScribeNotebook))`; append the
  three notebook message types to the existing `.RegisterChannel("scribe")` chain (same order
  both sides).
- `StartServerSide`: construct `ScribeDocumentStore` over `sapi.WorldManager.SaveGame`; set
  handlers for `ScribeNotebookOpenMessage` (open/alloc) and `ScribeNotebookEditMessage`
  (autosave). Keep a transient `Dictionary<string playerUID, string docId> notebookEditing`
  (the vanilla `nowEditing` analogue) so an edit is only accepted from the player who opened
  that `docId` this session — this is the server-authoritative guard that *replaces* the lock,
  not a contention lock (Decision B).
- `StartClientSide`: set handler for `ScribeNotebookDocumentMessage`.

### 3. Open flow (`ItemScribeNotebook.OnHeldInteractStart`)
1. Set `handling = EnumHandHandling.PreventDefault` (consume the right-click).
2. Read `docId = slot.Itemstack.Attributes.GetString("scribeDocId")` (may be null/"").
3. **Client side:** send `ScribeNotebookOpenMessage { DocId = docId ?? "", HotbarSlotId, WantEditor }`.
   Do **not** open the dialog yet — wait for the authoritative `ScribeNotebookDocumentMessage`
   reply (mirrors the lectern, which opens only after the server reply carrying the document).
4. **Server side (`OnServerReceivedNotebookOpen`):**
   - If `DocId` is empty → allocate `docId = Guid.NewGuid().ToString("N")`; write it onto the
     stack (`slot.Itemstack.Attributes.SetString("scribeDocId", docId)`); `slot.MarkDirty()` so
     the id persists and syncs; the store has no entry yet (empty document is the default on
     first `TryGet` miss).
   - Load the document: `store.TryGet(docId, out doc)` else `doc = new ScribeDocument()`.
   - Record `notebookEditing[playerUID] = docId`.
   - Reply `ScribeNotebookDocumentMessage { DocId = docId, DocumentBytes = Serialize(doc),
     Granted = true }`.
   - Locating the stack to stamp the id: use `HotbarSlotId` against
     `serverPlayer.InventoryManager.GetHotbarInventory()[slotId]` (or the player's active slot),
     validating the slot actually holds a `ScribeNotebook` — never trust the client's slot id
     blindly.
5. **Client (`OnClientReceivedNotebookDocument`):** construct `GuiDialogScribeNotebook(capi,
   docId, documentBytes)` and `TryOpen()`. If a dialog for this `docId` is already open, ignore
   (dedup).

### 4. Edit + save flow
- Editor edits a scratch `ScribeDocument`; a throttled autosave-while-dirty tick sends
  `ScribeNotebookEditMessage { DocId, DocumentBytes }` — identical cadence to the lectern's
  autosave.
- Server `OnServerReceivedNotebookEdit`: verify `notebookEditing[playerUID] == DocId` (the
  authoritative guard); `TryDeserialize`; `store.Save(docId, doc)`. There is **no** per-frame
  block-entity re-sync (no block entity exists) — the holder is the only viewer, so the server
  does not need to push the document back on every edit. It only pushes on open (step 3) and,
  optionally, on an explicit reader/refresh. This is *simpler* than the lectern, which had to
  `MarkDirty(redrawOnClient:true)` to reach other nearby viewers.
- On dialog close: send a final autosave if dirty; clear `notebookEditing[playerUID]` (via a
  close/leave message or piggy-backed on the last edit). Also clear on player disconnect
  (`sapi.Event.PlayerDisconnect`, as the lectern already does for its lock).

### 5. Persistence guarantees
- The `docId` lives on the stack attributes → travels with the item automatically through
  inventory moves, chest storage, drop-on-death, and pickup (VS handles stack attribute
  persistence). The document bytes live in `SaveGame` under `"scribe:doc:" + docId`.
- **Drop-on-death immersion hook** (roadmap "Death leaves a last entry") composes naturally
  later: the dropped notebook item still carries its `docId`, so a finder who picks it up and
  opens it hits the same server-store document. No extra work in v2 to enable it.

### 6. Row list / "infinite pages"
- Reuse S1/S2 of the row-list-rework verbatim (see Dependencies). "Infinite pages" is honest
  only once the real scrollable/clipped region exists; until then a long document renders
  off-screen. No pagination — the row-list-rework explicitly rejected paging in favor of a
  continuous scroll region (ROADMAP `skeuomorphic-lectern-gui` note).

## Decision B (the lock) — resolved

**Drop the position-based single-editor lock for the notebook. Do not port
`ScribeRequestAccessMessage`/`ScribeReleaseLockMessage`/`lockHolderUid`.**

Reasoning, grounded in the code:
- The lectern lock (`BlockEntityScribeLectern.lockHolderUid`, granted/refused in
  `RequestAccess`) exists because a **fixed-position** document is reachable by *any* player who
  walks up to the block — two players could open the same lectern's editor at once. That
  concurrency is real for a block.
- A held stack has exactly **one holder at any instant** (VS inventory semantics; a stack is in
  exactly one slot in one inventory). The holder is the only entity who can `OnHeldInteractStart`
  it. So the two-editors-at-once contention the lock prevents **cannot occur** for a normally
  unique notebook. Vanilla `ItemBook` confirms the pattern by having **no lock** — only a
  transient `nowEditing` session map used to route the save, not to arbitrate contention.
- What *does* carry over is the **server-authoritative save guard**: the server applies an edit
  only from the player it recorded in `notebookEditing[playerUID]` for that `docId`. That is not
  a lock (it never refuses a second player — there is no second player); it is a "this edit came
  from the session that opened this doc" sanity check.

**The one residual edge — a duplicated `docId`.** If a notebook stack is duplicated (creative
`/givestack` of an already-written notebook, or a dupe exploit), two physical stacks would share
one `docId` and two holders could edit the same server document concurrently → last-write-wins
clobbering. This is:
- Rare (requires creative/exploit; a crafted notebook always gets a fresh id on first write).
- **Already an accepted class of risk** in this codebase — the read-view lock-free toggle
  explicitly accepts that a concurrent editor's autosave can overwrite a reader's toggle
  (S1 handoff / archived design Risks). Notebook dup-edit is the same last-write-wins tradeoff.
- Not worth a lock to fix (a lock keyed by `docId` would reintroduce exactly the complexity we
  just removed, to guard a creative-only edge). Documented as a known limitation.

If a future tier *does* need multi-accessor notebooks (e.g. a notebook left in a shared
container that several players can open — not a v2 scenario), the `docId`-keyed store makes
adding a `docId`-keyed lock straightforward at that point. v2 does not pay for it.

## Dependencies & sequencing

**Hard prerequisite (Decision A): the row-list-rework.** "Infinite pages" requires the real
scrollable/clipped region. That region is being delivered by the in-flight `row-list-rework`
(branch `row-list-rework`): **S1 (read view) is shipped/archived**; **S2 (edit-in-place) is the
next stage** and moves the *editor* onto the same custom-drawn `ScribeRowElement` renderer. The
notebook needs the unified read+edit renderer, so **v2 must not start until S2 lands** (ideally
S2 merged to `main`; the branch is intentionally held until S2 removes the sample-seed and
reunifies the views — see `docs/session-notes/2026-07-21-S1-done-handoff.md`). S3 (drag-reorder
feedback) and S4 (checkbox animation) are polish and not blockers.

**Sequencing within v2:**
1. Core: optional `IDocumentStore` + `InMemoryDocumentStore` (+ tests). Pure Core; can be done
   anytime, even before S2.
2. Mod: `ScribeDocumentStore` over `SaveGame` + docId allocation, with an Atlas persistence
   test (round-trip a document through the store, restart, re-read) — mirrors the existing
   `PersistenceScenarios` lectern fixture approach.
3. Mod: `ItemScribeNotebook` + assets + recipe + the three packets + open/save handlers.
4. Mod: `GuiDialogScribeNotebook` — extract the shared row-list panel from
   `GuiDialogScribeLectern` (do this as a refactor first so both dialogs share one renderer) and
   host it under a plain `GuiDialog`.
5. Playtest: create, write, close, reopen, drop, pick up, verify persistence; verify no lock
   friction.

**Position in the staged plan:** v2 is the immediate next tier after v1's lectern slice. Its
`docId` store + held-item GUI plumbing are explicitly reused by **v3 (clay tablet)** and are the
substrate for v5 (backpack collection) and the drop-on-death immersion hook.

**Font supplier:** the notebook/book **rustic-script typeface** is specced in
`presentation-and-fonts.md` (item 3), which sequences the rustic face to land *with* v2. When
v2 is proposed, pull the font-face selection + `FreeTypeFontFace` loading from that spec rather
than re-deriving it here; it's gated on a font-license clearance called out there.

## Open questions

1. **One store or two?** — **DECIDED 2026-07-21: one shared store.** A single artifact-agnostic
   `"scribe:doc:<docId>"` namespace in `SaveGame` holds notebook + desk + tablet + backpack docs,
   indexed by `docId` (not duplicated per artifact). The desk "consolidates all your notes" by
   indexing into the same store. Proves the store is artifact-agnostic (a v3 goal) and makes the
   drop-on-death "current document" lookup trivial. (ROADMAP Open decision #4.)
2. **Duplicate/generalize the packets, or add `DocId` to the existing ones?** — **DECIDED
   2026-07-21: generalize.** The v1 lectern edit/toggle/move packets are refactored to carry an
   abstract document handle (BlockPos *or* docId) so lectern and held items share one wire path.
   This rewrites v1's confirmed wire format, accepted now while it's early dev with no shipped
   saves — DRYer than maintaining two parallel packet families forever. **Re-scope this spec's
   implementation section (which currently assumes duplicated notebook packets) to the generalized
   path when v2 is proposed.** (ROADMAP Open decision #4.)
3. **Orphaned documents.** When a notebook item is destroyed (burned, despawned, void), its
   `SaveGame` store entry is orphaned (the item can't tell the store it's gone). Options: (a)
   accept the leak — text is tiny vs the ~1 GB budget; (b) a periodic GC pass reconciling live
   `docId`s against stored keys (expensive, needs a world scan); (c) reference-count on
   craft/destroy (fragile). This spec assumes **(a) accept the leak** as the v2 default and
   revisits only if it ever matters. Confirm.
4. **Read/edit split, or edit-only?** Since the holder is the sole accessor, does the notebook
   need the lectern's read-vs-editor mode toggle at all, or does it open straight into editable?
   This spec leans **edit-only** (simpler). A read/edit split becomes meaningful only if the
   "sign → read-only" immersion idea (roadmap) is pursued, which is out of v2 scope.
5. **Cross-world portability.** Because the document lives in the *savegame's* store (not on the
   stack), a notebook carried to a different world/server is empty there (the `docId` resolves
   to nothing). Is that acceptable for v2, or should the document also be mirrored onto the
   stack for portability? (The roadmap's separate JSON export/import item is the intended answer
   for cross-world; confirm v2 doesn't need stack-mirroring.)
