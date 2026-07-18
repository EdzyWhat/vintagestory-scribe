using Atlas.XUnit;
using Xunit;

// Atlas hosts at most one live server per process; scenario classes must run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// The mod itself is staged via the ProjectReference AtlasMod=true sugar in the .csproj
// (see atlas-mods.generated.txt, written at build time), so no path is declared here.
