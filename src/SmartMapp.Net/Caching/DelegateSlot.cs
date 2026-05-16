using System.Runtime.CompilerServices;

namespace SmartMapp.Net.Caching;

/// <summary>
/// Sprint 9 · S9-T08 atomically-swappable delegate slot used by <see cref="MappingDelegateCache"/>.
/// Holds the current compiled mapping delegate behind a volatile-readable field so the adaptive
/// promotion manager can publish an IL-emitted replacement via
/// <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/> while concurrent
/// <c>Mapper&lt;,&gt;.Map</c> readers observe either the old or the new delegate but never a torn
/// reference.
/// </summary>
public sealed class DelegateSlot
{
    private Func<object, MappingScope, object>? _delegate;
    private readonly object _initLock = new();

    /// <summary>
    /// Returns the currently published delegate, compiling it on first access via
    /// <paramref name="factory"/>. The factory is invoked exactly once even under concurrent
    /// callers (<see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> semantics).
    /// </summary>
    public Func<object, MappingScope, object> GetOrCompile(Func<Func<object, MappingScope, object>> factory)
    {
        var current = Volatile.Read(ref _delegate);
        if (current is not null) return current;
        lock (_initLock)
        {
            if (_delegate is null)
            {
                _delegate = factory();
            }
            return _delegate;
        }
    }

    /// <summary>
    /// Returns the currently published delegate without compiling it. Returns <c>null</c> when
    /// the slot has not yet been initialised.
    /// </summary>
    public Func<object, MappingScope, object>? Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _delegate);
    }

    /// <summary>
    /// Atomically replaces the published delegate with <paramref name="newDelegate"/>. Returns
    /// <c>true</c> when the swap succeeded; <c>false</c> when the slot was uninitialised or had
    /// already been replaced with a different reference (rare under the single-writer promotion
    /// worker; reported for diagnostics).
    /// </summary>
    public bool TrySwap(Func<object, MappingScope, object> newDelegate)
    {
        var expected = Volatile.Read(ref _delegate);
        if (expected is null) return false;
        return Interlocked.CompareExchange(ref _delegate, newDelegate, expected) == expected;
    }
}
