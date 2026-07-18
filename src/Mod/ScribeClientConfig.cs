namespace Scribe;

/// <summary>
/// Client-only display preferences for the lectern GUI. Stored per-side via
/// <c>ICoreAPICommon.StoreModConfig</c>/<c>LoadModConfig</c>, which are never synced between
/// client and server — this must never be written into a <c>Scribe.Core.ScribeDocument</c>.
/// </summary>
public sealed class ScribeClientConfig
{
    public float TextSizeScale = 1f;
}
