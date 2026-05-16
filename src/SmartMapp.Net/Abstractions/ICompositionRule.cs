namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Fluent interface for configuring multi-origin composition mappings. Sprint 8 · S8-T08
/// delivers a minimal <see cref="FromOrigin{TOrigin}"/> accumulator for 1-, 2-, and 3-origin
/// scenarios; the richer <c>.Transform</c>, <c>.When</c>, <c>.OnlyIf</c>, and merge/override
/// surfaces ship in Sprint 15 per spec §8.11.
/// </summary>
/// <typeparam name="TTarget">The composed target type.</typeparam>
public interface ICompositionRule<TTarget>
{
    /// <summary>
    /// Registers an origin type that contributes to <typeparamref name="TTarget"/>. The
    /// optional <paramref name="configure"/> callback accesses the standard
    /// <see cref="IBindingRule{TOrigin, TTarget}"/> fluent surface so the same
    /// <c>.Property(...)</c> rules used for ordinary <c>Bind&lt;S,D&gt;()</c> apply to the
    /// per-origin partial blueprint.
    /// </summary>
    /// <typeparam name="TOrigin">The origin type contributing to the target.</typeparam>
    /// <param name="configure">Optional fluent configuration callback.</param>
    /// <returns>This rule for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the same <typeparamref name="TOrigin"/> is registered twice on the same
    /// composition rule (spec §S8-T08 Unit-Tests: "duplicate-origin rejection").
    /// </exception>
    ICompositionRule<TTarget> FromOrigin<TOrigin>(Action<IBindingRule<TOrigin, TTarget>>? configure = null);
}
