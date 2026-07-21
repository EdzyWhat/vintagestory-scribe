# Chronicle & Integrations

> Architect-level *exploration* spec (2026-07-21). NOT an OpenSpec change, NOT implemented
> code. When any sub-feature is picked up, this becomes the input to a real
> `openspec-propose`. See `docs/specs/README.md` for the shared structure and guardrails.

## Summary

This spec merges the roadmap's "self-writing journal" cluster with its two external
integrations and the program-level cross-cutting workstreams. The unifying idea: a Scribe
document should be able to grow *on its own* from things that happen in the world, and
optionally reach *out* of the world. Five feature threads plus four meta workstreams:

1. **Death leaves a last entry** — on player death, auto-append a timestamped/located final
   entry to the player's current document, so a notebook dropped on death becomes an
   in-fiction gravesite artifact.
2. **Calendar-stamped entries + passive chronicle-building** — auto-stamp entries with VS's
   real in-game calendar; auto-log recurring world events (survived a temporal storm on Day
   X); auto-archive completed task lists into a read-only section.
3. **Milestone-suggested tasks** — self-detect vanilla tech milestones (first bronze/steel/
   fired-clay) and surface ONE easily-dismissed suggested task. Zero third-party dependency
   (decision on record; three trigger mods researched and rejected).
4. **Slack push integration** — hidden/advanced config pushing one-way task-change
   notifications OUT to Slack via Incoming Webhooks. Config-gated, undiscoverable, the
   webhook URL is the secret.
5. **Handbook bookmarking** — bookmark a Survival Handbook page into a notebook as a task.
   Deepest API integration; late-stage, its own change.

