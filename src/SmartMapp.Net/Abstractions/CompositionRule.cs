namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Sprint 7 stub implementation of <see cref="ICompositionRule{TTarget}"/>. Records
/// origin-type registrations via <see cref="FromOrigin{TOrigin}"/> so downstream
/// validation and introspection work, but execution is deferred to Sprint 15.
/// </summary>
/// <typeparam name="TTarget">The composed target type.</typeparam>
internal sealed class CompositionRule<TTarget> : ICompositionRule<TTarget>
{
    private readonly List<Type> _originTypes = new();

    /// <summary>
    /// Gets the origin types registered against this composition rule.
    /// </summary>
    internal IReadOnlyList<Type> OriginTypes => _originTypes;

    /// <summary>
    /// Records an origin type for later composition. Multi-origin execution is a Sprint 15 feature.
    /// </summary>
    /// <typeparam name="TOrigin">The origin type.</typeparam>
    /// <returns>This rule for chaining.</returns>
    internal CompositionRule<TTarget> FromOrigin<TOrigin>()
    {
        if (!_originTypes.Contains(typeof(TOrigin)))
            _originTypes.Add(typeof(TOrigin));
        return this;
    }
}
