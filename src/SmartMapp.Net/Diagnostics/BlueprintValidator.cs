using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Validates all registered blueprints for configuration errors.
/// Detects duplicate bindings, unlinked required members, circular inheritance,
/// missing <c>Otherwise()</c> on discriminators, and invalid <c>Materialize&lt;T&gt;()</c> types.
/// </summary>
internal sealed class BlueprintValidator
{
    private readonly TypeModelCache _typeModelCache;

    /// <summary>
    /// Initializes a new <see cref="BlueprintValidator"/>.
    /// </summary>
    internal BlueprintValidator(TypeModelCache typeModelCache)
    {
        _typeModelCache = typeModelCache;
    }

    /// <summary>
    /// Validates all blueprints and binding configurations.
    /// </summary>
    /// <param name="blueprints">The built blueprints.</param>
    /// <param name="configs">The binding configurations (for extended validation).</param>
    /// <param name="strictMode">Whether strict mode is enabled globally.</param>
    /// <returns>The validation result containing all errors and warnings.</returns>
    internal BlueprintValidationResult Validate(
        IReadOnlyList<Blueprint> blueprints,
        IReadOnlyList<BindingConfiguration>? configs = null,
        bool strictMode = false)
    {
        var result = new BlueprintValidationResult();

        ValidateDuplicateBindings(blueprints, result);
        ValidateUnlinkedMembers(blueprints, strictMode, result);

        if (configs is not null)
        {
            ValidateDiscriminatorConfigs(configs, result);
            ValidateMaterializeTypes(configs, result);
            ValidateCircularInheritance(configs, result);
        }

        return result;
    }

    /// <summary>
    /// Detects duplicate type pair bindings.
    /// </summary>
    private static void ValidateDuplicateBindings(
        IReadOnlyList<Blueprint> blueprints,
        BlueprintValidationResult result)
    {
        var seen = new HashSet<TypePair>();
        foreach (var bp in blueprints)
        {
            if (!seen.Add(bp.TypePair))
            {
                result.AddError(bp.OriginType, bp.TargetType,
                    $"Duplicate binding for type pair '{bp.TypePair}'.");
            }
        }
    }

    /// <summary>
    /// Validates that all target members are linked (strict mode) or that required members are linked.
    /// </summary>
    private void ValidateUnlinkedMembers(
        IReadOnlyList<Blueprint> blueprints,
        bool strictMode,
        BlueprintValidationResult result)
    {
        foreach (var bp in blueprints)
        {
            var targetModel = _typeModelCache.GetOrAdd(bp.TargetType);
            var linkedMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var link in bp.Links)
            {
                linkedMembers.Add(link.TargetMember.Name);
            }

            foreach (var member in targetModel.WritableMembers)
            {
                if (linkedMembers.Contains(member.Name))
                    continue;

                var isRequired = member.IsRequired;

                if (isRequired && (strictMode || bp.StrictRequiredMembers))
                {
                    result.AddError(bp.OriginType, bp.TargetType,
                        $"Required target member '{member.Name}' is not linked.");
                }
                else if (strictMode)
                {
                    result.AddWarning(bp.OriginType, bp.TargetType,
                        $"Target member '{member.Name}' is not linked.");
                }
            }
        }
    }

    /// <summary>
    /// Validates that all discriminator configurations have an <c>Otherwise()</c> clause.
    /// </summary>
    private static void ValidateDiscriminatorConfigs(
        IReadOnlyList<BindingConfiguration> configs,
        BlueprintValidationResult result)
    {
        foreach (var config in configs)
        {
            if (config.Discriminator is not null && !config.Discriminator.OtherwisePair.HasValue)
            {
                result.AddError(config.TypePair.OriginType, config.TypePair.TargetType,
                    "DiscriminateBy() requires an Otherwise() clause. " +
                    "Add .Otherwise<TFallbackTarget>() to handle unmatched discriminator values.");
            }
        }
    }

    /// <summary>
    /// Validates that Materialize types implement/extend their target type.
    /// </summary>
    private static void ValidateMaterializeTypes(
        IReadOnlyList<BindingConfiguration> configs,
        BlueprintValidationResult result)
    {
        foreach (var config in configs)
        {
            if (config.MaterializeType is not null)
            {
                var targetType = config.TypePair.TargetType;
                var concreteType = config.MaterializeType;

                if (!targetType.IsAssignableFrom(concreteType))
                {
                    result.AddError(config.TypePair.OriginType, config.TypePair.TargetType,
                        $"Materialize type '{concreteType.Name}' does not implement or inherit from target type '{targetType.Name}'.");
                }

                if (concreteType.IsAbstract || concreteType.IsInterface)
                {
                    result.AddError(config.TypePair.OriginType, config.TypePair.TargetType,
                        $"Materialize type '{concreteType.Name}' must be a concrete (non-abstract, non-interface) type.");
                }
            }
        }
    }

    /// <summary>
    /// Detects circular blueprint inheritance (A inherits B inherits A).
    /// </summary>
    private static void ValidateCircularInheritance(
        IReadOnlyList<BindingConfiguration> configs,
        BlueprintValidationResult result)
    {
        var inheritMap = new Dictionary<TypePair, TypePair>();
        foreach (var config in configs)
        {
            if (config.InheritFromPair.HasValue)
            {
                inheritMap[config.TypePair] = config.InheritFromPair.Value;
            }
        }

        foreach (var (derived, _) in inheritMap)
        {
            var visited = new HashSet<TypePair>();
            var current = derived;

            while (inheritMap.TryGetValue(current, out var basePair))
            {
                if (!visited.Add(current))
                {
                    result.AddError(derived.OriginType, derived.TargetType,
                        $"Circular blueprint inheritance detected: {string.Join(" -> ", visited.Select(p => p.ToString()))} -> {current}");
                    break;
                }
                current = basePair;
            }
        }
    }
}
