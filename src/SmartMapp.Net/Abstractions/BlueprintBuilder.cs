using System.Reflection;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Composition;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Internal implementation of <see cref="IBlueprintBuilder"/>.
/// Accumulates binding configurations during <see cref="MappingBlueprint.Design"/>
/// and converts them to immutable <see cref="Blueprint"/> instances via <see cref="Build"/>.
/// </summary>
internal sealed class BlueprintBuilder : IBlueprintBuilder
{
    private readonly List<BindingConfiguration> _bindings = new();
    private readonly HashSet<TypePair> _registeredPairs = new();
    private readonly List<ICompositionRuleInternal> _compositions = new();

    /// <summary>
    /// Gets all accumulated binding configurations.
    /// </summary>
    internal IReadOnlyList<BindingConfiguration> Bindings => _bindings;

    /// <summary>
    /// Gets the accumulated composition rules registered through <see cref="Compose{TTarget}"/>
    /// or <c>options.Compose&lt;T&gt;()</c>. Consumed by the forge pipeline (Sprint 8 · S8-T08)
    /// to produce <see cref="Composition.CompositionBlueprint"/> records.
    /// </summary>
    internal IReadOnlyList<ICompositionRuleInternal> Compositions => _compositions;

    /// <summary>
    /// Gets the inheritance resolver produced during <see cref="Build"/>.
    /// Non-null only after <see cref="Build"/> has been called.
    /// </summary>
    internal InheritanceResolver? ResolvedInheritanceResolver { get; private set; }

    /// <summary>
    /// Returns <c>true</c> when a binding for <paramref name="pair"/> is already registered.
    /// </summary>
    internal bool IsRegistered(TypePair pair) => _registeredPairs.Contains(pair);

