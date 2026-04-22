namespace SmartMapp.Net.Extensions;

/// <summary>
/// Opt-in fluent extensions on <see cref="object"/> that project a source instance into a
/// target type via the ambient — or explicitly supplied — <see cref="ISculptor"/>. Placed in
/// the <c>SmartMapp.Net.Extensions</c> namespace so callers must deliberately <c>using</c> the
/// namespace to get the shortcuts, keeping the default <see cref="object"/> surface clean.
/// </summary>
/// <remarks>
/// Introduced in Sprint 8 · S8-T07 per spec §14.5.
/// </remarks>
public static class SculptorObjectExtensions
{
    /// <summary>
    /// Maps <paramref name="source"/> into a new <typeparamref name="TTarget"/> using the ambient
    /// <see cref="ISculptor"/> populated by <see cref="SculptorAmbient.Install"/> (called from
    /// <c>services.AddSculptor()</c>).
    /// </summary>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped <typeparamref name="TTarget"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no ambient <see cref="ISculptor"/> is available in the current async context.
    /// The message points the caller at <c>services.AddSculptor()</c> or the explicit-sculptor
    /// overload <see cref="MapTo{TTarget}(object, ISculptor)"/>.
    /// </exception>
    public static TTarget MapTo<TTarget>(this object source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var sculptor = SculptorAmbient.Current
            ?? throw new InvalidOperationException(
                "No ambient ISculptor is available. Either call services.AddSculptor() to register one " +
                "(which installs the ambient accessor), use SculptorAmbient.Set(sculptor) to push a scoped " +
                "override, or pass the sculptor explicitly via MapTo<TTarget>(sculptor).");

        return (TTarget)sculptor.Map(source, source.GetType(), typeof(TTarget));
    }

    /// <summary>
    /// Maps <paramref name="source"/> into a new <typeparamref name="TTarget"/> using the
    /// explicitly supplied <paramref name="sculptor"/>. Recommended for hot code paths and
    /// non-DI scenarios — no ambient lookup, easier to mock in tests.
    /// </summary>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <param name="sculptor">The sculptor to use.</param>
    /// <returns>The mapped <typeparamref name="TTarget"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <c>null</c>.</exception>
    public static TTarget MapTo<TTarget>(this object source, ISculptor sculptor)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));

        return (TTarget)sculptor.Map(source, source.GetType(), typeof(TTarget));
    }
}
