# v6 — Bulletin board (social tier) + shared/signed notes

> Implementation-exploration spec (design pass, 2026-07-21). NOT an OpenSpec change, NOT
> implemented code. When v6 is picked up, this becomes the input to `openspec-propose`.
> Structure follows `docs/specs/README.md`.

## Summary

The social tier turns Scribe from a set of personal artifacts into a **shared, public**
surface. It merges four ROADMAP items into one cluster that all sit on top of a single new
capability — a **public, non-owner-gated shared document block**:

1. **Bulletin board (public block)** — anyone can read and post; the task-oriented shared
   list. Contrast the private lectern/desk which gate the editor on `ownerUID`; the board
   deliberately does **not**.
2. **Chalkboard (drawable variant)** — same "public shared surface" framing, but its data
   model is *drawing* (strokes/pixels), not text. Flagged below as a distinct data model
   and a candidate for a **later sub-change** rather than v6-core.
3. **Signed vs. unsigned notes (immersion)** — since server-authoritative writes already
   know the writer's `PlayerUID`/`PlayerName` at write time, optionally persist and display
   it as a per-entry signature (attributed) vs. leaving it blank (anonymous). Cheap; a Core
   field plus a write-time capture in the network handler.
4. **Guestbook variant (immersion)** — a thin, **append-only**, signed-entry log ("visited
   on day X"), distinct from the task board. Cheap once the shared-doc infra + signatures
   + calendar read exist; it is mostly a *policy* over the same models.

A fifth item, **wax-seal soft security** for *private* docs, is analyzed here for
completeness but its recommendation is **defer / build-native-later**: the only cheap
interop candidate (the Envelopes mod) is items-only with no public API, so it does not
actually reduce the work. See "Dependencies & sequencing".

The whole cluster reuses the existing `ScribeDocument`/`ScribeBlock` Core model and the
Sign-pattern persistence/sync already proven on the lectern. The genuinely new Core
concepts are: (a) an **access policy** (public vs. private) expressed *without* the VS API,
(b) a **signature/attribution** field on blocks, (c) an **append-only** document mode, and
(d) a **stroke-based drawing model** for the chalkboard (self-contained, later sub-change).

## VS API hooks

All confirmed against the installed `VintagestoryAPI.dll` (v1.22.x) decompile and the
`anegostudios/vssurvivalmod` `BlockEntity/BESign.cs` source, plus this repo's existing
lectern adapter, which already exercises most of these.

### Reused from the lectern (already proven — see `BlockEntityScribeLectern.cs`)

- **Block / block-entity split** — `Block` + `BlockEntity`, registered via
  `api.RegisterBlockClass` / `api.RegisterBlockEntityClass` in `ScribeModSystem.Start`.
  Mirrors the vanilla Sign split (`BlockSign` + `BlockEntitySign`).
- **Persistence (Sign pattern)** — `BlockEntity.ToTreeAttributes(ITreeAttribute)` /
  `FromTreeAttributes(ITreeAttribute, IWorldAccessor)`, writing the codec bytes under a
  string key (`tree.SetBytes` / `tree.GetBytes`). Confirmed: `BESign` persists its `text`
  the same way; we persist the whole serialized `ScribeDocument`.
- **Sync** — `MarkDirty(redrawOnClient: true)` after a server-authoritative mutation;
  server pushes the current document to clients via the existing `scribe` network channel
  (`ScribeEditDocumentMessage`). `BESign` uses `MarkDirty` + `MarkModified()` on the chunk;
  our lectern already relies on `MarkDirty`'s block-entity packet path, which is sufficient.
- **Network channel** — `api.Network.RegisterChannel("scribe")` +
  `RegisterMessageType<T>()` (same order both sides), `SetMessageHandler<T>` per side. New
  message types for the board append onto this same channel.

### Player identity at write time (signatures) — CONFIRMED

Server-side network handlers already receive the sender as an `IServerPlayer` (see
`ScribeModSystem.OnServerReceivedEdit(IServerPlayer fromPlayer, …)`). From
`VintagestoryAPI.dll` decompile of `IPlayer`:

- `string PlayerUID { get; }` — "unique across all registered players and will never
  change. Use this to uniquely identify a player for all eternity." → the stable
  attribution key to persist.
- `string PlayerName { get; }` — "The character name can be changed every 60 days … don't
  consider the players name as a unique identifier." → persist for *display* but treat
  `PlayerUID` as the identity; re-resolve the display name from UID when possible so a
  rename doesn't leave stale signatures.

So a signature is captured **server-side, at the moment of the write**, from the
`fromPlayer` that the message handler already has — the client never asserts its own
identity (which would be spoofable). This is the same trust boundary the lectern's lock
already relies on.

### Access / claims — CONFIRMED (informs public-vs-private)

- `BESign` gates editing on `Api.World.Claims.TryAccess(player, Pos,
  EnumBlockAccessFlags.BuildOrBreak)` — i.e. land-claim build rights, **not** a
  Scribe-specific owner check. `IWorldAccessor.Claims` is `ILandClaimAPI` (confirmed on the
  decompiled interface).
- The lectern's private-editor gate is Scribe's own `lockHolderUid` / `ownerUID` concept,
  *layered on top of* whatever claim protection the world already provides.
- **Design consequence:** the public board should keep the vanilla claim check (so a
  claimed-land board still respects the claim) but add **no** Scribe owner gate on the
  editor — the opposite of the private lectern/desk. "Public" in Scribe means "no
  `ownerUID` editor gate", it does not mean "bypass land claims".

### In-game calendar (guestbook "day X") — CONFIRMED

From the `IGameCalendar` decompile (`IWorldAccessor.Calendar`, server-side available only
after run stage `LoadGamePre` — after which it is non-null):

- `int Year { get; }` (starts at 1386), `int DayOfYear { get; }` (0..`DaysPerYear`),
  `double TotalDays { get; }`, `EnumMonth MonthName`, `EnumSeason GetSeason(BlockPos)`,
  `float HourOfDay`.
- `string PrettyDate()` — "The worlds current date, nicely formatted" — the cheapest
  correct display string for a guestbook entry; but see Open Questions on storing a raw
  numeric stamp vs. a pre-formatted string.

**Recommendation:** store a **numeric** stamp in Core (e.g. `TotalDays` as a `double`, or
`Year` + `DayOfYear` as ints) so Core stays game-agnostic and the display string can be
localized/reformatted later; read it Mod-side from `sapi.World.Calendar` at write time and
pass the plain numbers into Core. Do **not** call `PrettyDate()` inside Core (it is a VS
API method) — format for display in the Mod layer, or store the pretty string only as an
opaque display cache alongside the numbers.

### Text GUI (board editor)

Reuse the lectern's custom row-list GUI machinery wholesale (`GuiDialogScribeLectern`,
`ScribeBlockRowCell`, `ScribeRowListScrollbar`, the two-pass measure/cull renderer). All
the hard-won GUI facts in `VSAPI-NOTES.md` (composer lifecycle, cull-don't-clip,
viewport-relative Y scrolling, scrollbar drag hand-off, macOS caret routing) apply
unchanged. The board is, GUI-wise, "the lectern editor with a different access policy and
optional per-row signature line".