Meta workstreams (spec lightly, as program-level concerns not features): cross-world
export/import (JSON), localization (`lang/` key structure), a Handbook/wiki authoring pass,
and crediting JeanPierre (Wanderer's Sketchbook).

**Architecture through-line (the load-bearing invariant):** every one of these features
reads game state or touches the network/OS — *all of that lives in `src/Mod/`*. `src/Core/`
gains only game-agnostic data: an "auto-entry" block variant, entry timestamp/location
*metadata* (plain primitives, no `BlockPos`/`GameDate` types), a "suggested task" flag, and
a per-player "milestones seen" set. Core never learns what a temporal storm or a webhook is.

---

## VS API hooks (grouped per sub-feature)

### 1. Death last-entry

- **`sapi.Event.PlayerDeath`** — `event PlayerDeathDelegate PlayerDeath` on
  `IServerEventAPI`. Signature (confirmed by decompiling `VintagestoryAPI.dll`,
  `PlayerDeathDelegate`): `void PlayerDeathDelegate(IServerPlayer byPlayer, DamageSource
  damageSource)`. Server-side, fires once per death, gives us the player identity directly —
  matches the "mod already knows player identity at write time" guardrail.
- **Player position at death:** `byPlayer.Entity.Pos` / `.ServerPos` (`EntityPos` → `AsBlockPos`
  for a coarse coordinate). `damageSource.Type` / `damageSource.Source` give a cause string
  ("fell", "eaten by a drifter") we can fold into the entry text.
- **Which document to append to:** requires the v2 held-notebook `docId` store — a lectern is
  positional and not "the player's current document." See Dependencies. Until then, this
  feature is *specced but not buildable* (v1 has only positional lectern docs).

### 2. Calendar-stamped entries + passive chronicle

- **`api.World.Calendar`** → **`IGameCalendar`** (confirmed via decompile). Read-only fields
  we need: `Year` (starts 1386), `DayOfYear` (0..`DaysPerYear`), `MonthName`
  (`EnumMonth`), `GetSeason(BlockPos)` (`EnumSeason`), `TotalDays`/`TotalHours` (monotonic,
  good as a sortable stamp and for debounce/dedup windows), `HourOfDay`. **`PrettyDate()`**
  returns the engine's own nicely-formatted date string — use it for display so our stamp
  matches vanilla's date formatting, but ALSO capture the numeric `TotalHours` for a stable,
  locale-independent sort/dedup key stored in Core.
- **Temporal storm logging:** the storm system is **`SystemTemporalStability`** in
  `VSSurvivalMod.dll` (NOT the API DLL — it is survival-mod content, so treat as "present in
  a normal survival world, absent in a barebones creative world"; guard on presence). Confirmed
  by decompile: it broadcasts a **`TemporalStormRunTimeData`** packet on a network channel
  named **`"temporalstability"`** whenever `data.nowStormActive` flips true (and again when it
  ends), and calls `sapi.BroadcastMessageToAllGroups(...Imminent/Approaching/Waning...)`.
  Two viable hooks, in preference order:
  1. **Register our own client/server message handler on the existing `"temporalstability"`
     channel** and watch `nowStormActive` transition false→true. Clean, event-driven, no
     reflection. (Registering an additional handler on a channel another mod owns needs
     verification — see Open Questions.)
  2. **Poll** `sapi.ModLoader.GetModSystem<SystemTemporalStability>()?.StormData.nowStormActive`
     on a slow server tick (`RegisterGameTickListener`, e.g. every 2s as the storm system
     itself does) and detect the edge. Reflection-free if the type is referenceable; robust.
- **Completed-task auto-archive:** pure Core operation (move `Done` tasks into a read-only
  archive section) triggered by a Mod-side calendar boundary (e.g. season/year rollover
  detected by comparing `Calendar.Year`/`GetSeason` against a persisted last-seen value). No
  new API surface beyond the calendar read.

### 3. Milestone self-detection

- **No first-party global "onCraft"/"onSmelt" event exists in the API.** Confirmed: the only
  crafting hook is the instance method `Collectible.OnCreatedByCrafting(...)` (an override on
  the item, not a subscribable event), and `MatchGridRecipeDelegate` is a recipe-match filter,
  not a completion signal. So milestone detection is **inventory-state polling**, not
  event-driven:
  - **`sapi.Event.RegisterGameTickListener(onMilestoneTick, intervalMs)`** on a slow interval
    (e.g. 5–10s) — cheap, deterministic, dependency-free.
  - For each online `IServerPlayer`, walk the player inventory
    (`player.Entity.WalkInventory(...)` / `player.InventoryManager`) and test each
    `ItemSlot.Itemstack.Collectible.Code` against a small table of milestone
    `AssetLocation` patterns (e.g. `game:ingot-tinbronze`, `game:ingot-steel`,
    fired-clay via the relevant `game:*` codes). First match that isn't already in the
    player's "seen" set → fire the suggestion, add to seen.
  - Alternative finer-grained hook: **`sapi.Event.DidUseBlock`** (`BlockUsedDelegate(IServerPlayer,
    BlockSelection)`, confirmed) to catch "used an anvil/forge/kiln," but inventory-scan is
    simpler and catches the milestone regardless of *how* the item was obtained (trade, gift),
    which better fits "the player has reached this tech level." Prefer inventory-scan.
- **Per-player persistent "seen" store:** **`IServerPlayer.SetModData<T>(key, value)` /
  `GetModData<T>(key, default)`** (confirmed on `IServerPlayer`) — arbitrary, permanently
  stored, per-player, NOT synced to client. This is the correct home for the milestones-seen
  set so a milestone fires exactly once per player for all eternity. (Raw byte variant
  `SetModdata`/`GetModdata` also exists if we want to store our own serialized blob.)

### 4. Slack push (one-way, Scribe → Slack)

- **HTTP client:** confirmed **no HTTP type ships in `VintagestoryAPI.dll`** — HTTP is not a
  game-API concern. The mod targets **`net10.0`** (confirmed in `Directory.Build.props`), so
  **`System.Net.Http.HttpClient` from the .NET 10 BCL is available directly** — no new
  package, satisfies "plain .NET HTTP, no library." Use a single long-lived static
  `HttpClient` (BCL best practice; avoid per-call construction/socket exhaustion).
- **Slack Incoming Webhooks:** a plain `POST` of `{"text": "..."}` JSON to the webhook URL.
  No auth header — **the URL itself is the credential.** Serialize the JSON with the already-
  referenced `Newtonsoft.Json` (in Mod's references) or a hand-built string; no new dep.
- **Where it runs:** server-side only (server is authoritative and already observes task
  changes). Fire from the same server path that persists a task change (the
  `ApplyEdit`/`ToggleTaskFromReader` completion points in `BlockEntityScribeLectern`, and the
  equivalent for future held docs), **debounced** (see Implementation).
- **Config storage:** a server-side config file (mirroring the existing
  `ScribeClientConfig`/`scribe-client-config.json` pattern, but server-scoped:
  `scribe-server-config.json`), read via `api.LoadModConfig`/`StoreModConfig`. NOT ConfigLib-
  exposed (must stay undiscoverable). The webhook URL field is never logged and never placed
  in any networked packet or `ToTreeAttributes` blob.

### 5. Handbook bookmarking

- **Page identity:** `GuiHandbookPage.PageCode` (abstract string on the base
  `GuiHandbookPage`, confirmed). For item/block pages it's produced by
  **`GuiHandbookItemStackPage.PageCodeForStack(ItemStack)`** →
  `"<class>-<Code.ToShortString()>-<attrs>"`, or a collectible's own
  `IHandBookPageCodeProvider.HandbookPageCodeForStack(...)` when implemented.
- **Opening a page from a code:** the survival handbook is **`ModSystemSurvivalHandbook`**
  (in `VSSurvivalMod.dll`), which owns a `GuiDialogHandbook` and exposes
  **`dialog.OpenDetailPageFor(string pageCode)`** (returns bool = found). To jump to a
  bookmarked page: `capi.ModLoader.GetModSystem<ModSystemSurvivalHandbook>()`, open its
  dialog, call `OpenDetailPageFor(savedPageCode)`. This is the survival mod again → same
  presence-guard caveat as temporal storms.
- **Capturing a bookmark:** the handbook hotkey handler already computes a page code from the
  currently-looked-at block/entity/stack (decompiled: `OnSurvivalHandbookHotkey` derives
  `pageCode` from `capi.World.Player.CurrentBlockSelection` etc.). A Scribe "bookmark current
  handbook page as a task" affordance would reuse that derivation.

---

## C# data structures

### Core (game-agnostic — NO Vintage Story API, NO `BlockPos`/`GameDate`/`AssetLocation`)

**Auto-entry as a `ScribeBlock` extension, not a new top-level type.** The document is
already an ordered list of `ScribeBlock`s (`ScribeBlockKind { Task, Text }`). Auto-generated
content (death entry, storm log, calendar-stamped chronicle line) is fundamentally *text the
system wrote* — model it by extending the existing block, keeping one document model:

```csharp
public enum ScribeBlockKind : byte
{
    Task = 0,
    Text = 1,
    // Append only (persisted as a byte; never renumber — codec rule).
    ChronicleEntry = 2, // system-authored, read-only journal line
}

public sealed class ScribeBlock
{
    // ... existing Kind/Text/Done/Depth/Pinned/AssignedToUid ...

    /// <summary>True for system-authored content the user cannot edit/delete via the normal
    /// editor (death entry, storm log, archived task). Purely a data flag; enforcement lives
    /// in the mutation methods + GUI. Default false.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Optional entry metadata, set when a block was auto-stamped. All primitives so
    /// Core stays game-agnostic — the Mod adapter fills these from IGameCalendar / EntityPos.</summary>
    public ChronicleStamp? Stamp { get; set; }
}

/// <summary>Game-agnostic timestamp/location metadata. Populated by the Mod layer from the VS
/// calendar and player position; Core only stores and round-trips it. No VS types here.</summary>
public sealed class ChronicleStamp
{
    public int Year { get; set; }
    public int DayOfYear { get; set; }
    public double TotalHours { get; set; }   // stable monotonic sort/dedup key
    public string? SeasonKey { get; set; }   // e.g. "spring" (lang key stem), NOT EnumSeason
    public string? DisplayDate { get; set; } // engine PrettyDate() snapshot, for display
    // Location: nullable primitives so a location-less stamp (calendar-only) is representable.
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Z { get; set; }
}
```

**Suggested-task flag** — reuse `ScribeBlock` with a flag rather than a parallel type, so a
suggestion that the player *keeps* becomes an ordinary task with zero migration:

```csharp
public sealed class ScribeBlock
{
    /// <summary>A system-suggested task the player has not yet accepted. Rendered with an
    /// accept/dismiss affordance; dismiss deletes it, accept clears the flag → plain task.</summary>
    public bool Suggested { get; set; }
}
```

**Per-player milestones-seen set** — a tiny game-agnostic value object the Mod serializes into
per-player ModData:

```csharp
/// <summary>Which one-time milestones a player has already been shown. Game-agnostic: holds
/// opaque string keys ("bronze", "steel", "firedclay"), decided/matched in the Mod layer.
/// Core just stores the set + round-trips it, so it's unit-testable without a game.</summary>
public sealed class MilestoneProgress
{
    private readonly HashSet<string> _seen = new();
    public IReadOnlyCollection<string> Seen => _seen;
    public bool HasSeen(string key) => _seen.Contains(key);
    public bool MarkSeen(string key) => _seen.Add(key); // false if already present
}
```

**Codec impact:** `ScribeDocumentCodec` bumps `Version` to 4 and appends the new
per-block fields (`ReadOnly`, `Suggested`, an optional `Stamp` guarded by a `hasStamp`
bool) *after* the existing ones, following the existing append-only discipline. Add a
tiny `MilestoneProgressCodec` (same hand-rolled binary style) for the ModData blob.

**New Core mutation methods** (keep the "return bool, never throw" convention):
`AddChronicleEntry(text, stamp)`, `ArchiveCompletedTasks()` (moves `Done` tasks to
read-only, returns count), `AddSuggestedTask(text)`, `AcceptSuggestion(index)` /
`DismissSuggestion(index)`. Editor-facing methods (`SetBlockText`, `DeleteBlock`) must
reject blocks with `ReadOnly == true`.

### Mod (all VS-API-touching adapters)

- **`ChronicleService`** (server-side) — the calendar adapter. Wraps `api.World.Calendar`;
  builds a `ChronicleStamp` from `IGameCalendar` + an optional `EntityPos`. Single place that
  knows `IGameCalendar` → keeps the mapping testable-by-inspection and out of Core.
- **`DeathChronicleListener`** — subscribes `sapi.Event.PlayerDeath`; resolves the player's
  current document (v2 held-notebook `docId`), calls `doc.AddChronicleEntry(...)`, persists.
- **`StormChronicleListener`** — handler/poller for `SystemTemporalStability`; on a
  false→true storm edge, appends a chronicle entry to each affected player's current doc.
- **`MilestoneDetector`** — the `RegisterGameTickListener` inventory scanner + a
  `MilestoneTable` mapping `AssetLocation` patterns → milestone keys → suggested-task lang
  keys. Reads/writes `MilestoneProgress` via `IServerPlayer.GetModData`/`SetModData`.
- **`SlackPushClient`** — owns the static `HttpClient`; `PostTaskChangeAsync(string message)`;
  a debounce timer (coalesce a burst of edits into one POST). Reads the webhook URL from
  `ScribeServerConfig`. Never logs the URL.
- **`ScribeServerConfig`** — POCO mirroring `ScribeClientConfig`, loaded via
  `api.LoadModConfig<ScribeServerConfig>(...)`; fields: `SlackWebhookUrl` (nullable, default
  null = disabled), `SlackDebounceSeconds` (default e.g. 10), `ChronicleAutoStamp` (bool),
  `MilestoneSuggestionsEnabled` (bool).
- **`HandbookBookmarkAdapter`** — derives a `PageCode` from the current selection (capture)
  and calls `ModSystemSurvivalHandbook.OpenDetailPageFor` (jump). Stores the page code as the
  task's text payload (or a dedicated field — see Open Questions) in a `ScribeBlock`.
- No new network *packets* are strictly required for chronicle features (server mutates the
  authoritative doc and re-syncs via the existing `SendReply`/`MarkDirty` path). Milestone
  suggestions ride the same document-sync channel.

---

## Implementation spec (per sub-feature)

### 1. Death last-entry

1. In `StartServerSide`, subscribe `sapi.Event.PlayerDeath += OnPlayerDeath`.
2. `OnPlayerDeath(byPlayer, damageSource)`: locate the player's *current* document. This
   needs the v2 held-notebook store (a `docId` on the held item → server doc store). Resolve
   the notebook the player was carrying; if none, no-op (a player with no notebook leaves no
   entry — acceptable and in-fiction).
3. Build a `ChronicleStamp` via `ChronicleService` (calendar + `byPlayer.Entity.ServerPos`).
4. `doc.AddChronicleEntry(text, stamp)` where `text` is a localized template
   (`scribe:chronicle-death`, args: cause from `damageSource.Type`, pretty date). Mark
   `ReadOnly`.
5. Persist through the doc store's normal save path. VS's own item-drop-on-death then drops
   the notebook with the entry already written — the gravesite-artifact payoff needs no extra
   code.

### 2. Calendar-stamped entries + passive chronicle

1. **Manual entry stamping:** when a player adds a `Text`/chronicle entry (and config
   `ChronicleAutoStamp` is on), the Mod fills its `ChronicleStamp` from `ChronicleService`
   before persisting. Display uses `Stamp.DisplayDate`; sort/dedup uses `Stamp.TotalHours`.
2. **Storm logging:** `StormChronicleListener` detects a false→true `nowStormActive` edge
   (handler on `"temporalstability"` channel, or poll — see Open Questions), then appends a
   `scribe:chronicle-storm` entry (args: pretty date) to each online player's current doc.
   Debounce so a single storm logs once (track last-logged `TotalDays`).
3. **Completed-task auto-archive:** on a season/year rollover (compare `Calendar.Year` +
   `GetSeason` to a persisted last-seen value in world ModData), call
   `doc.ArchiveCompletedTasks()` on eligible docs, moving `Done` tasks into a `ReadOnly`
   archive region rendered as a collapsed "Archived (N)" section. Config-gated;
   default OFF for a first cut (auto-mutating a user's list is opinionated — see Open Qs).

### 3. Milestone self-detection

1. `MilestoneDetector` registers a slow tick (`RegisterGameTickListener`, ~5–10s).
2. Each tick, for each online `IServerPlayer`: load `MilestoneProgress` from `GetModData`;
   scan inventory; for the first milestone whose item is present and NOT in `Seen`:
   `progress.MarkSeen(key)`, `SetModData`, and append ONE `AddSuggestedTask(localizedText)`
   to that player's current document. Exactly one suggestion per milestone per player, ever.
3. Never queue: if multiple milestones cross between ticks, still surface only the highest-
   priority single suggestion that tick; the rest surface on later ticks (still one-at-a-time),
   and only if the player hasn't got a pending suggestion already (no nag).
4. GUI: a suggested task renders with a subtle accept (✓ → becomes a normal task) / dismiss
   (✕ → deleted) affordance. Dismiss is one click; that's the "easily-dismissed" requirement.

### 4. Slack push

1. `SlackPushClient` holds `static readonly HttpClient` (BCL, net10).
2. On any persisted task change (add/toggle/edit/delete of a `Task`), if
   `ScribeServerConfig.SlackWebhookUrl` is non-null, enqueue a debounced push. Debounce:
   reset a timer to `SlackDebounceSeconds` on each change; on fire, POST one summary message
   (e.g. "N tasks changed on <doc>"), never one-per-keystroke.
3. POST body `{"text": "..."}`, `Content-Type: application/json`, fire-and-forget with
   `try/catch` (a Slack outage must never break gameplay or throw into the tick). Log
   *failures generically* ("Scribe: Slack push failed") — **never** log the URL or body.
4. One-way only: no inbound listener, no callback endpoint, no OAuth. Documented limits
   (fixed channel, no delete, unpublished rate limit) live in ROADMAP.

### 5. Handbook bookmarking (late-stage, own change)

1. Add a "bookmark this handbook page" affordance (a keybind or a button in the handbook
   context) that captures the current `PageCode` via the same derivation
   `ModSystemSurvivalHandbook` uses.
2. Store the page code on a task block (as a structured reference — see Open Qs), text like
   "Craft: <page title> once I have iron."
3. Clicking the bookmarked task opens the handbook to that page via `OpenDetailPageFor`.
4. Guard the whole feature on `ModSystemSurvivalHandbook` being loaded (absent in some
   worlds); degrade to a plain-text task if not.

### Meta workstreams (program-level, spec lightly)

- **Localization** — start NOW (v1-era), before more strings accrue. Keep the flat
  `assets/scribe/lang/en.json` structure already in use; every new string in this spec gets a
  `scribe:`-prefixed key at author time (the domain-prefix rule from VSAPI-NOTES.md — every
  `Lang.Get` must be `"scribe:<key>"`). Reserve key families now:
  `scribe:chronicle-death`, `scribe:chronicle-storm`, `scribe:chronicle-archived`,
  `scribe:milestone-<key>` (e.g. `-bronze`, `-steel`, `-firedclay`),
  `scribe:suggestion-accept`, `scribe:suggestion-dismiss`. Adding `de.json`/etc. later is
  then a pure translation drop with no code changes.
- **Cross-world export/import (JSON)** — a Core-level `ScribeDocument` ⇄ JSON codec (parallel
  to the binary codec, human-readable, versioned) + a Mod-side command/GUI to dump/load a doc
  to/from a file in the game's data dir. Naturally reuses the export mental model of
  Wanderer's Sketchbook. Belongs after the held-notebook store exists (something worth
  exporting). Core work is dependency-free and can start early.
- **Handbook/wiki authoring pass** — documentation, not code; do near shipping once tiers are
  stable. Spans all features here (documents which auto-features live on which artifact).
- **Credit JeanPierre (Wanderer's Sketchbook)** — add a `CREDITS.md` (or a CREDITS section)
  crediting the data-model/GUI inspiration; trivial, do anytime.

---

## Security note (Slack webhook secret handling)

- **The webhook URL is the sole credential.** Treat it exactly as a password:
  - Read only from the server-side config file; **never** place it in a `ToTreeAttributes`
    blob, a network packet, the document codec, or any client-visible state. It stays on the
    server process only.
  - **Never log it** — not at info, not at debug, not in an exception message. Scrub it from
    any error string (log "Slack push failed" without the URL/body). A `HttpRequestException`
    can contain the request URI; catch and log a fixed generic message instead of `ex.ToString()`.
  - Do **not** expose it via ConfigLib (that would surface it in an in-game panel and make the
    feature discoverable — both unwanted). It lives only in the hand-edited server config file.
  - It is server-scoped, not per-player — so it is not part of any player's synced data and
    cannot leak to clients through the normal doc-sync path.
- The push is outbound-only over HTTPS to a user-supplied host; wrap in try/catch so a
  malicious/broken URL can only fail the push, never crash the tick or block the game thread
  (use the async POST; do not `.Result`/`.Wait()` on the tick thread).

---

## Dependencies & sequencing

**Needs the held-notebook / `docId` doc store (v2) first** (a "player's current document" is
meaningless for a positional v1 lectern):
- Death last-entry (1) — hard blocked on v2.
- Storm logging + auto-stamp writing to "your journal" (2) — best on v2 (a lectern *could* be
  stamped, but the payoff is a personal carried chronicle).
- Milestone suggestions (3) — want to append to the player's carried doc; v2.

**Can start early / dependency-light:**
- Core data-model additions (`ChronicleEntry` kind, `ChronicleStamp`, `ReadOnly`/`Suggested`
  flags, `MilestoneProgress`, codec v4) — pure Core, unit-testable now, unblock everything else.
- Localization key-structure discipline — **start immediately** (cheapest to do before strings
  multiply; retrofitting is the expensive path).
- Cross-world JSON export/import Core codec — dependency-free; the Mod side waits for v2.
- CREDITS — anytime.

**Late-stage / own dedicated change:**
- Handbook bookmarking (5) — deepest integration, survival-mod-coupled; explicitly a
  post-core-tiers change per the roadmap decision.
- Handbook/wiki authoring pass — near shipping.

**Feature priority within the buildable set:** Slack (4) is technically the *least* blocked
(it only needs a task-change hook, which v1 already has) but is framed as personal API-
learning and explicitly niche/undiscoverable — so it is a low-priority "whenever" item, not a
gate on anything. Milestone detection (3) and calendar chronicle (2) are the higher-value
"self-writing journal" payoff and should lead once v2 lands.

**Survival-mod coupling caveat (2 storm, 5 handbook):** both `SystemTemporalStability` and
`ModSystemSurvivalHandbook` live in `VSSurvivalMod.dll`, not the API. They're present in a
normal survival world but not guaranteed universally. Every touch must `GetModSystem<T>()`-
guard and degrade gracefully (skip storm logging / fall back to a plain-text task) when
absent — do NOT add a hard reference that assumes they exist.

---

## Open questions

1. **Opt-in vs automatic per feature.** Death last-entry and calendar stamping feel like
   "on by default" immersion; auto-archiving completed tasks *mutates the user's list on a
   schedule* which is more opinionated — should archive default OFF (opt-in) while the
   others default ON? Which of these deserve a per-player toggle vs. a world/server setting?
2. **Storm-logging hook choice.** Register an additional handler on the survival mod's
   existing `"temporalstability"` network channel, or poll `SystemTemporalStability.StormData`
   on a tick? The channel-handler path is cleaner but needs confirming that a second mod can
   attach a handler to a channel it doesn't own without conflict — the poll path is safer but
   less elegant. Preference?
3. **Slack priority.** Given it's framed as a personal excuse to learn the Slack API and is
   deliberately undiscoverable — is it worth building before the higher-value chronicle
   features, or strictly a "spare afternoon" item after v2 lands?
4. **When to start localization key-structure work.** Recommend now (v1-era) so new strings
   are born correctly prefixed — confirm this is acceptable overhead vs. deferring until a
   second language is actually planned.
5. **Handbook bookmark storage shape.** Store the `PageCode` inline in the task's plain
   `Text` (zero schema cost, per the roadmap's "resist structured fields" discipline), or add
   a dedicated `HandbookPageCode` reference field on `ScribeBlock`? Inline keeps Core simpler
   but makes the "click to open the page" affordance rely on parsing text.
6. **Milestone table scope for a first cut.** Which milestones ship first — just the three
   named (bronze, steel, fired-clay), and matched against which exact `game:` item codes?
   (Needs a quick pass over the vanilla item registry to pin the exact `AssetLocation`s.)
```
