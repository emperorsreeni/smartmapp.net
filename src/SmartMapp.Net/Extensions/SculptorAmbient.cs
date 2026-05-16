namespace SmartMapp.Net.Extensions;

/// <summary>
/// Ambient accessor for the current <see cref="ISculptor"/>. Populated by the SmartMapp.Net DI
/// integration (<c>SmartMapp.Net.DependencyInjection</c>) on first resolve of <see cref="ISculptor"/>,
/// and consulted by the opt-in <see cref="SculptorObjectExtensions.MapTo{TTarget}(object)"/>
/// shortcut and the ambient <c>IQueryable.SelectAs&lt;TTarget&gt;()</c> overload shipped in
/// the DI package.
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Sprint 8 · S8-T07 per spec §14.5. The accessor is <b>opt-in</b>: callers only
/// see the fluent <c>.MapTo&lt;T&gt;()</c> / <c>.SelectAs&lt;T&gt;()</c> shortcuts when they
/// import <c>SmartMapp.Net.Extensions</c>, keeping the global object surface clean.
/// </para>
/// <para>
/// Storage is an <see cref="AsyncLocal{T}"/> of <see cref="ISculptor"/> so per-async-context
/// overrides work safely in parallel request handlers. <see cref="Install"/> sets the slot
/// once (during DI registration) as the root-level default; callers may push scoped overrides
/// via <see cref="Set"/> which returns an <see cref="IDisposable"/> that restores the previous
/// value on dispose.
/// </para>
/// </remarks>
public static class SculptorAmbient
{
    private static readonly AsyncLocal<ISculptor?> Slot = new();

    /// <summary>
    /// Gets the ambient <see cref="ISculptor"/>, or <c>null</c> when no <c>AddSculptor()</c>
    /// call has populated one in the current async context.
    /// </summary>
    public static ISculptor? Current => Slot.Value;

    /// <summary>
    /// Overrides the ambient <see cref="ISculptor"/> for the current async context. Returns a
    /// restore token that reverts <see cref="Current"/> to its previous value on dispose —
    /// safe to nest.
    /// </summary>
    /// <param name="sculptor">The sculptor to install.</param>
    /// <returns>An <see cref="IDisposable"/> scope token.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sculptor"/> is <c>null</c>.</exception>
    public static IDisposable Set(ISculptor sculptor)
    {
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        var previous = Slot.Value;
        Slot.Value = sculptor;
        return new Scope(previous);
    }

    /// <summary>
    /// Installs <paramref name="sculptor"/> as the root-level ambient — called once by the DI
    /// integration after the container forges the sculptor, so every subsequent
    /// <c>.MapTo&lt;T&gt;()</c> / <c>.SelectAs&lt;T&gt;()</c> invocation in the process sees
    /// it without needing an explicit argument. Overwrites any previous ambient (differs from
    /// <see cref="Set"/> which preserves-and-restores).
    /// </summary>
    /// <param name="sculptor">The root sculptor.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sculptor"/> is <c>null</c>.</exception>
    internal static void Install(ISculptor sculptor)
    {
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        Slot.Value = sculptor;
    }

    /// <summary>
    /// Clears the ambient slot — invoked by the DI package when the <c>IServiceProvider</c> is
    /// disposed so test harnesses and composable hosts don't leak state across containers.
    /// </summary>
    internal static void Clear() => Slot.Value = null;

    private readonly struct Scope : IDisposable
    {
        private readonly ISculptor? _previous;
        internal Scope(ISculptor? previous) { _previous = previous; }
        public void Dispose() => Slot.Value = _previous;
    }
}