### Drawable surface — NO vanilla precedent found

No vanilla block offers a freehand drawable/paint surface. The Sign block stores *text*,
not a raster/vector canvas; nothing in the shipped API exposes a per-block pixel buffer or
a stroke-capture GUI element. `GuiElement` subclasses are text/slot/inset/scrollbar
oriented; there is no `GuiElementCanvas`. **Conclusion: the chalkboard's drawing surface
has no reusable precedent and must be built from scratch** — a custom `GuiElement`
subclass that captures mouse-drag polylines and renders them (Cairo in `ComposeElements`,
or per-frame in `RenderInteractiveElements`), plus a brand-new Core stroke model and a new
codec. This is materially more work than the text board and is the main reason to split it
into a **later sub-change** (see below).

## C# data structures

Respect the `src/Core` (no VS API) vs. `src/Mod` (adapter) split. Everything here that is a
model/rule lives in Core with unit tests; everything that touches `ICoreAPI`, packets, GUI,
or the calendar lives in Mod.

### Core — access policy as a game-agnostic concept

Public-vs-private is currently an implicit Mod-side notion (`lockHolderUid` on the block
entity). Lift the *policy* into Core so it is testable without the game:

```csharp
namespace Scribe.Core;

/// <summary>Who may edit a document, independent of the VS API. The Mod layer maps this
/// onto land-claim checks and the editor lock; Core only expresses the rule.</summary>
public enum ScribeAccessPolicy : byte
{
    /// <summary>Private: only the owner UID may enter the editor (lectern/desk today).</summary>
    OwnerOnly = 0,
    /// <summary>Public: anyone may read and post; no owner-editor gate (bulletin board).</summary>
    Public = 1,
    /// <summary>Public but append-only: existing entries are immutable (guestbook).</summary>
    PublicAppendOnly = 2,
}
```

