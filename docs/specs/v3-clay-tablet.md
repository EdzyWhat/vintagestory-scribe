# v3 — Scratch tier: clay & wax tablets

> Exploration/design spec (not an OpenSpec change, not implemented code). Input to a future
> `openspec-propose`. Follows `docs/specs/README.md`. Merges these ROADMAP.md items: the v3
> **scratch** tier (originally scoped as the **clay tablet**, now covering **two sibling
> artifacts — the clay tablet and the wax tablet**); the parked **stamping mechanic**
> (cross-referenced, not fully specced — a later change); the immersion idea **"Firing a tablet
> is a real crafting decision"** (fired clay → permanent read-only archive); the immersion idea
> **"Fire vs. water asymmetric fragility across tiers"** (which the two scratch artifacts now
> embody *within a single tier* — clay is water-fragile, wax is heat-fragile); and the UX idea
> **"carry-forward migration"** (Bullet-Journal-style, fitting the tight line cap). Cuneiform-
> style stamped font is cross-referenced only (belongs to `presentation-and-fonts.md`).
>
> **File name note:** this file is still `v3-clay-tablet.md` (unchanged, to preserve the three
> ROADMAP.md pointers to it) even though its title now covers the whole scratch tier including
> wax. Rename later if desired.
>
> **The wax tablet (2026-07-21 addendum).** The scratch tier now ships **two** artifacts that
> share almost all infrastructure but sit at opposite ends of the fragility axis:
> - **Clay tablet** — soft/unfired clay; **water-fragile** (wets out); can be **fired** into a
>   permanent, read-only, water-proof archive. Fully specced below; unchanged by this addendum.
> - **Wax tablet** — a beeswax writing surface on a wooden frame (the classic Roman reusable
>   tablet); **NOT water-fragile** (swimming/falling in water does **not** damage or destroy it)
>   and **erasable/reusable in place** (smooth it flat to start over). Wax is deliberately **not
>   strictly better** than clay, but the balance is **material cost, not a punishing drawback**:
>   beeswax is a comparatively expensive, beekeeping-gated material (skep → honeycomb → pressed
>   wax) where clay is dug cheaply everywhere, and — crucially — **clay can be fired into a
>   permanent read-only archive while wax can never be made permanent** (it stays ephemeral and
>   erasable). So clay wins on cheapness + archival permanence; wax wins on water-resistance +
>   effortless reuse. Neither dominates. (No heat/melt penalty — beeswax is our only wax and an
>   invented "wax melts near fire" mechanic would be punishing without adding real choice.)
>
> Wherever this spec says "the tablet" for shared behavior (docId store, offhand-stylus gate,
> row-list editor, rack storage, line cap, carry-forward), it applies to **both** artifacts. The
> **deltas** unique to each are called out explicitly (clay: fire→archive + water damage; wax:
> water-immune + in-place erase, balanced by higher material cost).
>
> **Builds directly on `docs/specs/v2-notebook.md`.** The tablet reuses v2's `docId`-on-item
> store, its held-item GUI-open + server-authoritative save plumbing, and its shared row-list
> renderer. Read that spec first — everything it establishes (the `IDocumentStore` Core
> contract, the `ScribeDocumentStore` over `SaveGame`, `docId` on `ItemStack.Attributes`,
> `docId`-addressed packets, no single-editor lock for held items) is assumed here and only the
> **deltas** are specced.

## Summary

Scribe's **scratch tier** is the crudest, earliest writing family — made in the stone age
before real bookbinding exists — and ships as **two sibling artifacts, the clay tablet and the
wax tablet**, that share one document-UI, one docId store, one stylus gate, and one line cap,
but occupy opposite ends of the fragility axis.

### Clay tablet (the original v3 artifact)

The clay tablet is a **soft, unfired clay item** that opens the same task/note document UI as
the notebook and lectern, but:

- Its document is capped at **3 blocks (lines)** — deliberate scarcity that forces "is this
  still worth keeping?" and motivates the whole tier progression.
- It is created by **clayforming a flat single-layer slab** (no firing needed to *use* it —
  firing is a separate, later, one-way decision; see below).
- Writing requires a **stylus held in the offhand** (the vanilla writable-book precedent:
  `ItemBook` gates editing on a `writingTool` item in `LeftHandItemSlot`).
- It is **water-fragile**: falling in water (or being dropped in water) while soft risks data
  loss / destruction — the tier's signature drawback, and the *inverse* of the fire fragility
  planned for paper/leather tiers.
- It is **storable in the vanilla Vertical Rack** (the `scrollrack` block) via a single
  `scrollrackable: true` item attribute — no custom block work.
- **Firing it in a kiln is a genuine, irreversible trade-off:** a fired tablet becomes a
  **permanent, read-only, indestructible archive** — no longer editable, but no longer water-
  fragile either. This mirrors why real ancient tablets survive (accidental/deliberate firing).
- **Carry-forward migration:** because 3 lines is so tight, the tablet offers a one-action
  "carry forward undone tasks into a fresh tablet, clearing this one" operation (Bullet-Journal
  migration) — a pure Core document operation.

### Wax tablet (the 2026-07-21 sibling)

The wax tablet is a **beeswax writing surface poured/smoothed onto a small wooden frame** — the
classic Roman-era reusable tablet. It opens the **same** document UI, uses the **same** docId
store, the **same** offhand-stylus gate, and the **same** row-list editor as the clay tablet.
Its differences are all on the fragility/lifecycle axis, and they are the deliberate *inverse*
of clay's:

- It is **NOT water-fragile** — the explicit, required contrast. Swimming or falling in water
  while holding it, or dropping it in water, does **not** damage or destroy the document. (No
  `dissolveInWater`, no `OnHeldIdle` water-damage tick.) This is the single load-bearing wax
  behavior.
- It is instead **heat-fragile**: near lava/fire (or if the holder is on fire), the wax softens
  and the writing smooths away — modeled as damage-or-destroy analogous to clay's water damage,
  but keyed on heat/lava rather than liquid. This is what keeps wax from being strictly better
  than clay (see the asymmetric-fragility table).
