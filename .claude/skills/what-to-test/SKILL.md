---
name: what-to-test
description: Surface a short, concrete list of in-game conditions to test in Vintage Story, pulled from the remaining manual-test tasks in any in-progress OpenSpec change; persist/regenerate that list as TESTING.md at the repo root so it survives across sessions, with agent-recorded verdicts (not raw checkboxes) as the only source of confirmation; and use freshly captured screenshots as evidence against it. Use when the user asks "what should I test", "what should I check in-game", "give me a testing checklist", "what's left to verify", "update the testing checklist", or similar -- and also when the user mentions a "screenshot"/"screen"/"pic" while discussing testing, a bug, or a feature, since that means a new capture from the game's own screenshot folder needs triaging and checking against the current test checklist.
version: "1.0"
---

# What should I test?

Answers "what should I test" by turning OpenSpec's own remaining manual-test tasks into
a short, concrete checklist of things to actually go do in-game — not a dump of raw task
text, and not a guess if OpenSpec has nothing queued.

**Store selection:** If the user names a store (a store is a standalone OpenSpec repo
registered on this machine) or the work lives in one, pass `--store <id>` on `list` and
`status`. Without a store, these act on the nearest local `openspec/` root.

## Steps

1. **Find in-progress changes.**
   ```bash
   openspec list --json
   ```
   Collect entries with `status: "in-progress"` (i.e. `completedTasks < totalTasks`).

   - If the user named a specific change, use only that one (skip the filter above).
   - If the current conversation has clearly been focused on one particular change,
     prioritize that one — lead with its items — but still check the others; don't
     silently drop unrelated in-progress work the user might also want to hear about.
   - If there are none, skip straight to the last step below (nothing to pull from OpenSpec).

2. **Resolve each candidate change's `tasks.md`.**
   ```bash
   openspec status --change "<name>" --json
   ```
   Read `artifactPaths.tasks.existingOutputPaths[0]`. If the `tasks` artifact doesn't
   exist yet (change is still blocked earlier in planning), skip that change — there's
   nothing to test yet, not an error.

3. **Read each resolved `tasks.md` and extract unchecked items (`- [ ]`)**, including
   their full continuation text (a task's description often wraps onto indented
   following lines — read the whole block, not just the first line).