Note Core cannot *enforce* land claims (that is a VS concept) — it only models the
Scribe-owned policy. The block entity still combines this with `World.Claims.TryAccess`.

### Core — signature / attribution on a block

Add attribution to `ScribeBlock` (append to the class; the codec already versions its
format, so bump `Version` and read old saves without a signature):

```csharp
public sealed class ScribeBlock
{
    // …existing: Kind, Text, Done, Depth, Pinned, AssignedToUid…

    /// <summary>Stable UID of the writer, captured server-side at write time. Null = the
    /// entry is anonymous/unsigned. Distinct from AssignedToUid (who a task is FOR).</summary>
    public string? AuthorUid { get; set; }

    /// <summary>Display name of the writer at write time (may be stale after a rename;
    /// re-resolve from AuthorUid when a live player list is available). Null = unsigned.</summary>
    public string? AuthorName { get; set; }

    /// <summary>Numeric calendar stamp when the entry was written, for guestbook "day X"
    /// display and chronological sort. Null = untimestamped. Stored as whole + fractional
    /// in-game days since world start (IGameCalendar.TotalDays), formatted for display in
    /// the Mod layer only.</summary>
    public double? WrittenOnTotalDays { get; set; }
}
```

`AuthorUid`/`AuthorName`/`WrittenOnTotalDays` are all *optional* — a private lectern leaves
them null and behaves exactly as today. Only the board/guestbook populate them. Keep the
"trim, reject blank task text" rules unchanged.

Codec impact (`ScribeDocumentCodec`): bump `Version` from 3 to 4; per-block, after the
existing fields, write `hasAuthorUid`+string, `hasAuthorName`+string,
`hasWrittenOn`+double. Preserve the "read older versions" promise: a v4 reader must still
accept v3 bytes (treat the three new fields as null). The current codec hard-rejects any
version != Version — **this must change to a version-aware read** as part of v6 (or
earlier, whenever the first migration lands). Flag: this is the first real format migration
the codec faces; get the version-branching read right here.

### Core — shared/append-only document behavior

Options considered:

- **(A) Reuse `ScribeDocument` with a policy field.** Add
  `ScribeAccessPolicy Policy { get; }` to `ScribeDocument` and an
  `AppendEntry(text, authorUid, authorName, writtenOnDays)` method that respects it. Under
  `PublicAppendOnly`, `SetBlockText`/`DeleteBlock`/`MoveBlock`/`ToggleTask` all return
  `false` for existing blocks (no mutation), and only `AppendEntry` succeeds. Simplest;
  keeps one model and one codec.
- **(B) A separate `ScribeGuestbook` type.** Cleaner conceptually but duplicates
  serialization, GUI, and sync for little gain.

**Recommendation: (A).** The guestbook is "a public document whose policy forbids editing
existing entries", not a new artifact type. Sketch:

