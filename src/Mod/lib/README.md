# Vendored third-party mod DLLs

This folder is gitignored — these DLLs must be re-extracted on any machine that builds
this project, following the steps below. See
`openspec/changes/add-imgui-configlib-tuning/design.md` for why these are vendored here
instead of referenced via NuGet (neither VSImGui nor ConfigLib has a usable published
package at the version actually installed and tested against).

## Source and versions

Extracted from the game mod `.zip`s under the local Vintage Story Mods folder
(`~/Library/Application Support/VintagestoryData/Mods/` on macOS):

- `vsimgui_1.2.7.zip` → `VSImGui.dll`, `VSImGui_DebugTools.dll`, `ImGui.NET.dll`
- `configlib_1.12.0.zip` → `configlib.dll`

## Re-extraction steps

```bash
cd /tmp
MODS="$HOME/Library/Application Support/VintagestoryData/Mods"

unzip -o "$MODS/vsimgui_1.2.7.zip" -d vsimgui_extract \
  VSImGui.dll VSImGui_DebugTools.dll ImGui.NET.dll
unzip -o "$MODS/configlib_1.12.0.zip" -d configlib_extract configlib.dll

cp vsimgui_extract/{VSImGui.dll,VSImGui_DebugTools.dll,ImGui.NET.dll} src/Mod/lib/
cp configlib_extract/configlib.dll src/Mod/lib/
```

If you install a newer version of either mod, re-extract and update the version numbers
above (and re-verify any API assumptions in `design.md`/`GuiDialogScribeLectern.cs` still
hold — these are not stable published packages with change logs to check against).

## Upstream references

Check these before decompiling the vendored DLLs — decompiling is the last-resort
fallback (per the project's modding-references guardrail), not the first stop. The mod
`.zip`s ship no published NuGet package or docs, so these upstreams are the only
authoritative source short of the DLLs themselves.

**ConfigLib** (the permanent optional soft dependency)
- Mod portal (description + examples): https://mods.vintagestory.at/configlib#tab-description
- Source: https://github.com/maltiez2/vsmod_configlib
- Wiki (manifest schema / `configlib-patches.json` usage): https://github.com/maltiez2/vsmod_configlib/wiki
  — has sub-pages worth following, not just the landing page.

**VSImGui** (the Debug-only live-tuning overlay)
- Mod portal (helpful description + examples): https://mods.vintagestory.at/imgui
- VSImGui wraps **ImGui.NET**; its source: https://github.com/ImGuiNET/ImGui.NET
- ImGui.NET wiki: https://github.com/ImGuiNET/ImGui.NET/wiki
  — has sub-pages worth following, not just the landing page.
- ImGui manual (C++ — the .NET methods share names and take similar args):
  https://pthom.github.io/imgui_manual_online/manual/imgui_manual.html