- It is **erasable/reusable in place**: historically you smooth the wax flat with the blunt end
  of the stylus to reuse the tablet. Modeled as a cheap **"smooth / erase"** editor action that
  clears the document under the *same* docId — the opposite of clay's one-way carry-forward
  *migration* (which mints a new document and archives/clears the old). Wax needs no migration
  because you just wipe it and rewrite.
- It **cannot be fired / made permanent** — there is no wax analogue of the fired-clay archive
  (the same heat that would archive clay melts wax). Wax is inherently ephemeral; clay is the
  path to permanence. This is the intended asymmetry.
- Everything shared with clay — **stylus-in-offhand editing gate**, **line cap via
  `ScribeDocumentPolicy`**, **Vertical-Rack storability**, **docId-on-item persistence** — is
  identical and specced once below.

Open wax-specific decisions (line cap vs clay's 3, melt = destroy vs smudge, crafting path,
whether erase is a distinct action or reuses carry-forward-to-self) are collected under Open
Questions and in the Clarifying-questions list.

### What the tier adds over v2

Together the two artifacts make the scratch tier the **first place v2's `docId` store is
exercised from a second (and third) item type**, validating that the store is genuinely
artifact-agnostic. On top of v2 the tier adds: (1) a **soft/fired (clay) lifecycle** modeled as
a Core policy flag enforced server-side; (2) **contact damage** while held or dropped, keyed on
**water for clay** and **heat/lava for wax** (the same `OnHeldIdle`/`OnGroundIdle` seam, inverse
predicate); (3) an **offhand-tool gate** on editing (shared); and (4) an **in-place erase** op
for wax alongside clay's **carry-forward migration** — both pure Core document operations.

## VS API hooks

Confirmed by decompiling the installed `VintagestoryAPI.dll` / `VintagestoryLib.dll` /
`Mods/VSSurvivalMod.dll` (v1.22.x, game build dated 2025-05-30) and by reading shipped assets
under `/Applications/Vintage Story.app/assets/survival/`. Where a fact is confirmed is noted
inline. **Everything v2 already established (held-item GUI open, `ItemStack.Attributes`,
`ItemSlot.MarkDirty`, `SaveGame` store, `docId` packets) is inherited unchanged and not
re-listed here — see `v2-notebook.md`.**

### Making the clay tablet: clayforming a flat slab

- **Clayforming is a JSON `clayforming` recipe** resolved into `ClayFormingRecipe :
  LayeredVoxelRecipe<ClayFormingRecipe>` (confirmed: `ClayFormingRecipe` decompile — 16
  `QuantityLayers`, `RecipeCategoryCode == "clay forming"`). The recipe drives
  `BlockEntityClayForm`, which on completion clones `((LayeredVoxelRecipe)SelectedRecipe)
  .Output.ResolvedItemstack` and `SpawnItemEntity`s it (confirmed: `BlockEntityClayForm`
  decompile, lines ~237–259).
- **A flat slab is a single-layer pattern.** Confirmed against the shipped
  `assets/survival/recipes/clayforming/tiles.json`: its `pattern` is a **one-element array**
  (one layer) of a grid of `#` (clay present) / `_` (empty) rows, `name: "Tiles"`, `output: {
  type: "item", code: "claytile-raw-plain-{type}", stacksize: 4 }`. The tablet recipe copies
  this shape almost verbatim — a single filled rectangular layer — with our own `output` code.
- **Recipe JSON fields** (confirmed from `tiles.json` / `ingotmold.json`): `ingredient` (`{
  type: "item", code: "clay-*", name: "color", allowedVariants: [...] }`), `pattern` (array of
  layers; each layer an array of equal-length `#`/`_` strings), `name`, `output`. The
  `{color}`/`{type}` wildcard from the ingredient variant flows into the output code.
- **Implication:** creating a soft tablet needs **only a JSON recipe file** plus the item def —
  no C# for the crafting step. The clayformed output is our soft-tablet `Item` (see below).

### Making the wax tablet: beeswax on a wooden frame

Wax comes from bees, not clay, and is worked cold-by-hand/warm rather than clayformed. What the
vanilla assets confirm about the wax supply chain and how wax is "used up" into other items:

- **`game:beeswax` is a real vanilla item** (confirmed: shipped
  `assets/survival/itemtypes/resource/beeswax.json`, `code: "beeswax"`, `maxstacksize: 32`, no
  custom `class` — a plain item). Its production chain is confirmed by
  `assets/survival/itemtypes/resource/honeycomb.json`: honeycomb has a **`Squeezable`** behavior
  (`returnStacks: [{ code: "beeswax" }]`, `liquidItemCode: "honeyportion"`) **and**
  `juiceableProperties` (`returnStack: { code: "beeswax", stacksize: 5 }`, via a fruit press) —
  i.e. skep/beehive → honeycomb → **squeeze/press out honey, keep the beeswax**. So beeswax is a
  reliably obtainable stone-age-adjacent material (bees are early-game), which fits a scratch-
  tier artifact.
- **Vanilla precedent for "consume beeswax into a shaped item" is a grid/cooking recipe, not a
  mold.** Confirmed shipped recipes that turn `beeswax` into something: `recipes/cooking/candle.json`
  (a **firepit cooking recipe**: `3 beeswax + 1 flaxfibers → candle`, using
  `ingredients`/`cooksInto`), `recipes/grid/waxedcheese.json` (a **shapeless grid recipe**:
  `rawcheese-salted + beeswax → rawcheese-waxed`), plus bow/leather recipes. There is **no
  vanilla "pour wax into an ingot-style mold" mechanic** — wax is always just an ingredient
  consumed by a grid or cooking recipe. **So the plausible, precedent-backed wax-tablet crafting
  path is a plain grid recipe: `beeswax + a wooden frame/board → wax tablet`** (e.g. beeswax over
  a plank/`game:board-*` or a small crafted "tablet frame"), exactly the shape of `waxedcheese`
  (wax applied to a substrate). No clayforming, no firing, no new crafting mechanic — one JSON
  grid recipe, like the notebook's.
- **Why a wooden frame, not solid wax:** historically a wax tablet is a recessed wooden board
  filled with a thin wax layer — the wood gives it rigidity and is what survives; the wax is the
  erasable surface. Modeling it as `wood frame + beeswax` also gives the heat-fragility a natural
  fiction (the wax melts out of the frame; the empty frame could even be a "blank tablet" the
  player re-waxes — an optional reuse loop, open question).
