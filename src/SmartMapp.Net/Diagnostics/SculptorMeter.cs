using System.Diagnostics.Metrics;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Sprint 9 · S9-T10 OpenTelemetry surface for adaptive-promotion lifecycle events. A single
/// process-wide <see cref="Meter"/> exposes the spec-mandated counters
/// (<c>smartmappnet.cache.promotions</c>, <c>…promotion_failures</c>,
/// <c>…compile_duration_ms</c>) so OTel collectors can observe promotion behaviour without
/// reaching into <see cref="Engine.Promotion.AdaptivePromotionManager"/> internals.
/// </summary>
/// <remarks>
/// All emission sites are zero-allocation when no <see cref="MeterListener"/> is registered —
/// the runtime short-circuits <see cref="Counter{T}.Add(T)"/> when there are no listeners.
/// </remarks>
public static class SculptorMeter
{
    /// <summary>The <see cref="Meter"/> instance every counter is registered against.</summary>
    public static Meter Instance { get; } = new("SmartMapp.Net", "1.0.0");

    internal static readonly Counter<long> PromotionsCounter =
        Instance.CreateCounter<long>(
            "smartmappnet.cache.promotions",
            unit: "{promotion}",
            description: "Successful Compiled→ILEmit adaptive promotions, tagged by origin/target type.");

    internal static readonly Counter<long> PromotionFailuresCounter =
        Instance.CreateCounter<long>(
            "smartmappnet.cache.promotion_failures",
            unit: "{failure}",
            description: "Adaptive-promotion attempts that failed during background compilation.");

    internal static readonly Histogram<double> CompileDurationHistogram =
        Instance.CreateHistogram<double>(
            "smartmappnet.cache.compile_duration_ms",
            unit: "ms",
            description: "Wall-time of IL-Emit compilation during adaptive promotion.");

    internal static void RecordPromotion(TypePair pair, double compileDurationMs)
    {
        var originTag = new KeyValuePair<string, object?>("origin_type", pair.OriginType.FullName ?? pair.OriginType.Name);
        var targetTag = new KeyValuePair<string, object?>("target_type", pair.TargetType.FullName ?? pair.TargetType.Name);
        PromotionsCounter.Add(1, originTag, targetTag);
        CompileDurationHistogram.Record(compileDurationMs, originTag, targetTag);
    }

    internal static void RecordPromotionFailure(TypePair pair, string reason)
    {
        PromotionFailuresCounter.Add(
            1,
            new KeyValuePair<string, object?>("origin_type", pair.OriginType.FullName ?? pair.OriginType.Name),
            new KeyValuePair<string, object?>("target_type", pair.TargetType.FullName ?? pair.TargetType.Name),
            new KeyValuePair<string, object?>("reason", reason));
    }
}
