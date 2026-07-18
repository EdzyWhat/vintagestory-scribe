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
- `tests/Integration.Tests/` — [Atlas](https://github.com/Pixnop/Atlas) integration tests
  that boot a real headless server and exercise the mod's server-side behavior (see below).

> **CI note:** GitHub's cloud runners have no Vintage Story install, so continuous
> integration builds and tests `Core` only (the game DLL is not redistributable).
> The full mod and the Atlas suite are only run locally.

### Running the Atlas suite

[Atlas](https://github.com/Pixnop/Atlas) boots a real headless Vintage Story server inside
`dotnet test`, so `tests/Integration.Tests` can exercise persistence, the network edit
round-trip, and the single-editor lock against the actual engine — not mocks. It needs the
same `VINTAGE_STORY` environment variable as building the mod, plus the .NET 10 SDK.

```sh
dotnet test tests/Integration.Tests --filter "FullyQualifiedName!~FixtureBuilders"
```

`FixtureBuilders` is excluded from normal runs: it's a one-time world-builder scenario, not
a pass/fail test. `PersistenceScenarios` boots against a prebuilt world save
(`tests/Integration.Tests/fixtures/lectern.vcdbs`, already checked in) rather than seeding
its own state, because Atlas's `RestartWorld` isolation mode genuinely restarts the server
before a scenario runs — seeding from an earlier scenario method in the same test run would
depend on xUnit's execution order, which is not guaranteed. If `FixtureBuilders` changes (a
new lectern fixture is needed), regenerate the save with:

```sh
dotnet build tests/Integration.Tests
atlas fixture tests/Integration.Tests/bin/Debug/net10.0/Integration.Tests.dll \
    --scenario BuildsLecternWithDocumentFixture \
    --out tests/Integration.Tests/fixtures/lectern.vcdbs
```

(`atlas` is the Atlas CLI: `dotnet tool install -g Pixnop.Atlas.Cli`.)

## Development

This project uses [OpenSpec](https://github.com/Fission-AI/OpenSpec) for spec-driven
development — each feature is proposed as a spec before it is implemented. See the
`openspec/` directory.

## License

[MIT](./LICENSE)
