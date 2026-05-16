namespace SmartMapp.Net.Runtime;

/// <summary>
/// Async-local slot holding the <see cref="IServiceProvider"/> currently in scope for
/// sculptor-driven mapping calls. Populated by the DI integration
/// (<c>SmartMapp.Net.DependencyInjection</c>) immediately before invoking
/// <c>ISculptor.Map</c> / <c>IMapper&lt;,&gt;.Map</c>, and cleared in the matching
/// <c>finally</c> so no request scope leaks across awaits.
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Sprint 8 · S8-T04 per spec §11.4 ("<c>IMappingScopeFactory</c> acquires the
/// current <see cref="IServiceProvider"/> via a cached <c>AsyncLocal&lt;IServiceProvider?&gt;</c>
/// set inside the sculptor's DI wrapper"). <see cref="MappingExecutor.CreateScope"/> consults
/// <see cref="Current"/> as a fallback when the caller supplies no explicit
/// <see cref="IServiceProvider"/>, so deferred providers and transformers automatically flow
/// against the ambient request scope.
/// </para>
/// <para>
/// Use <see cref="Enter"/> with a <c>using</c> statement to guarantee the slot is restored to
/// its previous value — the token pattern matches <see cref="System.Threading.ExecutionContext"/>
/// and is safe to nest.
/// </para>
/// </remarks>
internal static class AmbientServiceProvider
{
    private static readonly AsyncLocal<IServiceProvider?> Slot = new();

    /// <summary>
    /// Gets the current ambient <see cref="IServiceProvider"/>, or <c>null</c> when no
    /// DI scope is active for this async context.
    /// </summary>
    internal static IServiceProvider? Current => Slot.Value;

    /// <summary>
    /// Pushes <paramref name="serviceProvider"/> onto the async-local slot and returns a
    /// token that restores the previous value when disposed. Safe to nest.
    /// </summary>
    /// <param name="serviceProvider">The provider to install. May be <c>null</c>.</param>
    internal static Scope Enter(IServiceProvider? serviceProvider)
    {
        var previous = Slot.Value;
        Slot.Value = serviceProvider;
        return new Scope(previous);
    }

    /// <summary>
    /// Restore-token returned from <see cref="Enter"/>.
    /// </summary>
    internal readonly struct Scope : IDisposable
    {
        private readonly IServiceProvider? _previous;

        internal Scope(IServiceProvider? previous)
        {
            _previous = previous;
        }

        /// <summary>
        /// Restores the previous ambient <see cref="IServiceProvider"/>.
        /// </summary>
        public void Dispose() => Slot.Value = _previous;
    }
}