```csharp
public sealed class ScribeDocument
{
    public ScribeAccessPolicy Policy { get; set; } = ScribeAccessPolicy.OwnerOnly;

    /// <summary>Append a fully-attributed entry. Under PublicAppendOnly this is the ONLY
    /// mutation allowed; under Public/OwnerOnly it behaves like AddTextSection but carries
    /// signature + timestamp. Returns false on blank text where blank is disallowed.</summary>
    public bool AppendEntry(string text, string? authorUid, string? authorName, double? writtenOnDays);

    // Existing mutators gain a guard: if Policy == PublicAppendOnly and the target is an
    // existing block, return false (immutable log). Adding at the end is still allowed.
}
```

Persist `Policy` in the codec (one byte, alongside the version). This keeps the codec the
single source of truth for round-tripping and lets a block declare its policy from its
block-type JSON at placement.

### Chalkboard — a distinct stroke model (self-contained, later sub-change)

Do **not** shoehorn drawings into `ScribeDocument` (text blocks). A separate Core model +
codec, still game-agnostic:

```csharp
namespace Scribe.Core.Drawing;

/// <summary>One freehand stroke: an ordered polyline in normalized [0,1] surface
/// coordinates (resolution-independent), plus color and width. Author fields mirror
/// ScribeBlock so a stroke can be attributed/erased-by-author on a public board.</summary>
public sealed class ScribeStroke
{
    public IReadOnlyList<(float X, float Y)> Points { get; }
    public int ColorRgba { get; set; }
    public float Width { get; set; }
    public string? AuthorUid { get; set; }
}

public sealed class ScribeDrawing
{
    public IReadOnlyList<ScribeStroke> Strokes { get; }
    public bool AddStroke(ScribeStroke s);
    public bool Clear();          // policy-gated Mod-side (public: anyone? owner? erase-own?)
    public bool RemoveLastByAuthor(string authorUid);
}

public static class ScribeDrawingCodec { /* magic "SCDR", versioned, like the doc codec */ }
```

**Sync cost is the reason to defer this.** Stroke lists grow unbounded and change on every
mouse-drag; naively re-serializing and re-syncing the whole drawing on each stroke (the
lectern's whole-document sync model) is fine for a handful of text rows but wasteful for a
busy canvas. A real chalkboard likely wants *incremental* "append these strokes" packets
rather than whole-surface resync — a different sync shape than the text board. This, plus
the from-scratch custom `GuiElement`, is why the recommendation is: **ship the text
bulletin board + guestbook + signatures in v6-core; make the drawable chalkboard a v6.1
sub-change** unless the user wants it in scope (Open Question).

### Mod — adapters & packets

- `BlockScribeBoard : Block` + `BlockEntityScribeBoard : BlockEntity` — parallels the
  lectern pair. The block-type JSON declares the policy (`Public` vs. `PublicAppendOnly`
  for the guestbook), read into `ScribeDocument.Policy` on placement/first-load.
- **Reuse `ScribeEditDocumentMessage`** for the board's text edits — its shape (pos + doc
  bytes + granted/refusal) already fits. The server captures `fromPlayer.PlayerUID` /
  `PlayerName` and stamps `AppendEntry` there, so the *client-submitted* bytes never carry
  a self-asserted signature (the server overwrites/sets it). For a **public** board there is
  no editor lock, so `granted` is effectively always true (still keep the land-claim check).
