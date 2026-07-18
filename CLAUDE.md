# Scribe

Note-taking & task-management mod for Vintage Story 1.22.x (targets .NET 10). Full
project context lives in `openspec/config.yaml` and `README.md` — read those for
architecture, not this file.

## Guardrails

- **Spec-driven**: for any non-trivial change, use the `openspec-propose` skill first
  rather than implementing directly. Don't skip straight to code for anything beyond a
  typo/small fix.
- **`src/Core/` must never reference the Vintage Story API.** This is the load-bearing
  invariant that keeps `Core` unit-testable without a game install — don't add a
  `VintagestoryAPI` reference or `using Vintagestory.*` there to solve a problem faster.
- **No new mod dependencies.** Vanilla `VintagestoryAPI` only; `ConfigLib` is the sole
  planned exception, and only as an optional soft dependency gated by `IsModEnabled`. Ask
  before adding any NuGet/mod package.
- **Don't touch `.github/workflows/*.yml` or `Directory.Build.props`** without asking —
  CI only builds/tests `Core` (no game DLL on cloud runners); changes here can silently
  break release automation.
- **Persistence/sync follows the vanilla Sign block pattern** (`ToTreeAttributes` /
  `FromTreeAttributes`, `SendBlockEntityPacket`, `MarkDirty`, server-authoritative). Match
  this pattern for new synced state rather than inventing a new one.
- Secondary goal is the author building dev skills — favor clear, conventional,
  well-explained solutions over clever/terse ones.

## Modding references

Before inventing a pattern from scratch or reverse-engineering `VintagestoryAPI.dll`
(decompiling is a fallback, not a first resort — it was only needed once, for
`GuiDialogBlockEntity`'s undocumented dedup/auto-close behavior), check these first:

- **The wiki**: https://wiki.vintagestory.at/Category:Modding — start at `Modding:GUIs` for
  dialog/composer questions. Page-specific lookups (e.g.
  https://wiki.vintagestory.at/Ink_and_quill) are useful for finding vanilla precedent for a
  specific interaction (e.g. writable-book save/open patterns).
- **Real shipped-mod source**, more authoritative than the wiki when the wiki is thin:
  `anegostudios/vsapi`, `anegostudios/vssurvivalmod`, `anegostudios/vsessentialsmod` on GitHub.
  E.g. `BlockEntity/BESign.cs` (text-editing dialog + `Controls.ShiftKey` right-click modifier),
  `Gui/GuiDialogBlockEntityText.cs`, `Gui/GuiDialogTrader.cs` (`AddCellList`/`IGuiElementCell`
  usage — note cell lists are mouse-only and can't host a live `GuiElementTextInput`).
