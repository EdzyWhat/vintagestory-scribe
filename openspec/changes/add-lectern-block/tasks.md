## 1. Solution & project scaffolding

- [x] 1.1 Create the solution `Scribe.slnx` and a `Directory.Build.props` setting `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable` (+ `VintageStoryPath` fallback)
- [x] 1.2 Create `src/Core/Core.csproj` (a plain classlib, NO game references)
- [x] 1.3 Create `tests/Core.Tests/Core.Tests.csproj` (xUnit, references `Core`)
- [x] 1.4 Create `src/Mod/Mod.csproj` referencing `Core` and `VintagestoryAPI.dll` via `<HintPath>$(VintageStoryPath)/VintagestoryAPI.dll</HintPath>` (Private=false)
- [x] 1.5 Add all three projects to the solution; confirm Core, Core.Tests, AND Mod all build

## 2. Core: block-based document model (test-first)

Document = an ordered sequence of blocks; each block is a task (text + done) or a text
section (freeform), interspersable and reorderable, with a reserved depth for future nesting.

- [x] 2.1 Write failing xUnit tests for the document structure (new doc is empty; block order + kinds preserved)
- [x] 2.2 Write failing tests for add task / add text section (adds to end; task trims + rejects blank; text section allows empty)
- [x] 2.3 Write failing tests for edit block text (keeps done flag/kind; task rejects blank; text section allows empty)
- [x] 2.4 Write failing tests for toggle completion (both directions; fails on a text section; invalid position fails safely)
- [x] 2.5 Write failing tests for delete + reorder (delete preserves order; move up/down; move-to-same is no-op; invalid position fails safely)
- [x] 2.6 (covered by 2.2/2.3 — text sections replace the single note)
- [x] 2.7 Write failing tests for the serialization round-trip (order/kind/text/done/depth preserved; malformed/empty bytes fail safely without throwing)
- [x] 2.8 Implement `ScribeBlock` (Kind, Text, Done, Depth) and `ScribeDocument` (ordered blocks) with mutations returning success/failure
- [x] 2.9 Implement the byte-array codec (serialize/try-deserialize) used by both persistence and networking
- [x] 2.10 Run `dotnet test` — all Core tests pass (27)

## 3. Mod: assets & mod metadata

- [x] 3.1 Add `src/Mod/modinfo.json` (`type: code`, `side: Universal`, `requiredOnServer: true`, id/name/version, game 1.22 dependency)
- [x] 3.2 Confirm (in-game creative search / `.blockcode`) the vanilla lectern shape+texture codes to reuse; record them
      -> confirmed: vanilla `clutter` block, type `bookshelves/lecturn-book-open`
         (switched from the "aged" variant: aged implies scavenged ancient material,
         not something crafted from ordinary wood); shape
         `block/clutter/bookshelves/lecturn-book-open`, textures verified on disk.
- [x] 3.3 Add the lectern block JSON under `assets/scribe/blocktypes/` reusing that shape, wiring it to the custom block/block-entity classes; creative-inventory only (no crafting recipe in v1)
- [x] 3.4 Add `assets/scribe/lang/en.json` for the block name, GUI labels, and hotkey name

## 4. Mod: block, persistence, networking (server-authoritative)

Atlas tests interleave here rather than batching at the end: each test lands right after
the implementation task that makes it possible, so persistence/networking/the lock get a
fast, automated check before we ever touch the GUI or manual playtesting (group 7). Atlas
needs the game install (`VINTAGE_STORY`) and runs via `dotnet test` against a real headless
server — a LOCAL suite only, not run on cloud CI.

