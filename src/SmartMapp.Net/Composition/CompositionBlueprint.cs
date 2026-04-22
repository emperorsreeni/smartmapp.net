namespace SmartMapp.Net.Composition;

/// <summary>
/// Immutable record describing a multi-origin composition: a target type that is populated
/// from one or more origin types, each contributing a subset of the target's property links.
/// Produced by <see cref="CompositionRuleBuilder{TTarget}"/> during forge and consumed by
/// <see cref="Runtime.CompositionDispatcher"/> at runtime when <see cref="ISculptor.Compose{TTarget}"/>
/// is invoked with multiple origins — Sprint 8 · S8-T08 per spec §8.11.
/// </summary>
/// <remarks>
/// The <see cref="Origins"/> list preserves declaration order (the order of
/// <c>FromOrigin&lt;TOrigin&gt;()</c> calls on the fluent rule). Collision resolution at
/// dispatch time is last-origin-wins against that declaration order, which makes the
/// composition call-site order-independent — <c>Compose&lt;T&gt;(a, b)</c> and
/// <c>Compose&lt;T&gt;(b, a)</c> produce the same result (spec §S8-T08 Acceptance bullet 3).
/// </remarks>
public sealed record CompositionBlueprint
{
    /// <summary>
    /// Gets the composed target type.
    /// </summary>
    public required Type TargetType { get; init; }

    /// <summary>
    /// Gets the origins registered against this composition, ordered by declaration.
    /// </summary>
    public required IReadOnlyList<CompositionOrigin> Origins { get; init; }
}

/// <summary>
/// A single origin contribution within a <see cref="CompositionBlueprint"/>: the origin type
/// and the partial <see cref="Blueprint"/> describing which target members it populates.
/// </summary>
public sealed record CompositionOrigin
{
    /// <summary>
    /// Gets the origin type that feeds the target composition.
    /// </summary>
    public required Type OriginType { get; init; }

    /// <summary>
    /// Gets the partial <see cref="Blueprint"/> for the <c>(OriginType, TargetType)</c> pair.
    /// Its <see cref="Blueprint.Links"/> identify exactly which target members this origin
    /// contributes; members not listed are left to other origins (or to their default values).
    /// </summary>
    public required Blueprint PartialBlueprint { get; init; }
}
