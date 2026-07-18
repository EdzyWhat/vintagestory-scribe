## Context

This is the first implemented slice of Scribe and it establishes patterns every later
tier reuses: the game-agnostic `Core` library, the thin `Mod` adapter, and
server-authoritative persistence/sync. The reference model is the vanilla **Sign** block
(`BlockEntitySign`), which stores text in block-entity tree attributes and syncs it to
clients — our lectern is a richer sign (a structured document instead of one string).

Constraints: vanilla `VintagestoryAPI` only; mod is Universal (`requiredOnServer: true`);
targets .NET 10 / game 1.22.3; `Core` must not reference the game API so it stays
unit-testable without the game.

## Goals / Non-Goals

**Goals:**

- A placeable lectern block whose document (tasks + note) persists with the world and
  syncs in multiplayer, edited through a GUI.
- Prove the Core/Mod separation and the GUI → server → persist → sync loop end-to-end.
- Keep all document logic in a unit-tested `Core` library.

**Non-Goals:**

- Held items and the `docId`-on-item store (v2+). v1 keys the document by block position.
- Ownership/privacy gating, categories, HUD, drawing, clay/paper mechanics.
- Fancy custom art — reuse the vanilla lectern shape.

## Decisions

**1. Two projects: `Core` (game-agnostic) + `Mod` (adapter).**
`Core` holds `ScribeDocument` (list of `ScribeTask{Text, Done}` + `Note` string), its
mutation methods (add/rename/toggle/delete task, set note) returning success/failure for
invalid input, and a byte-array codec. `Mod` references `Core` and `VintagestoryAPI.dll`.
*Why:* the spec's document requirements become plain xUnit tests with no game dependency;
this is also the project's central learning goal. *Alternative:* put logic directly in the
block entity — rejected, untestable without the game and not reusable by later tiers.

**2. Store the document in the block entity, keyed by block position.**
A `BlockEntityScribeLectern` holds a `ScribeDocument` and implements
`ToTreeAttributes`/`FromTreeAttributes` (serializing via the Core codec into the tree).
*Why:* mirrors the vanilla Sign; block position is the natural key and the world save +
initial chunk sync come "for free" through the tree attributes. *Alternative:* a separate
`SaveGame.StoreData` document store keyed by a docId — deferred to v2 when held items need
it; unnecessary complexity for a position-fixed block now.

**3. Server-authoritative edits over a network channel.**
The client GUI never mutates the authoritative document directly. On save it sends the
document (Core-serialized bytes in a `[ProtoContract]` message) to the server via a
registered network channel; the server applies it to the block entity, calls
`MarkDirty(true)`, which persists and re-syncs the tree to all clients. Opening the GUI
either reads the already-synced client-side block entity state or requests the current
document from the server.
*Why:* the spec requires the server to be the source of truth and changes to be visible to
other players; `ItemSlot`/client-side attribute edits are explicitly non-authoritative in
VS. *Alternative:* `SendBlockEntityPacket` helpers instead of a named channel — viable and
simpler for a single block, but a named channel generalizes to held items and the HUD
later, so we invest in it now.

**4. Two ways to open: right-click + look-and-hotkey.**
`Block.OnBlockInteractStart` handles right-click. A client-side hotkey registered with the
native `RegisterHotKey` opens the GUI when the player's current block selection is a
lectern. *Why:* the spec requires both; the hotkey is rebindable via the native controls UI.

**5. GUI via `GuiDialogBlockEntity`.**
A `GuiDialog` subclass composed with `AddTextArea` (note), a task list built with
`AddCellList<T>` (each row: a toggle + editable text + delete), and Add/Save buttons.
*Why:* standard VS GUI building blocks; `GuiDialogBlockEntity` already tunnels packets
through the block position and auto-closes when the player walks away.

## Risks / Trade-offs

- **Silent-fail scenario formatting in specs** → validated with `openspec validate --strict`.
- **Reusing the vanilla lectern shape may need exact block/shape codes we haven't confirmed**
  → verify in-game via creative search / `.blockcode`; if the shape can't be referenced
  cleanly, fall back to a minimal custom shape (cheap). Does not affect Core.
- **Network message ordering / registering types on both sides** → register message types
  in `Start` (runs both sides) in identical order; keep the payload one Core-serialized blob
  to minimize surface.
- **Concurrent edits by two players** (last-write-wins clobber) → acceptable for v1; the
  server applies whole-document saves and re-syncs. Fine-grained ops can come later if needed.
- **GUI scope creep** → v1 keeps the GUI minimal (flat task list + one note); categories,
  reordering UI, etc. are later tiers.

## Open Questions

- Exact vanilla lectern block/shape code to reuse (resolve in-game before authoring the
  block JSON).
- Crafting recipe for v1, or creative-only to start? (Leaning: a simple recipe, but
  creative-only is acceptable for the first playtest.)
