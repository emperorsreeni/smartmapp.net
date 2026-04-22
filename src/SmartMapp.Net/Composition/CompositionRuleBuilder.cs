// SPDX-License-Identifier: MIT
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Composition;

/// <summary>
/// Internal accumulator implementing <see cref="ICompositionRule{TTarget}"/>. Records each
/// <c>FromOrigin&lt;TOrigin&gt;()</c> call as a fresh <see cref="BindingConfiguration"/> so
/// the standard <see cref="IBindingRule{TOrigin, TTarget}"/> fluent surface (Property, When,
/// BuildWith, …) applies to the per-origin partial blueprint. The list is consumed at
/// <see cref="Runtime.SculptorBuildPipeline.Execute"/> time, materialised into per-origin
/// <see cref="Blueprint"/> instances, and bundled into a <see cref="CompositionBlueprint"/>
/// stashed on <see cref="Runtime.ForgedSculptorConfiguration"/> (Sprint 8 · S8-T08).
/// </summary>
/// <remarks>
/// Spec §S8-T08 Outputs bullet 2 lists
/// <c>src/SmartMapp.Net/Composition/CompositionRuleBuilder.cs</c> as the canonical location
/// for this type. The class was originally placed under <c>Abstractions/CompositionRule.cs</c>
/// during the Sprint 8 first-pass implementation; the Sprint 8 holistic review moved it here
/// and renamed the type to match the spec's file-name contract.
/// </remarks>
/// <typeparam name="TTarget">The composed target type.</typeparam>
internal sealed class CompositionRuleBuilder<TTarget> : ICompositionRule<TTarget>, ICompositionRuleInternal
{
    private readonly List<(Type OriginType, BindingConfiguration Config)> _origins = new();

    /// <inheritdoc />
    public Type TargetType => typeof(TTarget);

    /// <inheritdoc />
    public IReadOnlyList<(Type OriginType, BindingConfiguration Config)> Origins => _origins;

    /// <inheritdoc />
    public ICompositionRule<TTarget> FromOrigin<TOrigin>(Action<IBindingRule<TOrigin, TTarget>>? configure = null)
    {
        var originType = typeof(TOrigin);
        foreach (var existing in _origins)
        {
            if (existing.OriginType == originType)
            {
                throw new InvalidOperationException(
                    $"Duplicate composition origin: '{originType.Name}' is already registered for target " +
                    $"'{typeof(TTarget).Name}'. Each origin type may appear only once per composition rule " +
                    "(spec §S8-T08 Unit-Tests: duplicate-origin rejection).");
            }
        }

        var pair = new TypePair(originType, typeof(TTarget));
        var config = new BindingConfiguration(pair);
        var rule = new BindingRule<TOrigin, TTarget>(config);
        configure?.Invoke(rule);
        _origins.Add((originType, config));
        return this;
    }
}
