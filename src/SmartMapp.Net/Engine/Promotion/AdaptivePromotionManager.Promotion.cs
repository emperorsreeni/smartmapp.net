using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SmartMapp.Net.Engine.ILEmit;

namespace SmartMapp.Net.Engine.Promotion;

/// <summary>
/// Sprint 9 · S9-T07/T08 partial of <see cref="AdaptivePromotionManager"/>. Owns the per-pair
/// state machine, the bounded channel that feeds the background worker, and the lock-free
/// CompareExchange swap that publishes the IL-Emit delegate atomically once the worker
/// completes compilation.
/// </summary>
internal sealed partial class AdaptivePromotionManager
{
    private readonly ConcurrentDictionary<TypePair, PromotionRecord> _records = new();
    private readonly InvocationCounterTable _counters = new();
    private readonly Channel<TypePair> _queue = Channel.CreateBounded<TypePair>(
        new BoundedChannelOptions(capacity: 1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    private PromotionWorker? _worker;
    private readonly object _workerInitLock = new();

    /// <summary>Telemetry counter (S9-T10): successful Compiled→IL Emit swaps.</summary>
    internal long SuccessfulPromotions => Volatile.Read(ref _successfulPromotions);
    private long _successfulPromotions;

    /// <summary>Telemetry counter (S9-T10): failed promotion attempts.</summary>
    internal long FailedPromotions => Volatile.Read(ref _failedPromotions);
    private long _failedPromotions;

    internal InvocationCounterTable Counters => _counters;

    /// <summary>Snapshot of all known records (read-only diagnostics view).</summary>
    internal IReadOnlyDictionary<TypePair, PromotionRecord> Records => _records;

    /// <summary>Channel reader consumed by <see cref="PromotionWorker"/>.</summary>
    internal ChannelReader<TypePair> QueueReader => _queue.Reader;

    /// <summary>S9-T07 implementation hook called from the strategy chain on first compile.</summary>
    partial void RegisterCore(Blueprint blueprint)
    {
        var pair = blueprint.TypePair;
        // Disable up-front when the blueprint is not emit-eligible so Observe stays a no-op
        // and the channel never sees this pair. Cheap one-shot probe.
        if (!EmitDiagnostics.CanEmit(blueprint, out _))
        {
            var disabled = _records.GetOrAdd(pair, p => new PromotionRecord(p));
            disabled.TryTransition(PromotionState.Cold, PromotionState.Disabled);
            return;
        }
        _records.GetOrAdd(pair, p => new PromotionRecord(p));
    }

    /// <summary>
    /// Hot-path observation called from <c>Mapper&lt;,&gt;.Map</c> and
    /// <c>Sculptor.Map&lt;,&gt;</c> after each map invocation when the sculptor is in
    /// <see cref="Configuration.StrategyMode.Adaptive"/>. Wait-free; pushes to the promotion
    /// channel only when the per-pair invocation count crosses
    /// <see cref="Configuration.StrategyOptions.PromotionThreshold"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Observe(TypePair pair)
    {
        var count = _counters.Increment(pair);
        if (count == InvocationCounterTable.Promoted) return;
        var threshold = _config.Options.Strategy.PromotionThreshold;
        if (count != threshold) return;

        // Only the exact-threshold observation reaches here so we schedule at most once per pair.
        if (!_records.TryGetValue(pair, out var record)) return;
        if (record.State == PromotionState.Disabled) return;

        if (!record.TryTransition(PromotionState.Cold, PromotionState.Hot)) return;

        EnsureWorkerStarted();

        // Non-blocking TryWrite — channel drops on overflow per FullMode.DropWrite. Dropping is
        // safe because every pair has its own record; subsequent observations are no-ops.
        if (!_queue.Writer.TryWrite(pair))
        {
            // Roll back to Cold so a future observation can retry (rare; channel cap = 1024).
            record.TryTransition(PromotionState.Hot, PromotionState.Cold);
        }
    }

    /// <summary>
    /// Background-worker entry point — compiles the IL-Emit delegate, swaps it atomically, and
    /// updates the per-pair record. Failure paths surface via <see cref="PromotionRecord.LastError"/>
    /// and increment <see cref="FailedPromotions"/>.
    /// </summary>
    [SuppressMessage("Trimming", "IL3050:RequiresDynamicCode",
        Justification = "AdaptivePromotionManager is only constructed when StrategyMode.Adaptive is opted into; that mode is documented as RequiresDynamicCode and the AOT-safe default is CompiledOnly.")]
    internal void PromotePair(TypePair pair)
    {
        if (!_records.TryGetValue(pair, out var record)) return;
        if (!record.TryTransition(PromotionState.Hot, PromotionState.Compiling)) return;

        var blueprint = _config.TryGetBlueprint(pair);
        if (blueprint is null)
        {
            record.LastError = new InvalidOperationException(
                $"Promotion: blueprint for '{pair}' disappeared mid-flight.");
            record.TryTransition(PromotionState.Compiling, PromotionState.Failed);
            Interlocked.Increment(ref _failedPromotions);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var selector = _config.StrategySelector
                ?? throw new InvalidOperationException("Promotion: StrategySelector not initialised.");

            var emitted = selector.TryCompileEmit(blueprint, out var reason);
            if (emitted is null)
            {
                record.LastError = new BlueprintNotEmittableException(pair, reason);
                record.TryTransition(PromotionState.Compiling, PromotionState.Failed);
                Interlocked.Increment(ref _failedPromotions);
                return;
            }

            // S9-T08: atomic publication via the cache's swap method. The Volatile.Read pattern
            // on the consumer side (DelegateSlot.Current) guarantees subsequent Map() calls see
            // the new delegate without ever observing a torn reference.
            var swapped = _config.DelegateCache.TrySwap(pair, emitted);

            sw.Stop();
            record.CompileDurationMs = sw.Elapsed.TotalMilliseconds;

            if (!swapped)
            {
                record.LastError = new InvalidOperationException(
                    $"Promotion: CAS swap rejected for '{pair}' (cache pre-condition failed).");
                record.TryTransition(PromotionState.Compiling, PromotionState.Failed);
                Interlocked.Increment(ref _failedPromotions);
                return;
            }

            record.PromotedAt = DateTimeOffset.UtcNow;
            _config.ActiveStrategies[pair] = MappingStrategy.ILEmit;
            _counters.MarkPromoted(pair);
            record.TryTransition(PromotionState.Compiling, PromotionState.Promoted);
            Interlocked.Increment(ref _successfulPromotions);

            Diagnostics.SculptorMeter.RecordPromotion(pair, record.CompileDurationMs ?? 0);
        }
        catch (Exception ex)
        {
            record.LastError = ex;
            record.TryTransition(PromotionState.Compiling, PromotionState.Failed);
            Interlocked.Increment(ref _failedPromotions);
            Diagnostics.SculptorMeter.RecordPromotionFailure(pair, ex.GetType().Name);
        }
    }

    internal PromotionRecord? TryGetRecord(TypePair pair)
        => _records.TryGetValue(pair, out var r) ? r : null;

    private void EnsureWorkerStarted()
    {
        if (_worker is not null) return;
        lock (_workerInitLock)
        {
            _worker ??= new PromotionWorker(this);
        }
    }

    internal ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        return _worker?.DisposeAsync() ?? default;
    }
}