- A small **guestbook append packet** (client → server: just the new line's text) is
  cleaner than shipping the whole document for an append-only log; server builds the
  attributed+timestamped entry and resyncs. Optional; could reuse the edit message with a
  server-side append instead of replace.
- `BlockEntityScribeBoard` overrides `ToTreeAttributes`/`FromTreeAttributes` exactly like
  the lectern; the codec now carries `Policy`, signatures, and timestamps, so no new tree
  keys are needed beyond the existing `scribeDocument` bytes.
- Chalkboard (if in scope): `BlockEntityScribeChalkboard` persisting `ScribeDrawing` bytes
  under a new key; a new `ScribeDrawStrokeMessage` (incremental append) on the same channel.

## Implementation spec

Ordering, assuming the row-list rework (S1 done, S2 pending — see MEMORY / session-notes)
has landed so the shared row renderer is stable.

1. **Codec migration to versioned read (foundational).** Change
   `ScribeDocumentCodec.TryDeserialize` from "reject if version != Version" to a
   version-branched read: v3 path (no author/timestamp/policy), v4 path (with them). Bump
   `Version = 4`. Add round-trip + "read a v3 blob" unit tests. This unblocks everything
   else and is the one place a mistake corrupts existing saves.
2. **Core: `ScribeAccessPolicy`, block author/timestamp fields, `Document.Policy`,
   `AppendEntry`, append-only guards.** Pure Core; unit-test the policy matrix (public
   edit allowed, append-only edit rejected, append always allowed, blank rules).
3. **Mod: `BlockScribeBoard`/`BlockEntityScribeBoard` (Public policy).** Copy the lectern
   pair; **remove the owner/lock gate** (public = no `lockHolderUid`; keep
   `World.Claims.TryAccess`). Block-type JSON + lang keys + a placeholder model (reuse an
   existing shape initially, per the lectern's "reuse vanilla shape" precedent). Wire the
   existing GUI dialog against the board entity.
4. **Mod: signature capture.** In the server edit handler, set `AuthorUid`/`AuthorName`
   from `fromPlayer` on newly-added/edited board entries; render a signature line in
   `ScribeBlockRowCell` for board documents (a small dim "— Name" under the row). Make it a
   read-only display; the client cannot set it.
5. **Mod: guestbook variant.** A second block-type using `PublicAppendOnly` policy + a
   minimal "add entry" GUI (single text field + submit) rather than the full editor;
   server stamps `WrittenOnTotalDays` from `sapi.World.Calendar.TotalDays` and formats "Day
   X, Year Y" (or `PrettyDate()`) for display. Entries render newest-or-oldest-first
   (Open Question) and are immutable.
6. **(Later sub-change v6.1) Chalkboard.** New Core `Scribe.Core.Drawing` model + codec +
   tests; custom `GuiElement` stroke-capture/render; `BlockEntityScribeChalkboard`;
   incremental stroke-append packet. Playtest sync cost with several simultaneous drawers
   before committing to whole-surface vs. incremental sync.

Persistence/sync for steps 3–5 is the **exact Sign pattern already in the lectern** — no
new persistence mechanism. The only new sync consideration is that a *public* board can be
edited by anyone, so concurrent-edit contention is higher than the single-owner lectern;
for the text board, keep the lectern's whole-document server-authoritative replace but
consider whether a public board still wants *some* lock (Open Question) to avoid two
players clobbering each other's in-flight text. The append-only guestbook sidesteps this
entirely (appends never conflict).

## Dependencies & sequencing

- **Prereq: shared row-list renderer (row-list-rework S2).** The board's editor is the
  lectern editor with a different policy; land the unified renderer first so the board
  doesn't fork GUI code. (S1 done/archived per MEMORY; S2 pending.)
- **Prereq: codec versioned-read migration.** First real format migration; do it as step 1
  of v6, carefully, with a "reads v3" test.
- **Shared-doc infra is reused by the guestbook.** The guestbook is a *policy* over the
  same `ScribeDocument`/codec/block-entity/sync, not new infrastructure — its whole point
  is to be cheap once the board exists. Do the board first, guestbook second.
- **Chalkboard depends on nothing in the text board** (separate model/codec/GUI) but is the
  most expensive piece; recommended as **v6.1**, after v6-core ships and is playtested.
- **Relation to v4 writing desk / faction assignment.** The desk's "shared owner list /
  faction task assignment" idea (ROADMAP) is a *different* sharing model (a defined group,
  possibly leader-locked) than the board's *fully public* one. `ScribeAccessPolicy` should
  be designed with a future `SharedGroup` member in mind (don't paint it into a
  public/private binary), but v6 only needs `OwnerOnly`/`Public`/`PublicAppendOnly`.

### Wax-seal soft security — Envelopes interop vs. native (tradeoff analysis)

**Recommendation: DEFER; if pursued later, build native — do NOT take an Envelopes
dependency.** Rationale, per the on-record decision ("pursue only if a cheap Envelopes
interop; otherwise skip / native later"):

- **Envelopes is items-only with no public API.** Its README describes sealing a
  *parchment letter* inside an *envelope item* via the crafting grid, storing only a
  content-reference ID + the sealer's name on the item, with tamper evidence shown as a
  broken-seal graphic on unseal. There is no documented interface, method surface, or
  extension point for another mod to seal *its own* documents or blocks. (Confirmed from the
  repo README; no API code shown.)
- **Shape mismatch.** Scribe's private docs are a **block** (lectern/desk) and, from v2, a
  **docId-on-item** notebook. Envelopes wraps a *specific parchment item* in a *specific
  envelope item*; it has no concept of "seal this block's document" or "seal this docId".
  Interop would mean reverse-engineering its internal attributes (like the Achievements
  dead-end in ROADMAP) — the opposite of "cheap".
- **A hard dep is disallowed anyway** (CLAUDE.md: vanilla + ConfigLib soft-dep only). Even a
  *soft* `IsModEnabled("envelopes")`-gated integration would still require a usable API,
  which does not exist. So the soft-dep escape hatch (the ConfigLib pattern) does not help
  here — there is nothing to call.
- **Native is conceptually cheap but not free, and not urgent.** A native wax seal is: a
  boolean `Sealed` + `SealedByUid`/`SealColor` on the document, an "open evidence" flag set
  when a non-owner reads a sealed private doc (tamper leaves evidence, not a hard lock —
  matching VS's grounded ethos), plus item/art for the wax. This fits Scribe's own model
  with no dependency, but it needs art/animation and touches the private-doc read path — a
  polish-tier effort, not v6-core. The "Sign button prevents further modification" pattern
  on the vanilla writable book (from the Ink_and_quill wiki page) is the closest vanilla
  precedent for an immutability toggle, if a native seal is later built.

Net: wax-seal is **out of v6**; recorded here as "native-later, no Envelopes dep". Confirm
with the user (Open Question) only whether to formally close the Envelopes-interop door.

## Open questions

1. **Chalkboard scope.** Is the drawable chalkboard in v6, or a v6.1 sub-change? This spec
   recommends v6.1 (from-scratch custom `GuiElement` + separate stroke model/codec +
   different sync shape). Confirm.
2. **Signatures: opt-in per entry, or board-wide policy?** — **DECIDED 2026-07-21: option (a),
   always attributed on public boards.** Every public-board entry is signed with the writer's
   name; no anonymous posting on public boards. The nullable author fields stay (a *private*
   lectern/desk still leaves them null), but a public board always populates them server-side.
   This also settles question 6 (deletion): author-match is always available, so "author or
   land-claim-owner may delete" is enforceable. Guestbooks remain always-signed (their point).
   (ROADMAP Open decision #5.)
3. **Public board concurrency.** — **DECIDED 2026-07-21: lock-free, last-write-wins.** The
   editable text bulletin board takes NO editor lock — anyone opens the editor, whole-document
   last-write-wins on save. Chosen for the "public feels like simultaneous posting" reason;
   made safe-enough by the always-attributed decision above (every clobber is traceable to a
   named author). If in-flight clobbering proves painful at playtest, revisit toward per-block
   merge rather than reintroducing a lock. The append-only guestbook never contends. (ROADMAP
   Open decision #5.)
4. **Guestbook entry order & timestamp display.** Newest-first or chronological? Store
   `TotalDays` (recommended, game-agnostic) and format "Day X, Year Y" in the Mod layer, or
   store `PrettyDate()` output directly? (Recommended: store numbers, format Mod-side.)
5. **Wax-seal / Envelopes.** Formally close the Envelopes-interop door and mark wax-seal as
   "native, later, out of v6"? (This spec recommends yes.)
6. **Board erasing on a public board.** Who may delete/clear entries on a fully public
   task board — anyone (griefing risk), only the author of an entry (needs `AuthorUid`
   match, which signatures already give us), or land-claim-owner-only? Interacts directly
   with question 2/3.