- [x] 4.1 Implement `ScribeModSystem` (register block/block-entity classes and the network channel + message type in `Start`; per-side handlers in `StartClientSide`/`StartServerSide`)
- [x] 4.2 Implement `BlockScribeLectern` (placement/break; `OnBlockInteractStart` opens the GUI client-side)
- [x] 4.3 Implement `BlockEntityScribeLectern` holding a `ScribeDocument`; `ToTreeAttributes`/`FromTreeAttributes` serialize via the Core codec (persist + initial sync)
- [x] 4.3a Add a `tests/Integration.Tests` project referencing `Pixnop.Atlas.XUnit`, loading the built mod via `[AtlasMods(...)]` — the first point there's something real to boot a server against
- [x] 4.3b Atlas test: persistence — place a lectern, edit its document, reload the world, assert the document survives (`RollbackWorld`/`RestartWorld` isolation)
- [x] 4.4 Define the `[ProtoContract]` edit message (Core-serialized document bytes + block position) and register it identically on both sides
- [x] 4.5 Server handler applies the incoming document to the block entity and calls `MarkDirty(true)` to persist + re-sync to all clients
- [x] 4.5a Atlas test: server-authoritative edit — send an edit packet, assert the block entity's stored document updates and re-syncs
- [x] 4.6 Implement the single-editor lock: server tracks position→holder UID; refuse a second opener with the "one person at a time" message; release on close and on disconnect/leave
- [x] 4.6a Atlas test: the lock — first opener acquires it; a second opener is refused; lock releases on close/disconnect
- [x] 4.7 Document how to run the Atlas suite locally in the README (it's excluded from cloud CI)

## 5. Mod: editor GUI

- [x] 5.1 Implement `GuiDialogScribeLectern` (a `GuiDialogBlockEntity`) rendering the document's ordered blocks
- [x] 5.2 Each block row: task rows have a complete-toggle + editable text + delete; text-section rows have editable text + delete; plus "add task" / "add text section" controls
- [x] 5.3 Collapsible tool panel with a per-option **visibility-predicate hook** (the gating mechanism); wire NO real gates in v1 (all options visible)
- [x] 5.4 Reorder mode: mouse-drag reordering of block rows in the list (consistent with VS's mouse-driven crafting grid/blacksmithing/clayforming interactions), calling Core `MoveBlock(from, to)` on drop
- [x] 5.5 Text size control as a **client-side display preference** (scales GUI font; stored in local mod config, NOT in the document, NOT synced)
- [x] 5.6 Edit-mode toggle keybind: GUI opens in a resting state; pressing the key enters edit mode with an immersive "pull out the pen/stylus" beat
      -> superseded during planning: no hotkey. Plain right-click opens a lock-free read
         view; shift+right-click (or the in-GUI toggle button) opens/switches to the
         lock-holding editor view. See design.md-equivalent plan notes for the full
         two-view rationale.
- [x] 5.7 On save, send the edited document to the server over the channel; reflect the server-synced state on reopen
      -> superseded during planning: no explicit Save action. Editor-view edits autosave
         via a throttled (1s) dirty-flag tick, force-flushed on mode-switch/close/
         walk-away; the server acks failures (e.g. lost lock) back to the client.

## 6. Build & release automation

- [x] 6.1 Add `.github/workflows/ci.yml`: on push/PR, `dotnet test` the Core project (cloud runners have no game DLL) — document this scope in the README
- [x] 6.2 Add release packaging (`.github/workflows/release.yml` on tag `v*`) that builds the mod locally-style and zips `modinfo.json` + assets + the compiled DLL into `Releases/`
- [x] 6.3 Verify CI is green on a pushed branch

## 7. In-game verification (local, this Mac)

Written for the two-view design: plain right-click opens a lock-free **read view**;
shift+right-click (or the in-GUI toggle button) opens/switches to the lock-holding
**editor view**.

- [ ] 7.1 Build the mod and copy it into `~/Library/Application Support/VintagestoryData/Mods`; launch the game
- [ ] 7.2 Place a lectern (from creative inventory); plain right-click opens a read view with no edit controls; shift+right-click opens the editor view; add tasks, complete one, edit the note, and confirm edits autosave (no explicit Save button — check a moment after typing, before closing, that the change already round-tripped)
- [ ] 7.3 Save and reload the world; confirm the lectern's tasks and note persist
- [ ] 7.4 Toggle check: from the editor view, click the in-GUI toggle to switch to read view (confirm the just-typed edit is reflected, not stale); from the read view, click the toggle to request the editor view back
- [ ] 7.5 Multiplayer check: run a local headless server (`dotnet ".../VintagestoryServer.dll" --dataPath ~/vsdata`) with the mod, connect a second client, confirm an edit by one session is seen live in the other session's *read* view, and that two separate lecterns hold independent documents
- [ ] 7.6 Lock check: with the editor view open in one session, confirm a second session's shift+right-click (or toggle-to-editor) is refused with the "one person at a time" message but still shows current content read-only; confirm a second session's plain right-click (read view) is granted normally even while the editor lock is held elsewhere; confirm closing the editor view or disconnecting releases the lock for the next requester
- [ ] 7.7 Reorder + tool panel check: in the editor view, mouse-drag a row to reorder it; collapse/expand the tool panel; adjust the text-size slider and confirm the font scales and the preference persists across reopen
- [ ] 7.8 Walk-away check: open the editor view, make an edit, walk out of interaction range without closing the GUI; confirm the dialog auto-closes and the edit was flushed (reopen and see it persisted) rather than lost
