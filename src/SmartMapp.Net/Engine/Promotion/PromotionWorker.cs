using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace SmartMapp.Net.Engine.Promotion;

/// <summary>
/// Sprint 9 · S9-T09 dedicated background worker that drains the
/// <see cref="AdaptivePromotionManager"/> promotion queue and invokes the IL-Emit compiler
/// off the caller's thread. Exactly one worker per sculptor; lazily started on the first hot
/// observation so sculptors that never promote pay zero overhead.
/// </summary>
/// <remarks>
/// The worker is hosted on a <see cref="TaskCreationOptions.LongRunning"/> task so it never
/// shares thread-pool budget with user work. Per-pair compilation exceptions are caught and
/// recorded on <see cref="PromotionRecord.LastError"/>; one bad pair never kills the worker.
/// </remarks>
internal sealed class PromotionWorker : IAsyncDisposable
{
    private readonly AdaptivePromotionManager _manager;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _runTask;
    private TaskCompletionSource<bool>? _onIdle;

    [SuppressMessage("Trimming", "IL3050:RequiresDynamicCode",
        Justification = "PromotionWorker is only constructed when StrategyMode.Adaptive is opted into; that mode is documented as RequiresDynamicCode.")]
    internal PromotionWorker(AdaptivePromotionManager manager)
    {
        _manager = manager;
        _runTask = Task.Factory.StartNew(
            RunAsync,
            _cts.Token,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default).Unwrap();
    }

    /// <summary>
    /// Returns a task that completes the next time the worker has fully drained the queue —
    /// used by tests to observe promotion ordering deterministically without relying on real
    /// thread timing (spec §S9-T09 "OnIdleAsync test hook").
    /// </summary>
    internal Task OnIdleAsync()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref _onIdle, tcs);
        return tcs.Task;
    }

    private async Task RunAsync()
    {
        var reader = _manager.QueueReader;
        try
        {
            while (await reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var pair))
                {
                    try
                    {
                        _manager.PromotePair(pair);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Defensive: PromotePair already absorbs its own exceptions, but a
                        // catastrophic failure (OOM, etc.) must not kill the worker for the
                        // sake of subsequent pairs.
                        Diagnostics.SculptorMeter.RecordPromotionFailure(pair, ex.GetType().Name);
                    }
                }

                // Queue currently drained — signal any test waiters.
                var idle = Interlocked.Exchange(ref _onIdle, null);
                idle?.TrySetResult(true);
            }
        }
        catch (OperationCanceledException) { /* graceful disposal */ }
        catch (ChannelClosedException) { /* writer completed */ }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _runTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        _cts.Dispose();
    }
}