- **Confirmed non-behavior:** vanilla beeswax has **no melting/heat reaction of its own** —
  `beeswax.json` defines no `combustibleProps`, no temperature behavior; it is an inert stacking
  item (the wiki confirms no heat behavior). The vanilla `candle` item *does* have
  `combustibleProps { burnTemperature: 700, burnDuration: 8 }` (it burns *as fuel*), but that is
  a candle being consumed as a light/fuel source, **not** wax "melting away." **So wax-tablet
  heat-fragility is entirely our own mechanic — there is no vanilla melt behavior to inherit for
  free** (contrast clay's water-fragility, which gets `dissolveInWater` for free). See the
  heat-fragility subsection below for the hooks.
- **Implication:** creating a wax tablet needs **only a JSON grid recipe** plus the item def and
  a wooden-frame ingredient (either a vanilla board or a tiny crafted frame item) — no C# for the
  crafting step, same as clay. The heat-melt behavior *is* custom C# (below).

### Editing gate: stylus in the offhand

- **`EntityAgent.LeftHandItemSlot`** is the offhand slot (confirmed: `EntityAgent` decompile
  line ~137; `ActiveHandItemSlot => RightHandItemSlot`, the main hand). `OnHeldInteractStart`
  receives `byEntity`, so the tablet reads `byEntity.LeftHandItemSlot` exactly as the notebook
  reads `byEntity.Controls.ShiftKey`.
- **Vanilla precedent is exact.** `ItemBook.OnHeldInteractStart` gates opening the *editable*
  dialog on `editable && isWritingTool(byEntity.LeftHandItemSlot) && !isSigned(slot)`
  (confirmed: `ItemBook` decompile line 132). `isWritingTool(slot)` returns
  `slot.Itemstack?.Collectible.Attributes.IsTrue("writingTool")` (confirmed: `ItemBook` decompile
  lines 223–235). **So "stylus in offhand" = a stylus item whose JSON `attributes` set
  `writingTool: true`, and the tablet checks `ItemBook.isWritingTool`-style logic on
  `LeftHandItemSlot`.** No new mechanism — this is the shipped writable-book pattern.
- Without a stylus in the offhand, the tablet should open **read-only** (mirrors `ItemBook`
  falling through to the read dialog when no writing tool is present). This gives the tablet a
  natural read/edit split driven by the offhand tool rather than a Shift modifier.

### Water-contact fragility (the CLAY drawback)

> Applies to the **soft clay tablet only**. The **wax tablet opts out entirely** — it sets no
> `dissolveInWater` and overrides no water-damage tick, so all of the below is simply absent for
> wax (its inverse drawback is heat, next subsection). The fired clay tablet also opts out.

- **Held-while-soft:** `CollectibleObject.OnHeldIdle(ItemSlot slot, EntityAgent byEntity)` is
  called every tick while the item is held (confirmed: `CollectibleObject` decompile line 1315,
  empty virtual). The tablet overrides it and checks the holder's liquid state:
  **`Entity.Swimming`** and **`Entity.FeetInLiquid`** are public fields on the base `Entity`
  (confirmed: `VintagestoryLib` decompile — `public bool FeetInLiquid;` / `public bool Swimming;`
  / `public bool InLava;`, and `EntityAgent` reads them, e.g. line 741 `if (FeetInLiquid)`).
  Gate on `api.Side == Server` (the server is authoritative over the document/damage).
- **Dropped-in-water:** `CollectibleObject.OnGroundIdle(EntityItem entityItem)` fires every tick
  for a dropped stack (confirmed: `CollectibleObject` decompile line 1327). **Vanilla already
  ships a `dissolveInWater` attribute here:** the base implementation, when
  `entityItem.Swimming && api.Side == Server && Attributes.IsTrue("dissolveInWater")`, rolls
  `Rand < 0.01` → `SpawnCubeParticles` + `entityItem.Die()` (destroy), else `Rand < 0.2` →
  emit a few dissolve particles (confirmed: `CollectibleObject` decompile lines 1327–1345). The
  soft tablet can **set `dissolveInWater: true`** to get dropped-in-water destruction *for
  free*, and only needs custom code for the *held-while-swimming* case and for the "orphan the
  `docId` store entry" bookkeeping when it dies.
- **Fired tablet sets neither** — it is not water-fragile (asymmetric fragility, below).

### Heat-contact fragility (the WAX drawback — the inverse of clay's)

> Applies to the **wax tablet only**. This is the mechanic that keeps wax from being strictly
> better than clay: wax shrugs off water but **melts near heat**, and (unlike fired clay) can
> never be made permanent. Everything here is **our own C#** — there is no vanilla "wax melts"
> behavior to inherit (confirmed above: beeswax has no `combustibleProps`/temperature behavior),
> so this is the mirror of clay's water path built by hand.

- **The same two idle hooks clay uses for water, keyed on heat instead.** Reuse
  `CollectibleObject.OnHeldIdle` (held, every tick — confirmed empty virtual, `CollectibleObject`
  line ~1315) and `CollectibleObject.OnGroundIdle` (dropped, every tick — confirmed line ~1327),
  server-side only, but branch on **heat/lava** predicates rather than liquid ones.
- **Confirmed heat/proximity signals on the holder entity** (all confirmed public fields on the
  base `Entity` — `VintagestoryLib` decompile of `Vintagestory.API.Common.Entities.Entity`):
  **`Entity.InLava`** (line 138, `public bool InLava;`), **`Entity.IsOnFire`** (line 321, a
  `WatchedAttributes.GetBool("onFire")`-backed property — true when the entity itself is
  burning), and the timing fields `InLavaBeginTotalMs` / `OnFireBeginTotalMs`. **So "wax near
  extreme heat" is confirmable for the two unambiguous cases: the holder is in lava, or the
  holder is on fire.** These are the direct inverse of the `Swimming`/`FeetInLiquid` reads clay
  uses for water. Gate on `api.Side == Server`, same as clay.
- **Proximity to a fire block (firepit/forge/torch) without being *in* lava or *on* fire is NOT
  a confirmed free signal — mark VERIFY.** There is no confirmed collectible callback that says
  "you are standing next to a hot block." Two candidate approaches, both needing a spike:
  - *(a) Ambient/block scan (VERIFY):* in the server-side idle tick, scan the few blocks around
    `byEntity.Pos`/`entityItem.Pos` for a lit heat source (firepit lit, forge, lava, magma) and
    treat proximity as a melt tick. Cheap enough at a low tick cadence but needs confirming which
    block/behavior exposes "is currently hot/lit."
  - *(b) Item temperature (VERIFY the source of heat):* `CollectibleObject.GetTemperature(world,
    stack)` / `HasTemperature` are **confirmed** (`CollectibleObject` lines ~3017/3059) and read
    a `"temperature"` tree attribute; `GlobalConstants.TooHotToTouchTemperature == 250` and
    `CollectibleDefaultTemperature == 20` are **confirmed** constants. Melt could trigger when
    the *stack's own temperature* rises above a wax melt point. **BUT** what confirmedly raises a
    held/dropped item's temperature is a **firepit/kiln smelting slot** (`BlockEntityFirepit`
    heats items placed *in* it), not mere proximity — a wax tablet in the hotbar near a fire is
    not confirmed to gain temperature. So temperature-based melt is clean *if* the tablet is put
    into/near a heat source that actually heats it; do not assume open-air proximity heats it.
  **Recommendation:** ship the **confirmed** cases first (`InLava` and `IsOnFire` → melt), which
  already deliver the "don't take your wax notes into a volcano / while burning" fiction, and
  treat firepit/forge *proximity* as a VERIFY follow-up once a spike confirms a heat signal.
