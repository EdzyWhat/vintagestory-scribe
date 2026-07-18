## 1. Solution & project scaffolding

- [x] 1.1 Create the solution `Scribe.slnx` and a `Directory.Build.props` setting `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable` (+ `VintageStoryPath` fallback)
- [x] 1.2 Create `src/Core/Core.csproj` (a plain classlib, NO game references)
- [x] 1.3 Create `tests/Core.Tests/Core.Tests.csproj` (xUnit, references `Core`)
- [x] 1.4 Create `src/Mod/Mod.csproj` referencing `Core` and `VintagestoryAPI.dll` via `<HintPath>$(VintageStoryPath)/VintagestoryAPI.dll</HintPath>` (Private=false)
- [x] 1.5 Add all three projects to the solution; confirm Core, Core.Tests, AND Mod all build

## 2. Core: task-note document model (test-first)

- [x] 2.1 Write failing xUnit tests for the document structure (new doc is empty; task order preserved)
- [x] 2.2 Write failing tests for add task (adds to end; trims; rejects blank)
- [x] 2.3 Write failing tests for rename task (changes text, keeps done flag; rejects blank; invalid position fails safely)
- [x] 2.4 Write failing tests for toggle completion (both directions; invalid position fails safely)
- [x] 2.5 Write failing tests for delete task (removes by position, preserves order; invalid position fails safely)
- [x] 2.6 Write failing tests for editing the note (set and clear)
- [x] 2.7 Write failing tests for the serialization round-trip (content preserved; malformed/empty bytes fail safely without throwing)
- [x] 2.8 Implement `ScribeTask` (Text, Done) and `ScribeDocument` (tasks + note) with the mutation methods returning success/failure
- [x] 2.9 Implement the byte-array codec (serialize/try-deserialize) used by both persistence and networking
- [x] 2.10 Run `dotnet test` — all Core tests pass

## 3. Mod: assets & mod metadata

- [ ] 3.1 Add `src/Mod/modinfo.json` (`type: code`, `side: Universal`, `requiredOnServer: true`, id/name/version, game 1.22 dependency)
- [ ] 3.2 Confirm (in-game creative search / `.blockcode`) the vanilla "Aged book lectern" shape+texture codes to reuse; record them
- [ ] 3.3 Add the lectern block JSON under `assets/scribe/blocktypes/` reusing that shape, wiring it to the custom block/block-entity classes; creative-inventory only (no crafting recipe in v1)
- [ ] 3.4 Add `assets/scribe/lang/en.json` for the block name, GUI labels, and hotkey name

## 4. Mod: block, persistence, networking (server-authoritative)

- [ ] 4.1 Implement `ScribeModSystem` (register block/block-entity classes and the network channel + message type in `Start`; per-side handlers in `StartClientSide`/`StartServerSide`)
- [ ] 4.2 Implement `BlockScribeLectern` (placement/break; `OnBlockInteractStart` opens the GUI client-side)
- [ ] 4.3 Implement `BlockEntityScribeLectern` holding a `ScribeDocument`; `ToTreeAttributes`/`FromTreeAttributes` serialize via the Core codec (persist + initial sync)
- [ ] 4.4 Define the `[ProtoContract]` edit message (Core-serialized document bytes + block position) and register it identically on both sides
- [ ] 4.5 Server handler applies the incoming document to the block entity and calls `MarkDirty(true)` to persist + re-sync to all clients
- [ ] 4.6 Implement the single-editor lock: server tracks position→holder UID; refuse a second opener with the "one person at a time" message; release on close and on disconnect/leave

## 5. Mod: editor GUI

- [ ] 5.1 Implement `GuiDialogScribeLectern` (a `GuiDialogBlockEntity`) showing the note text area and the task list
- [ ] 5.2 Task list: each row has a complete-toggle, editable text, and delete; plus an "add task" control
- [ ] 5.3 On save, send the edited document to the server over the channel; reflect the server-synced state on reopen

## 6. Build & release automation

- [x] 6.1 Add `.github/workflows/ci.yml`: on push/PR, `dotnet test` the Core project (cloud runners have no game DLL) — document this scope in the README
- [x] 6.2 Add release packaging (`.github/workflows/release.yml` on tag `v*`) that builds the mod locally-style and zips `modinfo.json` + assets + the compiled DLL into `Releases/`
- [x] 6.3 Verify CI is green on a pushed branch

## 7. In-game verification (local, this Mac)

- [ ] 7.1 Build the mod and copy it into `~/Library/Application Support/VintagestoryData/Mods`; launch the game
- [ ] 7.2 Place a lectern (from creative inventory); open it by right-click; add tasks, complete one, edit the note
- [ ] 7.3 Save and reload the world; confirm the lectern's tasks and note persist
- [ ] 7.4 Multiplayer check: run a local headless server (`dotnet ".../VintagestoryServer.dll" --dataPath ~/vsdata`) with the mod, connect the client, confirm an edit by one session is seen by another and that two lecterns hold independent documents
- [ ] 7.5 Lock check: with the lectern open in one session, confirm a second session is refused with the "one person at a time" message, and that closing/disconnecting releases the lock