4. **Filter to items that describe something to verify in-game.** Keep a task if it:
   - Explicitly says "manually test", "in-game", "playtest", or similar, OR
   - Is a decision that can only be made by looking at the live result (e.g. "decide
     based on how it actually looks in-game").

   Drop tasks that are pure code/investigation/test-suite work with no in-game
   verification step (e.g. "add Core.Tests coverage for X", "investigate Y in source") —
   those aren't things to go test in the game client, they're implementation work still
   pending. If literally every remaining task in a change is like this, that change
   contributes nothing to the list (not an error — just has no in-game-testable items
   right now).

5. **Distill each kept task into ONE crisp, concrete, actionable line** — imperative,
   specific steps plus what outcome to check. Do not paste the raw multi-sentence task
   prose verbatim; compress it to what someone would actually do with the mouse/keyboard
   in-game and what they're looking for. Tag each line with its source
   `(change-name task-id)` so the user can cross-reference `tasks.md` directly.

   **Lead every line with a bolded, up-to-four-word summary** (`**Test slider sync.**`,
   `**Check hover focus.**`) before the fuller description — a quick "what to actually
   do" flag for scanning a long list fast. Keep it imperative and concrete (name the
   thing being poked, not "verify that X works correctly"). The playtest checklist app
   renders this lead-in bolded; without it an item still works, just reads slower.

   Example transformation:
   - Raw task: "Manually test in-game: confirm hover-icon show/hide does not reset focus
     or caret position while typing (the exact regression this render-time approach is
     meant to avoid — verify it actually holds, don't just assume the mechanism works)."
   - Distilled: "**Check hover focus.** Start typing in a note's text area, then move
     the mouse over a different row's delete/pin icon and back — confirm your caret
     position and in-progress text are undisturbed. *(skeuomorphic-lectern-gui 6.6)*"

6. **Dedupe across changes.** An older change's pending task can be superseded by a
   newer change already covering the same ground (e.g. an old "no scrollbar exists yet"
   task vs. a newer change's own scroll-testing task). When two kept items clearly test
   the same underlying thing, keep only the one from the more recently modified change
   and drop the other — don't make the user verify the same behavior twice under two
   different labels.

7. **Cap the list at roughly 5-8 items.** If more candidates exist than that, prioritize
   in this order, and say how many were left out (offer to list the rest if asked):
   1. Tasks already suspected broken or blocking a retest (e.g. the task directly tied
      to a bug fix that's awaiting live confirmation).
   2. Lower task-group number first (earlier, more foundational work).
   3. Tasks whose change the conversation has been actively focused on.

8. **Present the list** as a compact bulleted checklist. If items came from more than
   one change, group by change name with a small header per group. Keep the framing
   terse — this is a checklist to act on, not a report to read.

9. **If no in-progress OpenSpec change has any in-game-testable item** (no in-progress
   changes at all, all blocked on earlier planning, or every remaining task is
   non-manual work), say so plainly — don't fabricate a list to fill the gap — and ask
   the user directly what they'd like to test or work on instead. Keep this open-ended
   (plain question, not a forced multiple-choice) since at that point there's no
   OpenSpec signal to build options from.

## Persisting the checklist (`TESTING.md`)

The in-chat list above doesn't survive a session boundary — the user can't scroll back
through a future session to find it, and there's no way to mark an item done without
touching `tasks.md` (which is OpenSpec's file, not a scratch checklist). `TESTING.md` at
the repo root solves both: a standalone, git-tracked, regenerable file that mirrors the
in-game-testable items this skill surfaces, with real checkable boxes.

**Core rule: the checkbox glyph is never the source of truth. Only an agent-written
verdict annotation immediately under an item counts as "confirmed."** This is
deliberate — `TESTING.md` regenerates from `tasks.md` every time this skill runs, and a
user (or a stale prior version of the file) could have hand-toggled a box without
anything having actually been tested. Regeneration must not trust a bare `[x]` with no
matching annotation; it re-derives checked/unchecked purely from whether a real verdict
is on file.

### File format

```markdown
# Testing checklist

Regenerated by the `what-to-test` skill from OpenSpec's remaining tasks. Checkboxes are
NOT authoritative on their own -- only re-check an item if you've re-verified it
yourself; the agent-written verdict line under a checked item is what actually confirms
it. Hand-checking a box with no verdict line will be reverted next regeneration.

## skeuomorphic-lectern-gui

- [ ] `7d808ca9` **Overflow and scroll.** Add enough rows to overflow the visible dialog
      height; confirm every row is reachable by scrolling, in both read and editor view.
      *(3.5)*
- [x] `805e78a7` **Check hover focus.** Start typing in a note's text area, then hover a
      different row's delete/pin icon and back -- confirm caret/typing is undisturbed.
      *(6.6)*
      - **Confirmed 2026-07-19** via screenshots/debug/2026-07-19_.._focus-check.png:
        caret position held after hovering the delete icon on an adjacent row.

## add-lectern-block

- [ ] `c127b9ad` **Test multiplayer sync.** Two clients, one lectern each -- confirm edits are
      session-independent and visible live in read view. *(7.5)*
```

Each item's leading code (`` `7d808ca9` ``) is a short fingerprint: `sha256(task-id +
" " + normalized task text)[:8]`, computed the same way each regeneration. It's not
meant to be human-meaningful — it exists purely so regeneration can detect whether a
`tasks.md` item changed underneath a `TESTING.md` entry.

### Generating / regenerating `TESTING.md`

1. Run this skill's normal steps (1-7 above) to produce the current in-game-testable
   item list, computing each item's fingerprint as described.
2. If `TESTING.md` already exists, read it first and, for every checked (`[x]`) item
   with a fingerprint that still matches the freshly computed one AND has a verdict
   annotation under it, carry both the checked state and the annotation forward
   unchanged.
3. For every other case — item didn't exist before, fingerprint changed (the underlying
   task text changed), or it was checked with no annotation underneath (nothing actually
   confirms it) — write it unchecked, with no annotation. Never invent or infer an
   annotation; if there isn't one on file, the item resets to unconfirmed.
4. Write the full file back (grouped by change, same order/priority as the in-chat
   list), and mention to the user that `TESTING.md` was written/updated.

### Recording a confirmation

When the user reports back a test result (in this session or a future one — this is
exactly the scrollback problem this file solves), and you have first-hand evidence for
it (you watched them describe the live behavior, or you read a screenshot per the
section below):

1. Check the box for that item in `TESTING.md`.
2. Immediately under it, add a verdict line in your own words: `**Confirmed
   <YYYY-MM-DD>** <how, and what was actually observed>` — or, if it's a fail, leave the
   box unchecked and instead note the failure inline (`**Still broken <date>:** <what's
   wrong>`) so the next read of the file shows current status without re-asking.
3. Only write this annotation from your own observation in the current turn — never
   because the user told you to just check a box, and never by copying an annotation
   forward from a *different* fingerprint than the one currently on file (that would be
   confirming stale text, not the current task).
4. If this is the item's last remaining confirmation and the user wants it reflected
   upstream, offer to help them check off the corresponding `tasks.md` box too (via the
   normal `/opsx:apply` flow) — `TESTING.md` and `tasks.md` are two different files with
   two different authorities; confirming one doesn't silently edit the other.

## Screenshots as test evidence

When the user mentions "screenshot(s)", "screen(s)", or "pic(s)" in this context, they
mean images the game itself just auto-captured (fn+F12) into
`~/Pictures/Vintagestory/`, timestamp-named — not a general image reference. Treat that
mention as a trigger to pull the evidence in and use it, not just acknowledge it:

1. **Triage first.** Use the `triage-screenshot` skill (same repo,
   `.claude/skills/triage-screenshot/`) to move the new file(s) out of the game's flat
   source folder into this project's own `screenshots/debug/` (or `screenshots/progress/`
   for a clearly progress/demo shot) with a context-slugged filename. Don't duplicate
   that skill's move/rename logic here — invoke it and use its result.

2. **Read the triaged image(s)** and connect them back to whichever checklist item (from
   this skill's own output, or `TESTING.md` if it exists) they're evidence for. Match by
   what the screenshot actually shows, not by assuming it matches whatever was mentioned
   last — e.g. if six items were just handed to the user and the screenshot shows a
   title-bar overlap, that's evidence for a rendering/layout item, not for an unrelated
   persistence check.

3. **Report a verdict against that item**, not just a description of the picture:
   confirms it passes, confirms it's still broken (say what's visibly wrong and where),
   or is inconclusive (say what additional angle/state would resolve it). If the shot
   reveals a NEW problem unrelated to any current checklist item, say so plainly rather
   than forcing it to fit one of the existing items.

4. **If several screenshots arrive together** (the user often captures more than one in
   a row while testing), triage all of them, but read them as a sequence when they
   plausibly document one interaction (e.g. before/after a scroll) rather than grading
   each in isolation.

5. **If `TESTING.md` exists**, record the verdict there per "Recording a confirmation"
   above — a screenshot that clearly shows correct behavior is exactly the kind of
   first-hand evidence that annotation is for; cite the triaged screenshot's path in the
   verdict line. A screenshot showing a failure gets the matching "Still broken" note
   instead, box left unchecked.

## Notes

- This skill surfaces the list and (optionally) persists it to `TESTING.md`. It never
  checks a `tasks.md` box itself — that still happens through the normal apply/
  update-change flow, and only if the user explicitly wants the confirmation carried
  upstream (see "Recording a confirmation," step 4).
- `TESTING.md` and `tasks.md` have different authorities: `tasks.md` is OpenSpec's own
  planning artifact (edited via `/opsx:apply`/`/opsx:update`); `TESTING.md` is this
  skill's own regenerable, git-tracked scratch checklist. Don't conflate them, and don't
  let the user's request to "check something off" default to editing `tasks.md` —
  clarify which file they mean if it's ambiguous.
- Don't invent test conditions that aren't grounded in an actual pending task unless the
  user has explicitly said OpenSpec has nothing relevant and asked for ideas anyway —
  the whole point is pulling from the real remaining work, not guessing.
- Never write a checked box in `TESTING.md` without a verdict annotation directly under
  it, and never write a verdict annotation you didn't personally just derive from actual
  evidence (a described live result, a read screenshot) in the current turn. This is the
  whole point of the file — a box with no backing annotation is worthless as a "did
  someone actually check this" record.
