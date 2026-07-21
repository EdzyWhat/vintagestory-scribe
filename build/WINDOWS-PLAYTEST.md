# Playtesting on the Windows PC (GitHub Desktop workflow)

The loop for pulling the latest code and getting the mod into Vintage Story on Windows, so
you can test in-game (including the VSImGui debug sliders, which only render on Windows —
see [`VSAPI-NOTES.md`](../VSAPI-NOTES.md) "VSImGui debug overlay").

This assumes you use **GitHub Desktop** for the git side and shut the PC down each night, so
every morning starts fresh. The good news: the one-time setup below is stored in your
Windows user profile and **survives reboots** — you only do it once, not every day.

## One-time setup (do this once; it persists across shutdowns)

1. **.NET 10 SDK** — the mod targets `net10.0`. Open PowerShell and run `dotnet --version`;
   if it's not 10.x, install the .NET 10 SDK from https://dotnet.microsoft.com/download.

2. **Vintage Story installed.** The default Windows install path is `%APPDATA%\Vintagestory`
   (i.e. `C:\Users\<you>\AppData\Roaming\Vintagestory`). The game's *data* folder (worlds,
   mods, logs) is the sibling `%APPDATA%\VintagestoryData`.

3. **The repo cloned via GitHub Desktop.** In GitHub Desktop: **File → Clone repository →**
   pick `vintagestory-scribe`. Note the local path it clones to — you won't need to type it
   (GitHub Desktop remembers it), but it's handy to know.

4. **Point the build at the game install** via the `VINTAGE_STORY` environment variable.
   Without it, the build falls back to the macOS path and fails on Windows. Set it
   **permanently** (User scope, so it survives every shutdown):

   ```powershell
   [Environment]::SetEnvironmentVariable('VINTAGE_STORY', "$env:APPDATA\Vintagestory", 'User')
   ```

   Then **close and reopen PowerShell** and confirm it:

   ```powershell
   $env:VINTAGE_STORY        # should print C:\Users\<you>\AppData\Roaming\Vintagestory
   ```

   A blank line means it isn't set for this shell — reopen PowerShell (env vars are read at
   launch), and if it's still blank, redo the command above.

   > Installed Vintage Story somewhere non-default? Set `VINTAGE_STORY` to that folder
   > instead — it must be the directory that contains `VintagestoryAPI.dll`.

## Every day you want to test the newest code

### 1. Pull the latest code (GitHub Desktop)

1. Open **GitHub Desktop**.
2. **Current Repository** dropdown (top-left) → select **vintagestory-scribe**.
3. **Current Branch** (top-middle) → make sure it's **main**.
4. Click **Fetch origin** (top-right). If new commits exist, the button becomes
   **Pull origin** — click it to bring them down.

### 2. Build and stage the mod (PowerShell, one script)

GitHub Desktop only does the git side — it doesn't build the mod or copy it into the game
folder. Let GitHub Desktop open a shell already sitting in the repo folder, so you never
have to `cd` anywhere:

- In GitHub Desktop: **Repository → Open in Command Prompt** (the menu may say
  "Open in PowerShell" / "Open in Terminal" depending on your setup).

Then run the restage script:

- **If it opened PowerShell:**

  ```powershell
  .\build\restage.ps1 -Configuration Debug
  ```

- **If it opened Command Prompt (cmd):**

  ```cmd
  powershell -ExecutionPolicy Bypass -File .\build\restage.ps1 -Configuration Debug
  ```

Use **`-Configuration Debug`** for our GUI work — that's the build with the VSImGui
live-tuning sliders (`RegisterDebugSliders`). They're `#if DEBUG`-gated *and* the VSImGui
reference itself is Debug-only, so a Release stage has zero ImGui presence. Use
`-Configuration Release` (the default if you omit the flag) only for a final player-like
sanity pass.

If PowerShell blocks the script on execution policy, unblock it **for this session only**,
then re-run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

The script rebuilds `src/Mod`, then copies the DLLs + `modinfo.json` + `assets/` into
`%APPDATA%\VintagestoryData\Mods\scribe`. On success it prints `Staged: N files`.

### 3. Fully quit and relaunch Vintage Story

Assets and lang files load once at boot, **not** per-world-join — so leaving the game
running (or just reloading a world) will not pick up the new build. Quit all the way to
desktop and start it again.

### 4. Test

Work through [`TESTING.md`](../TESTING.md) at the repo root — that's the list of concrete
in-game conditions to verify, with agent-recorded verdicts as the source of truth.

## Sanity checks if something looks off

- **"My change isn't showing."** 99% of the time you didn't fully quit/relaunch the client
  (step 3). Otherwise confirm the staged files are fresh:

  ```powershell
  Get-ChildItem "$env:APPDATA\VintagestoryData\Mods\scribe" -Recurse
  ```

- **"Fetch origin found nothing"** but you expected changes → the commits haven't been
  pushed to GitHub yet from the Mac side. Nothing to pull until they are.

- **Build fails with a missing `VintagestoryAPI.dll` / path error** → `VINTAGE_STORY` is
  unset or wrong for this shell. Re-check `$env:VINTAGE_STORY` (one-time setup step 4). Since
  it's User-scoped it should survive reboots, but a brand-new shell must still be opened
  *after* it was first set.

- **The VSImGui slider overlay never appears** → confirm you staged a **Debug** build, then
  use VSImGui's toggle hotkey in-game. (The overlay genuinely can't render on the Mac —
  that's the whole reason this workflow is on Windows.)

## What the script actually does (reference)

`build/restage.ps1` is the Windows port of `build/restage.sh`. In order it:

1. Reads `modid` from `src/Mod/modinfo.json` (defaults to `scribe`).
2. Runs `dotnet build src/Mod/Mod.csproj --configuration <Debug|Release>`.
3. Copies `modinfo.json` and `src/Mod/bin/<Config>/net10.0/*.dll` into
   `%APPDATA%\VintagestoryData\Mods\<modid>`.
4. **Wipes** that folder's `assets\` and recopies `src/Mod/assets\` fresh (copying on top of
   an existing folder can leave a stale nested `assets\assets\` tree).
5. Prints the staged file count and the "fully quit and relaunch" reminder.

For building a distributable `.zip` (not local playtesting), that's a different script
(`build/package.sh`), not this one.
