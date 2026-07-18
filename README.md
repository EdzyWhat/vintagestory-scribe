# Scribe

A note-taking and light task-management mod for [Vintage Story](https://www.vintagestory.at/).

Scribe helps you remember your goals across Vintage Story's long, branching progression.
Tasks are the priority — dead-easy to view and edit — with immersive notekeeping as a
secondary payoff. Note-keeping tools progress with your tech tree, from a crude clay
tablet in the stone age up to shared bulletin boards, grounded in both the real
archaeology of writing and vanilla game mechanics.

> **Status:** early development. See [`ROADMAP.md`](./ROADMAP.md) for the staged plan.

## Requirements

- Vintage Story **1.22.x** (targets .NET 10)

## Building from source

This is a C# code mod. To build it you need:

- The **.NET 10 SDK**
- A **Vintage Story installation**, with the `VINTAGE_STORY` environment variable
  pointing at it (e.g. on macOS: `export VINTAGE_STORY="/Applications/Vintage Story.app"`).
  The mod references `VintagestoryAPI.dll` from there.

```sh
dotnet test    # runs the game-agnostic Core unit tests (no game install needed)
dotnet build   # compiles the full mod against VintagestoryAPI.dll
```

### Project layout

- `src/Core/` — game-agnostic library (models, rules, serialization). No game references,
  so it is unit-testable anywhere with `dotnet test`.
- `src/Mod/` — the Vintage Story code mod; a thin adapter mapping the game API onto `Core`.
- `tests/Core.Tests/` — xUnit tests over `Core`.

> **CI note:** GitHub's cloud runners have no Vintage Story install, so continuous
> integration builds and tests `Core` only (the game DLL is not redistributable).
> The full mod is compiled locally.

## Development

This project uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for spec-driven
development — each feature is proposed as a spec before it is implemented. See the
`openspec/` directory.

## License

[MIT](./LICENSE)