    /// <summary>
    /// Registers an empty binding for a runtime-supplied pair. Used by the scanner integration
    /// in <see cref="Runtime.SculptorBuildPipeline"/> for attribute-discovered type pairs that
    /// have no explicit fluent or blueprint configuration.
    /// </summary>
    /// <param name="pair">The type pair to register.</param>
    /// <returns>The newly created <see cref="BindingConfiguration"/>, or <c>null</c> if the
    /// pair was already registered.</returns>
    internal BindingConfiguration? RegisterEmpty(TypePair pair)
    {
        if (!_registeredPairs.Add(pair)) return null;
        var config = new BindingConfiguration(pair);
        _bindings.Add(config);
        return config;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> Bind<TOrigin, TTarget>()
    {
        var pair = TypePair.Of<TOrigin, TTarget>();

        if (!_registeredPairs.Add(pair))
            throw new InvalidOperationException(
                $"Duplicate binding: '{typeof(TOrigin).Name} -> {typeof(TTarget).Name}' is already registered in this blueprint. " +
                "Each type pair can only be bound once per MappingBlueprint.");

        var config = new BindingConfiguration(pair);
        _bindings.Add(config);
        return new BindingRule<TOrigin, TTarget>(config);
    }

    /// <inheritdoc />
    public ICompositionRule<TTarget> Compose<TTarget>()
    {
        // Sprint 8 · S8-T08: stash the rule on the builder so the forge pipeline can later
        // materialise it into a CompositionBlueprint. Each Compose<TTarget>() call produces a
        // fresh CompositionRule — multiple calls for the same target create separate rules,
        // which the pipeline flattens / validates for ambiguity during forge.
        var rule = new CompositionRuleBuilder<TTarget>();
        _compositions.Add(rule);
        return rule;
    }

    /// <summary>
    /// Converts all accumulated binding configurations into immutable <see cref="Blueprint"/> instances.
    /// Runs the full build pipeline: build → inheritance resolution → bidirectional generation → validation.
    /// </summary>
    /// <param name="validate">Whether to run validation after building (default: true).</param>
    /// <returns>The list of built blueprints.</returns>
    internal IReadOnlyList<Blueprint> Build(bool validate = true)
    {
        // Phase 1: Build raw blueprints from configurations
        var blueprints = new List<Blueprint>(_bindings.Count);
        foreach (var config in _bindings)
        {
            blueprints.Add(BuildBlueprint(config));
        }

        // Phase 2: Feed inheritance metadata into an InheritanceResolver
        var allKnownTypes = new HashSet<Type>();
        foreach (var bp in blueprints)
        {
            allKnownTypes.Add(bp.OriginType);
            allKnownTypes.Add(bp.TargetType);
        }

        var inheritanceResolver = new InheritanceResolver(allKnownTypes);

        foreach (var config in _bindings)
        {
            foreach (var derivedPair in config.ExplicitDerivedPairs)
                inheritanceResolver.RegisterExplicitDerivedPair(config.TypePair, derivedPair);

            if (config.Discriminator is not null)
                inheritanceResolver.RegisterDiscriminator(config.TypePair, config.Discriminator);

            if (config.MaterializeType is not null)
                inheritanceResolver.RegisterMaterializeType(config.TypePair, config.MaterializeType);

            if (config.InheritFromPair.HasValue)
                inheritanceResolver.RegisterInheritFrom(config.TypePair, config.InheritFromPair.Value);
        }

        inheritanceResolver.BuildDerivedPairLookup(blueprints.Select(b => b.TypePair));
        ResolvedInheritanceResolver = inheritanceResolver;

        // Phase 3: Resolve blueprint inheritance (InheritFrom)
        var inheritResolver = new BlueprintInheritanceResolver(inheritanceResolver, blueprints);
        blueprints = new List<Blueprint>(inheritResolver.ResolveAll());

        // Phase 4: Generate inverse blueprints for bidirectional bindings
        var biMapper = new BidirectionalMapper();
        var inverseBps = biMapper.GenerateInverseBlueprints(_bindings, blueprints);
        blueprints.AddRange(inverseBps);

        // Phase 5: Validate
        if (validate)
        {
            var validator = new BlueprintValidator(TypeModelCache.Default);
            // Per-binding strict mode is carried on Blueprint.StrictRequiredMembers.
            // Global strictMode only controls warnings for optional unlinked members.
            var result = validator.Validate(blueprints, _bindings, strictMode: false);
            if (!result.IsValid)
                throw new BlueprintValidationException(result);
        }

        return blueprints;
    }

    /// <summary>
    /// Converts a single <see cref="BindingConfiguration"/> into an immutable <see cref="Blueprint"/>.
    /// Exposed to the forge pipeline so composition origin partials (Sprint 8 · S8-T08) can reuse
    /// the same link-building logic as ordinary bindings.
    /// </summary>
    internal static Blueprint BuildBlueprintFromConfig(BindingConfiguration config)
        => BuildBlueprint(config);

    /// <summary>
    /// Converts a single <see cref="BindingConfiguration"/> into an immutable <see cref="Blueprint"/>.
    /// </summary>
    private static Blueprint BuildBlueprint(BindingConfiguration config)
    {
        var links = new List<PropertyLink>();

        foreach (var propConfig in config.PropertyConfigs)
        {
            var link = BuildPropertyLink(propConfig, config.TypePair);
            links.Add(link);
        }

        // Sort links by order
        links.Sort((a, b) => a.Order.CompareTo(b.Order));

        // Build OnMapping/OnMapped hooks as Action<object, object>
        Action<object, object>? onMapping = null;
        if (config.OnMappingHook is not null)
        {
            var hook = config.OnMappingHook;
            onMapping = (origin, target) => hook.DynamicInvoke(origin, target);
        }

        Action<object, object>? onMapped = null;
        if (config.OnMappedHook is not null)
        {
            var hook = config.OnMappedHook;
            onMapped = (origin, target) => hook.DynamicInvoke(origin, target);
        }

        // Build TargetFactory from FactoryExpression
        Func<object, object>? targetFactory = null;
        if (config.FactoryExpression is not null)
        {
            var compiled = config.FactoryExpression.Compile();
            targetFactory = origin => compiled.DynamicInvoke(origin)!;
        }

        // Build type-level Condition from When() predicate
        Func<object, bool>? typeLevelCondition = null;
        if (config.Condition is not null)
        {
            var compiled = config.Condition.Compile();
            typeLevelCondition = origin => (bool)compiled.DynamicInvoke(origin)!;
        }

        return new Blueprint
        {
            OriginType = config.TypePair.OriginType,
            TargetType = config.TypePair.TargetType,
            Links = links,
            MaxDepth = config.MaxDepth ?? int.MaxValue,
            TrackReferences = config.TrackReferences,
            OnMapping = onMapping,
            OnMapped = onMapped,
            StrictRequiredMembers = config.StrictMode,
            TargetFactory = targetFactory,
            Condition = typeLevelCondition,
            TypeTransformer = config.TransformerType,
        };
    }

    /// <summary>
    /// Converts a <see cref="PropertyConfiguration"/> into an immutable <see cref="PropertyLink"/>.
    /// </summary>
    private static PropertyLink BuildPropertyLink(PropertyConfiguration propConfig, TypePair typePair)
    {
        IValueProvider provider;
        ConventionMatch linkedBy;

        if (propConfig.IsSkipped)
        {
            provider = new NullValueProvider();
            linkedBy = ConventionMatch.Explicit("(skipped)");
        }
        else if (propConfig.OriginExpression is not null)
        {
            provider = new ExpressionValueProvider(propConfig.OriginExpression);
            linkedBy = ConventionMatch.Explicit(propConfig.OriginExpression.ToString());
        }
        else if (propConfig.ProviderType is not null)
        {
            provider = new DeferredValueProvider(propConfig.ProviderType);
            linkedBy = ConventionMatch.CustomProvider(propConfig.ProviderType);
        }
        else
        {
            // No explicit configuration — will be resolved by convention pipeline
            provider = new NullValueProvider();
            linkedBy = ConventionMatch.Explicit("(unresolved)");
        }

        // Compile condition/pre-condition lambdas to delegates
        Func<object, bool>? condition = null;
        if (propConfig.Condition is not null)
        {
            var compiled = propConfig.Condition.Compile();
            condition = origin => (bool)compiled.DynamicInvoke(origin)!;
        }

        Func<object, bool>? preCondition = null;
        if (propConfig.PreCondition is not null)
        {
            var compiled = propConfig.PreCondition.Compile();
            preCondition = origin => (bool)compiled.DynamicInvoke(origin)!;
        }

        // Resolve transformer: explicit type, inline expression, or null
        ITypeTransformer? transformer = null;
        if (propConfig.TransformerType is not null)
        {
            transformer = new DeferredTypeTransformer(propConfig.TransformerType);
        }
        else if (propConfig.InlineTransform is not null)
        {
            transformer = new InlineTypeTransformer(propConfig.InlineTransform);
        }
        else if (propConfig.PostProcess is not null)
        {
            // PostProcess is treated as a transformer applied after the main value is assigned
            transformer = new InlineTypeTransformer(propConfig.PostProcess);
        }

        return new PropertyLink
        {
            TargetMember = propConfig.TargetMember,
            Provider = provider,
            LinkedBy = linkedBy,
            IsSkipped = propConfig.IsSkipped,
            Fallback = propConfig.HasFallback ? propConfig.FallbackValue : null,
            Order = propConfig.Order,
            Condition = condition,
            PreCondition = preCondition,
            Transformer = transformer,
        };
    }
}

/// <summary>
/// A type transformer placeholder for DI-resolved transformers. Resolved at mapping time.
/// </summary>
internal sealed class DeferredTypeTransformer : ITypeTransformer
{
    /// <summary>
    /// Gets the type of the DI-resolved transformer.
    /// </summary>
    internal Type TransformerType { get; }

