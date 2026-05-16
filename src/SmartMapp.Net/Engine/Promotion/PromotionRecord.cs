namespace SmartMapp.Net.Engine.Promotion;

/// <summary>
/// Per-<see cref="TypePair"/> record tracking the adaptive-promotion lifecycle. Held inside
/// <see cref="AdaptivePromotionManager"/> and observed (read-only) by
/// <see cref="Diagnostics.MappingInspection"/>.
/// </summary>
internal sealed class PromotionRecord
{
    internal PromotionRecord(TypePair pair) => Pair = pair;

    internal TypePair Pair { get; }

    /// <summary>Volatile state; transitions via <c>Interlocked.CompareExchange</c> on an int field.</summary>
    private int _state = (int)PromotionState.Cold;

    internal PromotionState State => (PromotionState)Volatile.Read(ref _state);

    internal bool TryTransition(PromotionState from, PromotionState to)
        => Interlocked.CompareExchange(ref _state, (int)to, (int)from) == (int)from;

    /// <summary>Set when the manager (or worker) records a non-recoverable failure.</summary>
    internal Exception? LastError { get; set; }

    /// <summary>UTC timestamp of the most recent successful promotion (or <c>null</c> if never).</summary>
    internal DateTimeOffset? PromotedAt { get; set; }

    /// <summary>Wall-time the background compile took, in milliseconds.</summary>
    internal double? CompileDurationMs { get; set; }
}
