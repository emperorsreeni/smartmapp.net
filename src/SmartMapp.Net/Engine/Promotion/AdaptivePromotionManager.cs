namespace SmartMapp.Net.Engine.Promotion;

/// <summary>
/// Sprint 9 · S9-T07 adaptive-promotion orchestrator. Forward declaration emitted in T05 so
/// <see cref="Runtime.ForgedSculptorConfiguration"/> can hold an optional reference; the full
/// state machine, channel-backed promotion queue, and background worker land in T07–T09.
/// </summary>
internal sealed partial class AdaptivePromotionManager
{
    private readonly Runtime.ForgedSculptorConfiguration _config;

    internal AdaptivePromotionManager(Runtime.ForgedSculptorConfiguration config)
        => _config = config;

    // The Observe / Promote surface is delivered by the partial in
    // AdaptivePromotionManager.Promotion.cs (S9-T07) so this class can be referenced from
    // S9-T05 (strategy selector) without forward-declaring stubs.

    /// <summary>
    /// Registers <paramref name="blueprint"/> with the manager so its invocation counter is
    /// observed on every <c>Mapper&lt;,&gt;.Map</c> call. Idempotent. Concrete state lives in
    /// the T07 partial; this T05-era stub is a no-op so the strategy selector can call into
    /// it unconditionally.
    /// </summary>
    internal void Register(Blueprint blueprint) => RegisterCore(blueprint);

    /// <summary>Implementation hook supplied by the T07 partial.</summary>
    partial void RegisterCore(Blueprint blueprint);
}