- **What "melt" does** (mirror of clay's water-damage options; open question, pick one):
  1. **Warn only** — particles + a one-time "your wax is softening!" message, no data loss.
  2. **Progressive smudge** — on a hit, blank the last block's text via the same Core op clay's
     water-smudge uses (the writing "runs" as the wax softens). Symmetric with clay.
  3. **Destroy** — on the rare hit, delete the wax-tablet stack and orphan its `docId` (same
     orphan class as clay-drop and as v2 open-question 3). Thematically: the wax runs off the
     frame entirely.
  This spec leans **progressive smudge for `IsOnFire`, destroy for `InLava`** (lava is
  catastrophic; being on fire is a warning you can act on), exactly paralleling clay's "smudge
  when swimming, destroy when dropped in water" lean — flagged for the user.
- **No `dissolveInWater`, no water tick** on wax — it is explicitly water-immune (the required
  contrast). A wax tablet dropped in water just floats/sits like any item and keeps its document.

### Firing: soft → permanent read-only archive (CLAY only)

> Wax has **no firing path** — it cannot be made permanent (the heat that fires clay melts wax).
> This subsection is clay-only.

- **Firing transforms an item via `CombustibleProperties.SmeltedStack` with
  `SmeltingType == EnumSmeltType.Fire`** (confirmed: `EnumSmeltType.Fire` = "must be fired in a
  kiln"; `CombustibleProperties` fields `MeltingPoint`, `MeltingDuration`, `SmeltedRatio`,
  `SmeltingType`, `SmeltedStack`, `RequiresContainer`). Confirmed against shipped
  `assets/survival/itemtypes/resource/tile-clay-raw.json`, whose `combustiblePropsByType` sets
  `{ meltingPoint: 600, meltingDuration: 30, smeltedRatio: 1, smeltingType: "fire",
  requiresContainer: false, smeltedStack: { type: "item", code: "claytile-fired-..." } }`.
- **`BlockEntityPitKiln.OnFired()` is the transform site** and reveals a **critical gotcha**
  (confirmed: `BlockEntityPitKiln` decompile lines 234–255): it does
  `slot.Itemstack = combustibleProps.SmeltedStack.ResolvedItemstack.Clone()` — it **replaces the
  entire stack with a fresh clone of the recipe output**, then only copies `StackSize`. **The
  source stack's `Attributes` (our `docId`!) are NOT carried onto the fired stack.** Firing via
  the vanilla combustible path would therefore **sever the tablet from its document.** This is
  the single most important API fact for this tier — see Implementation for the two ways to
  handle it.
- Beehive-kiln firing uses a separate `beehivekiln` attribute map on the item
  (`"0".."3" → resulting stack`, confirmed in `tile-clay-raw.json`) rather than
  `CombustibleProperties`; it has the **same** "resulting stack is a fresh item, attributes not
  copied" problem. Whichever kiln path is supported, the docId-preservation issue is identical.

### Storable in the Vertical Rack

- The vanilla "vertical rack" for scrolls/tablets is the **`scrollrack`** block, backed by
  `BlockEntityScrollRack` (confirmed: decompile — `InventoryClassName == "scrollrack"`, a
  12-slot `InventoryGeneric`). It accepts an item **iff the held item's collectible attributes
  set `scrollrackable: true`**: `OnInteract` computes `flag = val3?.Attributes != null &&
  val3.Attributes["scrollrackable"].AsBool(false)` and only stores when `flag` (confirmed:
  `BlockEntityScrollRack` decompile line 142).
- Confirmed against shipped `assets/survival/itemtypes/lore/paper.json`, which sets
  `scrollrackable: true` plus an `onscrollrackTransform` (a display transform positioning the
  item on the rack) and the related `displaycaseable`/`shelvable` flags. **So rack storability =
  add `scrollrackable: true` (and an `onscrollrackTransform`) to the tablet item JSON. Zero C#,
  zero new block.** The `docId` on `ItemStack.Attributes` survives being placed in the rack (the
  rack stores the whole `ItemStack`), so a racked tablet keeps its document.
- **Applies to the wax tablet too** — add the same `scrollrackable: true` +
  `onscrollrackTransform` to the wax item JSON. Zero extra C#; the wax tablet's docId survives
  racking identically. (Racking is a shared, material-agnostic behavior.)

### Stamping / animation / sound (cross-reference only — parked)

- The roadmap parks "stamping mechanic (custom UI + animation + sound)" as a large standalone
  effort. Not specced here beyond noting the seam: a future stamping change would replace the
  plain-text row editor with a stamp interaction and would swap the render font to the
  cuneiform-style block-letter face specced in `presentation-and-fonts.md`. v3 ships **plain
  text entry** (same `ScribeRowElement` editor the notebook uses), leaving stamping as a purely
  presentational later layer with no data-model change.

## C# data structures

### Core (`src/Core/` — MUST NOT reference the VS API)

The document model is **almost** unchanged from v2 — same `ScribeDocument` / `ScribeBlock` /
`ScribeDocumentCodec`, same `IDocumentStore`. Three additions, all game-agnostic and unit-
testable with `dotnet test`:

**1. A 3-line cap as a Core policy, not a magic number in the Mod.** The scarcity is a
document-level rule, so it belongs in Core where it can be tested. Rather than hard-code `3` in
`ScribeDocument` (which is shared by every tier and must stay uncapped for the notebook), model
the cap as a small policy object the Mod applies:

```csharp
namespace Scribe.Core;

/// A per-artifact capacity/edit policy. Core holds the rule; the Mod picks which policy a given
/// artifact uses. NO VS API. Keeps "3 lines" out of the shared ScribeDocument and out of the Mod.
public sealed class ScribeDocumentPolicy
{
    /// Max blocks the document may hold. null = unbounded (notebook/lectern). 3 for the tablet.
    public int? MaxBlocks { get; init; }

    /// If true, all mutation is refused (fired tablet = read-only archive).
    public bool ReadOnly { get; init; }

    public static readonly ScribeDocumentPolicy Unbounded = new();
    public static readonly ScribeDocumentPolicy ClayTabletSoft = new() { MaxBlocks = 3 };
    public static readonly ScribeDocumentPolicy ClayTabletFired = new() { MaxBlocks = 3, ReadOnly = true };
    // Wax reuses the exact same policy shape — it is line-capped and editable, never read-only
    // (wax can't be "fired"/locked). Its cap MAY differ from clay's 3 (open question — wax
    // tablets historically held more, being erasable; shown here as 4, decide at design time).
    public static readonly ScribeDocumentPolicy WaxTablet = new() { MaxBlocks = 4 };

    /// True if adding one more block is allowed under this policy.
    public bool CanAdd(ScribeDocument doc) => !ReadOnly && (MaxBlocks is null || doc.Blocks.Count < MaxBlocks);

    /// True if any structural/text mutation is allowed at all.
    public bool CanEdit => !ReadOnly;
}
```

The policy is **advisory in Core and enforced at the mutation boundary** — the Mod's edit
handler consults it before applying a decoded document (see Implementation "Server-side
enforcement"). We deliberately do **not** bake the cap into `ScribeDocument.AddTask` itself: the
document type stays tier-agnostic, and the codec/round-trip is unaffected (a fired tablet's 3
blocks serialize exactly like any other document — no format change, `Version` stays 3).

**2. Carry-forward migration as a pure Core operation.** Given a source document, produce the
blocks that should move to a fresh tablet (undone tasks) and leave the source cleared:

```csharp
namespace Scribe.Core;

public static class ScribeMigration
{
    /// Bullet-Journal "carry forward": returns a NEW document containing the source's undone
    /// tasks (Done == false) in order, capped to maxBlocks (default: all). Text sections are
    /// dropped (scratch tier is task-first). Does not mutate the source — the caller clears it.
    public static ScribeDocument CarryForwardUndone(ScribeDocument source, int? maxBlocks = null) { /* ... */ }
}
```

This is trivially unit-testable (undone tasks carried in order, done tasks and text sections
dropped, cap respected) with no game install — exactly the kind of rule the Core/Mod split
exists to protect. "Clear the old one" is just constructing an empty `ScribeDocument` and
`store.Save`-ing it under the old `docId` (Mod side).

**2b. Wax's in-place erase is even simpler — and needs no new Core API.** Where clay's carry-
forward *mints a new document* under a new docId (one-way migration to a fresh tablet), wax is
*erased in place*: the player smooths the wax flat and the **same** tablet (same docId) now holds
an empty document. That is just `store.Save(docId, new ScribeDocument())` on the Mod side — no
migration, no new docId, no Core helper beyond constructing an empty document. So the Core
surface for the two artifacts is: **`CarryForwardUndone` (clay migration)** and **plain
empty-document save (wax erase)** — the erase reuses existing Core, deliberately. (If a future
"undo the last erase" is ever wanted it would need a Core-side history, out of scope here.)

> Deliberately **not** in Core: any notion of "fired", "clay", "wax", "water", "heat", "melt",
> "stylus", "erase", or "rack". `ReadOnly` is expressed abstractly as a policy flag; *why* a
> document is read-only (because its clay tablet was fired) is a Mod concern; *why* one is erased
> (the player smoothed the wax) is likewise Mod-side. Core only knows "this document may/may not
> be mutated" and "here are the undone tasks carried forward."

### Mod (`src/Mod/` — the VS API adapter)

**One tier-neutral class with a `material` axis, spanning clay and wax.** The three concrete
artifacts (soft clay, fired clay, wax) share nearly all Mod logic — the docId open round-trip,
the stylus gate, the row editor, rack storage — and differ only in a few branch points
(water-damage vs heat-melt vs neither; fireable vs not; erasable vs migrate-only). Rather than a
separate `ItemScribeClayTablet` and `ItemScribeWaxTablet` duplicating the open/idle plumbing,
**rename the class to the tier-neutral `ItemScribeTablet`** and drive behavior off **two variant
axes**:

- `material: ["clay", "wax"]` — picks the fragility model + capabilities.
- `state: ["soft", "fired"]` — **only meaningful for clay** (wax is always effectively "soft"
  and has no fired state; its JSON simply omits/ignores the `state` axis or fixes it to a single
  value — decide at design time whether wax is a separate item def with no `state` axis, or the
  same def with `state` constrained; a separate `waxtablet.json` def with only a `material`
  understanding is cleanest given wax's crafting path and attributes differ).

```csharp
public sealed class ItemScribeTablet : Item   // registered "ScribeTablet" (was "ScribeClayTablet")
{
    private bool IsWax   => Variant["material"] == "wax";
    private bool IsClay  => Variant["material"] == "clay";
    private bool IsFired => IsClay && Variant["state"] == "fired";   // wax is never fired
    private bool IsReadOnly => IsFired;                              // only fired clay is read-only

    // Reuses v2's DocIdAttributeKey = "scribeDocId" verbatim — same store, same key, both materials.

    private ScribeDocumentPolicy Policy =>
        IsWax   ? ScribeDocumentPolicy.WaxTablet :
        IsFired ? ScribeDocumentPolicy.ClayTabletFired :
                  ScribeDocumentPolicy.ClayTabletSoft;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, /*...*/ ref EnumHandHandling handling)
    {
        // 1. PreventDefault.
        // 2. Read/lazily-alloc docId (server), same as ItemScribeNotebook.
        // 3. Decide mode:
        //      fired clay -> always read-only view (no stylus check).
        //      soft clay / wax -> editable IFF ItemBook.isWritingTool(byEntity.LeftHandItemSlot); else read-only.
        //    Send ScribeTabletOpenMessage { DocId, HotbarSlotId, WantEditor } where
        //    WantEditor = !IsReadOnly && hasStylus.
    }

    public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
    {
        // server only. Branch on material:
        //   soft clay -> if (byEntity.Swimming || byEntity.FeetInLiquid) -> WATER-damage tick.
        //   wax       -> if (byEntity.InLava || byEntity.IsOnFire)       -> HEAT-melt tick (see Impl).
        //   fired clay-> nothing.
    }

    public override void OnGroundIdle(EntityItem entityItem)
    {
        // wax: if (entityItem.Swimming) -> DO NOTHING (water-immune, the required contrast).
        //      heat-melt-while-dropped keyed on InLava is optional (open question).
        // soft clay gets dropped-in-water destroy for FREE via dissolveInWater JSON (no override needed).
        base.OnGroundIdle(entityItem);
    }

    public override string GetHeldItemName(ItemStack stack) { /* "Clay tablet" / "Fired clay tablet (archive)" / "Wax tablet" */ }
    public override void GetHeldItemInfo(...) {
        /* task count; clay-soft: "Fire to make permanent", "Wets out in water";
           clay-fired: "Permanent archive (read-only)";
           wax: "Waterproof", "Melts near fire/lava", "Smooth to erase & reuse" */
    }
}
```

Using one class keeps the docId/open/idle logic in one place; the JSON defs differ in attributes
(clay-soft: `dissolveInWater` + `combustibleProps`; wax: neither, plus its heat behavior is code-
side; wax's editor shows a "smooth/erase" action). An `ItemClay`-style `Variant[...]` read is the
confirmed idiom (`ItemClay.OnHeldInteractStart` reads `((RegistryObject)this).Variant["type"]`).

> **Naming note for the parent:** this renames the planned `ItemScribeClayTablet` →
> `ItemScribeTablet` and the registry code `"ScribeClayTablet"` → `"ScribeTablet"`. Since no code
> exists yet (this is a design spec), it is a pure naming decision, not a migration. If the parent
> prefers to keep `ItemScribeClayTablet` and add a parallel `ItemScribeWaxTablet`, the shared
> plumbing should be factored into a common base to avoid duplication — noted as a design choice.

**Packets** — the notebook packets are **`docId`-addressed already**, so v3 either reuses them
verbatim or adds thin tablet-named twins. Reuse is preferred (the tablet *is* a docId-backed held
document); the only new field the open message needs is which mode to open:

```csharp
// Reuse ScribeNotebookOpenMessage / ...DocumentMessage / ...EditMessage from v2, OR add
// ScribeTablet* twins if v3 wants to keep the item types cleanly separated. WantEditor
// already exists on the v2 open message (added "for symmetry"); the tablet is the feature that
// finally uses it (readOnly=false + stylus present -> editor; else read-only). ONE set of
// tablet packets covers BOTH clay and wax — the material never appears in the packet; the
// server derives the policy from the stack's own variant, so wax adds NO new packet shape.
```

No new packet *shape* is required. What **is** new is server-side policy enforcement on the edit
handler (below) and a **fire-transform handler** if firing is done via a custom path.

## Implementation spec

### 1. Assets (Mod-side JSON) — most of the tier is data, not code

- `assets/scribe/itemtypes/claytablet.json` — one item, `class: "ScribeClayTablet"`,
  `maxstacksize: 1` (a docId-bearing item must never stack — same rule as the notebook),
  `variantgroups: [{ code: "state", states: ["soft", "fired"] }]`, clay-tinted texture
  (placeholder art acceptable, per lectern/notebook precedent). Then `attributesByType`:
  - `*-soft`: `dissolveInWater: true` (free dropped-in-water destruction, confirmed vanilla
    path); `combustiblePropsByType` with `smeltingType: "fire"`, a `meltingPoint`/`duration`
    (~600/30 per tile precedent), and `smeltedStack: { type: "item", code:
    "scribe:claytablet-fired" }` **only if the vanilla firing path is chosen** (see step 5 for
    the docId caveat); `scrollrackable: true` + `onscrollrackTransform`.
  - `*-fired`: `scrollrackable: true` + `onscrollrackTransform`; **no** `dissolveInWater`, **no**
    `combustibleProps`, **no** editability. Optionally a higher blast/harvest resistance if we
    want "indestructible" to be literal (open question).
  - Interaction hints in `lang/en.json` (remember the `"scribe:"` domain prefix — VSAPI-NOTES
    "Localization"): "Hold a stylus to write", "Wets out if you swim", "Fire to make permanent".
- `assets/scribe/recipes/clayforming/claytablet.json` — a **single-layer** `pattern` (copy
  `tiles.json`'s shape), `ingredient: { type: "item", code: "clay-*", name: "color",
  allowedVariants: ["blue","fire","red"] }`, `output: { type: "item", code:
  "scribe:claytablet-soft" }`. This is the whole "clayform a flat slab" mechanic.
- **The stylus.** Either (a) reuse an existing vanilla item as the writing tool by requiring
  `writingTool: true` — but no vanilla item sets it except via our own patch, so (b) ship a
  small `assets/scribe/itemtypes/stylus.json` (a bone/wood point) with `attributes: {
  writingTool: true }`, knappable or grid-crafted from bone/stick. Cheap; makes "stylus in
  offhand" a real crafted object. (Open question: craft a stylus, or accept any pointed vanilla
  tool via an attribute patch?)

### 2. Register (`ScribeModSystem`)
- `Start`: `api.RegisterItemClass("ScribeClayTablet", typeof(ItemScribeClayTablet))`. Reuse the
  v2 `docId` store + packets registered on the `"scribe"` channel; if adding tablet-named packet
  twins, append them after the notebook types (same order both sides — existing invariant).
- `StartServerSide`: the `ScribeDocumentStore` from v2 is reused as-is. Add the edit-handler
  **policy check** (below). If firing uses a custom handler, wire it here.

### 3. Open flow (mode selection is the v3 delta)
Same round-trip as the notebook (client sends open → server allocs/loads doc → replies with
bytes → client opens dialog). The **only delta** is mode selection in
`OnHeldInteractStart`/on the server reply:
- `fired` variant → open the **read-only** view (reuse the lectern/notebook read view). Never an
  editor. There is no stylus check.
- `soft` variant → editor **iff** `ItemBook.isWritingTool(byEntity.LeftHandItemSlot)` is true;
  otherwise read-only. (Mirrors `ItemBook`'s `editable && isWritingTool(...)` gate exactly.)

### 4. Edit + save flow with server-side policy enforcement (the security boundary)
- Client editor edits a scratch `ScribeDocument`, throttled autosave sends the `docId`-addressed
  edit message — identical to v2.
- **Server edit handler enforces `ScribeDocumentPolicy` before saving** (this is where "fired =
  read-only" and "3-line cap" are actually enforced — never trust the client):
  1. Resolve the tablet stack from `HotbarSlotId`, confirm it is a `ScribeClayTablet`, read its
     `state` variant → pick `ClayTabletSoft` vs `ClayTabletFired` policy.
  2. If `!policy.CanEdit` (fired) → **reject the edit** (drop it; optionally reply with a refusal
     so the client can revert + show "This tablet is fired — read only"). The document is never
     mutated server-side.
  3. Decode the incoming document; if `policy.MaxBlocks` is set and the decoded doc exceeds it →
     **reject** (a well-behaved client's editor already prevents adding a 4th block via
     `policy.CanAdd`; the server rejection guards a malicious/desynced client).
  4. Else `store.Save(docId, doc)` as v2.
- The client editor also consults the policy locally (UX): the "add task" affordance is disabled
  once `doc.Blocks.Count == 3`, and the whole editor is unavailable for a fired tablet. Local
  checks are convenience; the server check is the guarantee.

### 5. Firing: preserving the docId across the transform (the crux)
The vanilla firing paths (`BlockEntityPitKiln.OnFired` and the `beehivekiln` attribute map)
**both replace the stack with a fresh output clone and drop the source attributes** (confirmed:
`BlockEntityPitKiln` decompile — `slot.Itemstack = SmeltedStack.ResolvedItemstack.Clone()`). A
naive `combustibleProps.smeltedStack` would therefore fire a **blank** `claytablet-fired` with no
`docId` → the document is silently orphaned and the archive is empty. Two viable approaches:

- **Approach A — same item, `fired` flag on the stack, no stack replacement (preferred if
  feasible).** Instead of a distinct fired *item*, keep the *same* `claytablet` item and store
  `fired` as a **boolean on `ItemStack.Attributes`** (e.g. `scribeFired = true`). Then firing must
  set that attribute rather than swap the stack. But the vanilla kiln always swaps the stack, so
  this needs a hook: override **`CollectibleObject.OnCreatedByCrafting`** /
  a fire-completion hook, or intercept via a custom kiln interaction. This is the cleanest
  data-wise (docId never leaves the stack) but fights the vanilla combustible flow. Needs a
  spike (open question).
- **Approach B — vanilla combustible path + a fire-transform fixup that copies attributes
  (pragmatic).** Let the vanilla kiln swap `claytablet-soft` → `claytablet-fired` via
  `combustibleProps.smeltedStack`, then **copy the `docId` from the consumed stack onto the fired
  stack.** The seam: the pit kiln clones the recipe output and discards the source; to intervene
  we either (i) patch/wrap the transform, or (ii) accept that the vanilla path can't carry
  attributes and instead **fire via a Scribe-owned interaction** (right-click a soft tablet on a
  lit kiln/fire with the tablet → server reads the soft stack's `docId`, spawns a `fired` stack
  with the same `docId` copied onto its `Attributes`, `MarkDirty`). Grid-recipe transforms have a
  first-class attribute-copy primitive — **`GridRecipeIngredient.CopyAttributesFrom`** (confirmed:
  `CopyAttributesFrom` property on the recipe-ingredient base) — so if firing were ever modeled
  as a grid "recipe" (soft tablet + fuel → fired tablet) the docId copy is declarative. That is
  the simplest concrete path and avoids Harmony-patching the kiln.

**Recommendation:** default to **firing as a Scribe-owned server interaction (B-ii)** or a grid
transform with `CopyAttributesFrom` (B via grid) so the `docId` is provably carried, and treat
the "real kiln combustible path" as a later polish once the attribute-carry story is solved.
Whichever path, the server, on completing a firing, **flips the document's stored policy to
read-only from that point on** — but note the policy is derived from the *item variant/attribute*
at edit time (step 4), so simply producing a `fired` stack is sufficient; there is no separate
"lock the document" write. The document bytes are unchanged; only the artifact's editability
changes. (This is why `ReadOnly` lives on the *policy the Mod selects per stack*, not inside the
serialized document.)

### 6. Water-contact damage (soft only, server-authoritative)
- **Dropped in water:** set `dissolveInWater: true` on the soft variant → vanilla `OnGroundIdle`
  destroys the `EntityItem` with particles (confirmed path). **Caveat:** when the entity dies the
  `docId` store entry is orphaned (same orphan class as v2 open-question 3 — accept the leak).
- **Held while swimming/submerged:** override `OnHeldIdle`; server-side, if `byEntity.Swimming ||
  byEntity.FeetInLiquid`, run a low-probability-per-tick damage roll (mirror the vanilla
  `Rand < 0.01` destroy / `Rand < 0.2` warn cadence for consistency). "Damage" options, in
  increasing severity (open question — pick one):
  1. **Warn only** — emit dissolve particles + a one-time "your tablet is getting wet!" message,
     no data loss (gentlest; drawback is purely tension/immersion).
  2. **Progressive smudge** — on a hit, mutate the document (e.g. blank the last block's text via
     a Core op) so prolonged submersion erodes content line by line.
  3. **Destroy** — on the rare hit, delete the tablet stack and orphan its `docId` (harshest;
     matches dropped-in-water). 
  The 3-line scarcity makes even option 2 painful, which is thematically the point. This spec
  leans **option 2 (progressive smudge) held, option 3 (destroy) dropped** — dropping it in water
  is a bigger mistake than swimming with it — but flags it for the user.

### 7. Vertical Rack storage
- Add `scrollrackable: true` + `onscrollrackTransform` to **both** variants' attributes
  (confirmed sufficient via `paper.json` + `BlockEntityScrollRack` decompile). No C#. A racked
  tablet keeps its `docId` (the rack stores the whole `ItemStack`). Interacting with a tablet
  *while it sits in the rack* is out of scope — take it out to read/write (matches vanilla
  scroll/paper behavior).

### 8. Carry-forward migration (UI action + Core op)
- Add a "Carry forward to new tablet" action in the soft-tablet editor's options bar (only shown
  when the doc has ≥1 undone task and the player holds a fresh/clayformed blank tablet, or the
  action itself produces the blank — open question on ergonomics). On invoke:
  1. `var carried = ScribeMigration.CarryForwardUndone(current, maxBlocks: 3)` (Core).
  2. Server saves `carried` under the **new** tablet's `docId` (allocating one if the target is
     blank) and saves an **empty** document under the **old** `docId` (clearing it).
  3. `MarkDirty` both slots. This is the Bullet-Journal migration, bounded to 3 lines by the
     policy.
- Simplest first cut: the action requires the player to be holding the *destination* blank
  tablet in the offhand (reusing the offhand-slot read we already do for the stylus) — but that
  collides with the stylus requirement, so more likely a two-step flow. Ergonomics are an open
  question; the Core op is the load-bearing, testable part and is unambiguous.

## Dependencies & sequencing

**Hard prerequisite: v2 (Notebook).** v3 reuses v2's `docId` store (`IDocumentStore` +
`ScribeDocumentStore` over `SaveGame`), the held-item GUI-open + server-authoritative save
plumbing, the `docId`-addressed packets, and the shared row-list renderer. v3 **must not start
until v2 lands** (which itself waits on the row-list-rework S2 — see `v2-notebook.md`
Dependencies). v3 adds no new GUI-rendering prerequisites; the read/edit views it needs already
exist by the time v2 ships.

**Sequencing within v3:**
1. Core: `ScribeDocumentPolicy` + `ScribeMigration.CarryForwardUndone` (+ xUnit tests). Pure
   Core; can be done in parallel with v2, before any Mod work.
2. Mod: `ItemScribeClayTablet` (soft/fired variants) + clayforming recipe + stylus item +
   assets/lang. Wire open-flow mode selection (stylus/fired gate) reusing v2 packets.
3. Mod: server-side policy enforcement in the edit handler (3-line cap + fired read-only).
4. Mod: water-contact damage (`dissolveInWater` for dropped + `OnHeldIdle` for held).
5. Mod: firing → fired transform that **carries the docId** (spike Approach A vs B first — this
   is the riskiest piece; do a throwaway test that fires a written soft tablet and asserts the
   fired stack resolves to the same document before committing to a path).
6. Mod: carry-forward migration action in the editor.
7. Playtest: clayform a tablet, write with/without stylus, hit the 3-line cap, swim with it, drop
   it in water, fire it and confirm it's read-only + keeps text + survives water, rack it, carry
   forward.

**Position in the staged plan:** v3 is the **scratch tier** — narratively the *earliest* tool but
architecturally *after* v2, because it depends on the notebook's `docId` infrastructure. It
validates that the store is artifact-agnostic (a second item type sharing the same store),
introduces the **soft/fired lifecycle** and **water fragility** patterns that later tiers invert
(paper/leather: fire-fragile, water-resistant), and its `ScribeDocumentPolicy` (`ReadOnly`,
`MaxBlocks`) is reusable by any future capacity-limited or archived artifact.

## Open questions

1. **Does "wets out" destroy the document or just block/erode it?** This spec leans: dropped-in-
   water = destroy (free vanilla `dissolveInWater`); held-while-swimming = progressive text
   smudge (erode), not instant destroy. Confirm the intended harshness — full destroy on both is
   simpler and more punishing; warn-only is gentlest.
2. **Is the fired archive truly indestructible, or just very durable/permanent?** "Read-only +
   not water-fragile" is the load-bearing part and is fully specced. Literal indestructibility
   (immune to explosions/decay/despawn, can't be broken or burned again) is an extra JSON
   resistance tweak — worth it, or is "permanent & read-only" enough? Note truly indestructible
   makes orphaned `docId` store entries permanent too.
3. **Should stamping be a separate later change?** This spec assumes **yes** — v3 ships plain
   text entry and defers the stamping UI/animation/sound + cuneiform font to a parked change
   (cross-referenced to `presentation-and-fonts.md`). Confirm stamping is not in v3 scope.
4. **Firing mechanism: which path (Approach A vs B) carries the `docId`?** The vanilla kiln
   drops stack attributes on transform. This spec recommends a Scribe-owned firing interaction or
   a grid transform using `CopyAttributesFrom`, and flags the pit-kiln combustible path as
   attribute-losing. A short spike is needed to pick A (fired-flag-on-stack, no swap) vs B
   (swap + copy docId). Which fits the "fire it in a kiln" fiction best while provably keeping
   the document?
5. **Stylus: a new crafted item, or reuse a vanilla tool?** No vanilla item sets `writingTool:
   true` today. Ship a dedicated stylus (bone/wood, cheap knap/craft) — or patch an existing
   pointed vanilla item's attributes to accept it? A dedicated stylus is more immersive but adds
   an item + recipe.
6. **Carry-forward ergonomics.** The Core op is settled; the *interaction* (does the action mint
   a fresh blank tablet automatically, or require the player to supply/hold a blank one? where
   does the old cleared tablet go?) needs a UX decision, ideally at playtest.
7. **One item + `fired` attribute, or two variants/items?** This spec uses one item def with a
   `state: [soft, fired]` variant group. If firing ends up modeled as a stack-attribute flag
   (Approach A), a single non-variant item with `scribeFired` on `Attributes` may be cleaner.
   Resolve alongside question 4.
