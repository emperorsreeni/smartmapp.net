using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SmartMapp.Net.Engine.Promotion;

/// <summary>
/// Sprint 9 · S9-T06 per-<see cref="TypePair"/> invocation counter table. A single
/// <see cref="Interlocked.Increment(ref long)"/> per map call is the entire hot-path budget the
/// adaptive-promotion path adds when <see cref="Configuration.StrategyMode.Adaptive"/> is
/// active. Cold-path sculptors (<c>CompiledOnly</c> / <c>EmitFirst</c> / <c>EmitOnly</c>) never
/// touch this type because the strategy chain never constructs a manager for them.
/// </summary>
/// <remarks>
/// <para>
/// Counters are stored in a <see cref="ConcurrentDictionary{TKey, TValue}"/> indexed by
/// <see cref="TypePair"/>. The value is a <see cref="StrongBox{T}"/> of <see cref="long"/> so
/// <see cref="Interlocked.Increment(ref long)"/> can target a stable address across concurrent
/// callers. After a pair has been promoted, the counter is replaced with the
/// <see cref="Promoted"/> sentinel value and subsequent observations short-circuit immediately
/// to avoid the unbounded <c>Interlocked</c> cost.
/// </para>
/// </remarks>
internal sealed class InvocationCounterTable
{
    /// <summary>Sentinel value indicating a pair has been promoted; further observations no-op.</summary>
    internal const long Promoted = long.MinValue;

    private readonly ConcurrentDictionary<TypePair, StrongBox<long>> _counters = new();

    /// <summary>
    /// Increments the counter for <paramref name="pair"/> and returns the new value. Returns
    /// <see cref="Promoted"/> when the pair has already been promoted, signalling the caller to
    /// skip any promotion scheduling.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long Increment(TypePair pair)
    {
        var box = _counters.GetOrAdd(pair, static _ => new StrongBox<long>(0));
        // Saturate post-promotion: a Volatile.Read avoids the increment-and-back-off round-trip
        // when the counter has already been marked as promoted.
        var current = Volatile.Read(ref box.Value);
        if (current == Promoted) return Promoted;
        return Interlocked.Increment(ref box.Value);
    }

    /// <summary>
    /// Marks <paramref name="pair"/> as promoted. Subsequent <see cref="Increment"/> calls
    /// return <see cref="Promoted"/> immediately without touching <c>Interlocked</c>.
    /// </summary>
    internal void MarkPromoted(TypePair pair)
    {
        var box = _counters.GetOrAdd(pair, static _ => new StrongBox<long>(0));
        Volatile.Write(ref box.Value, Promoted);
    }

    /// <summary>Reads the current count for <paramref name="pair"/> without mutating it.</summary>
    internal long Read(TypePair pair)
        => _counters.TryGetValue(pair, out var box) ? Volatile.Read(ref box.Value) : 0;
}