    internal DeferredTypeTransformer(Type transformerType)
    {
        TransformerType = transformerType;
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType) => true;
}

/// <summary>
/// A type transformer that wraps an inline lambda expression (from <c>.TransformWith(v => ...)</c>
/// or <c>.PostProcess(v => ...)</c>).
/// </summary>
internal sealed class InlineTypeTransformer : ITypeTransformer
{
    private readonly Delegate _compiled;

    /// <summary>
    /// Gets the original lambda expression.
    /// </summary>
    internal System.Linq.Expressions.LambdaExpression Expression { get; }

    internal InlineTypeTransformer(System.Linq.Expressions.LambdaExpression expression)
    {
        Expression = expression;
        _compiled = expression.Compile();
    }

    /// <summary>
    /// Applies the inline transform to the given value.
    /// </summary>
    internal object? Transform(object? value) => _compiled.DynamicInvoke(value);

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType) => true;
}

/// <summary>
/// A value provider that wraps a lambda expression for explicit <c>.From(s => ...)</c> configuration.
/// </summary>
internal sealed class ExpressionValueProvider : IValueProvider
{
    private readonly Delegate _compiled;

    /// <summary>
    /// Gets the original lambda expression.
    /// </summary>
    internal System.Linq.Expressions.LambdaExpression Expression { get; }

    /// <summary>
    /// Initializes a new <see cref="ExpressionValueProvider"/>.
    /// </summary>
    internal ExpressionValueProvider(System.Linq.Expressions.LambdaExpression expression)
    {
        Expression = expression;
        _compiled = expression.Compile();
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        return _compiled.DynamicInvoke(origin);
    }
}

/// <summary>
/// A value provider that always returns <c>null</c>. Used for skipped properties and unresolved links.
/// </summary>
internal sealed class NullValueProvider : IValueProvider
{
    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope) => null;
}

/// <summary>
/// A value provider placeholder for DI-resolved providers. The actual provider is resolved at
/// mapping time via <see cref="MappingScope.ProviderResolver"/> with
/// <see cref="MappingScope.ServiceProvider"/> — falling back to
/// <see cref="Activator.CreateInstance(Type)"/> when the type is not registered in the
/// container or when no container is wired into the sculptor (builder-only usage).
/// </summary>
internal sealed class DeferredValueProvider : IValueProvider
{
    /// <summary>
    /// Gets the type of the DI-resolved provider.
    /// </summary>
    internal Type ProviderType { get; }

    /// <summary>
    /// Initializes a new <see cref="DeferredValueProvider"/>.
    /// </summary>
    internal DeferredValueProvider(Type providerType)
    {
        ProviderType = providerType;
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        // Per spec §11.4 (S8-T04): route through the scope's IProviderResolver. The default
        // resolver uses scope.ServiceProvider.GetService + Activator fallback; the DI package
        // installs a richer resolver that uses ActivatorUtilities.CreateInstance so types with
        // constructor dependencies still activate when they aren't registered explicitly.
        var instance = scope.ProviderResolver.Resolve(ProviderType, scope.ServiceProvider);
        if (instance is not IValueProvider provider)
        {
            throw new InvalidOperationException(
                $"Type '{ProviderType.FullName}' was resolved but does not implement IValueProvider.");
        }

        return provider.Provide(origin, target, targetMemberName, scope);
    }
}
