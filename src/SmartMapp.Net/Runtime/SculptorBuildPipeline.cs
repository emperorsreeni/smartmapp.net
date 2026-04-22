using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Orchestrates the <c>Forge()</c> pipeline in six well-defined stages:
/// (1) freeze options, (2) scan assemblies, (3) drain inline bindings,
/// (4) apply blueprint types/instances, (5) register attributed pairs,
/// (6) build/merge/compile/validate. Non-public — use <see cref="SculptorBuilder.Forge"/>.
/// </summary>
internal sealed class SculptorBuildPipeline
{
    private readonly AssemblyScanner _scanner;

    internal SculptorBuildPipeline(AssemblyScanner scanner)
    {
        _scanner = scanner;
    }

    internal ISculptor Execute(SculptorBuildInputs inputs)
    {
        // Stage 1 — Freeze options so downstream stages see immutable config
        inputs.Options.Freeze();

        // Stage 2 — Scan assemblies
        var scanResult = _scanner.Scan(inputs.Assemblies.ToArray());

        // Stage 3 — Drain inline bindings
        foreach (var inline in inputs.InlineBindings)
        {
            inline.Apply(inputs.BlueprintBuilder);
        }

        // Stage 3b — Drain inline compositions (Sprint 8 · S8-T08). The callback runs against the
        // shared IBlueprintBuilder so options.Compose<T>(...) ends up in BlueprintBuilder.Compositions
        // just like SculptorBuilder.Compose<T>() does.
        foreach (var inline in inputs.InlineCompositions)
        {
            inline.Apply(inputs.BlueprintBuilder);
        }

        // Stage 4 — Apply MappingBlueprint instances (user-supplied)
        foreach (var instance in inputs.BlueprintInstances)
        {
            instance.Design(inputs.BlueprintBuilder);
        }

        // Stage 4b — Apply blueprint types.
        // User-explicit blueprints (via UseBlueprint<T>()) fail fast on duplicates.
        // Scanner-discovered blueprints are tolerant — duplicate-pair collisions are silently skipped
        // so that opting in to auto-discovery doesn't break when multiple blueprints cover the same pair.
        var userExplicitBlueprintTypes = new HashSet<Type>(inputs.BlueprintTypes);

        foreach (var blueprintType in inputs.BlueprintTypes)
        {
            InstantiateAndApplyBlueprint(blueprintType, inputs.BlueprintBuilder, tolerant: false);
        }

        foreach (var blueprintType in scanResult.BlueprintTypes)
        {
            if (userExplicitBlueprintTypes.Contains(blueprintType)) continue;
            InstantiateAndApplyBlueprint(blueprintType, inputs.BlueprintBuilder, tolerant: true);
        }

        // Stage 5 — Register attributed pairs for pairs not already configured
        foreach (var scanned in scanResult.AttributedPairs)
        {
            inputs.BlueprintBuilderImpl.RegisterEmpty(scanned.Pair);
        }

        // Stage 6 — Build blueprints (runs inheritance resolution, bidirectional inverse, validation)
        IReadOnlyList<Blueprint> rawBlueprints;
        try
        {
            rawBlueprints = inputs.BlueprintBuilderImpl.Build(validate: false);
        }
        catch (InvalidOperationException ex)
        {
            throw new BlueprintValidationException(ex.Message);
        }

        // Stage 6b — Merge convention-driven links into each blueprint (uses user-configured options).
        var typeModelCache = TypeModelCache.Default;
        var conventionPipeline = BuildConfiguredConventionPipeline(inputs.Options, typeModelCache);

        var mergedBlueprints = new List<Blueprint>(rawBlueprints.Count);
        var explicitlyConfigured = new HashSet<TypePair>();
        foreach (var cfg in inputs.BlueprintBuilderImpl.Bindings)
        {
            if (cfg.PropertyConfigs.Count > 0 || cfg.FactoryExpression is not null)
                explicitlyConfigured.Add(cfg.TypePair);
        }

        foreach (var bp in rawBlueprints)
        {
            var merged = MergeConventionLinks(bp, conventionPipeline, typeModelCache);
            mergedBlueprints.Add(merged);
        }

        // Stage 6c — Materialise composition rules into CompositionBlueprint records (S8-T08).
        // Each FromOrigin<TOrigin>() call's BindingConfiguration is built into a partial Blueprint
        // and convention-merged so FromOrigin<User>() with no explicit .Property(...) calls still
        // auto-maps User's members to target members by name. The resulting CompositionBlueprints
        // live on the forged configuration alongside regular blueprints — they do NOT occupy the
        // (TOrigin, TTarget) pair slot, so a user can still register `Bind<User, Dashboard>()`
        // independently.
        var compositionBlueprints = new List<Composition.CompositionBlueprint>();
        var seenCompositionTargets = new HashSet<Type>();
        foreach (var rule in inputs.BlueprintBuilderImpl.Compositions)
        {
            if (!seenCompositionTargets.Add(rule.TargetType))
            {
                throw new Diagnostics.BlueprintValidationException(
                    $"Multiple composition rules registered for target '{rule.TargetType.FullName}'. " +
                    "Each target type supports exactly one composition; merge origins into a single Compose<T>() call " +
                    "(spec §S8-T08 Constraints: ambiguous match).");
            }

            var origins = new List<Composition.CompositionOrigin>(rule.Origins.Count);
            foreach (var (originType, originConfig) in rule.Origins)
            {
                // Spec §S8-T08 Technical Considerations bullet 3: "Reserve the per-origin
                // .Transform, .When, .OnlyIf surfaces for Sprint 15 — stubs throw
                // NotSupportedException for clarity." The FromOrigin callback accepts the full
                // IBindingRule<TOrigin, TTarget> surface (to preserve API continuity), so we
                // guard at forge time by inspecting the accumulated configuration and throwing
                // with a clear sprint-deferred message when any reserved feature is found.
                ValidateCompositionOriginConfig(rule.TargetType, originType, originConfig);

                var partialRaw = Abstractions.BlueprintBuilder.BuildBlueprintFromConfig(originConfig);
                var partialMerged = MergeConventionLinks(partialRaw, conventionPipeline, typeModelCache);
                origins.Add(new Composition.CompositionOrigin
                {
                    OriginType = originType,
                    PartialBlueprint = partialMerged,
                });
            }

            compositionBlueprints.Add(new Composition.CompositionBlueprint
            {
                TargetType = rule.TargetType,
                Origins = origins,
            });
        }

        // Stage 7 — Instantiate transformer types and populate the registry
        var transformerRegistry = new TypeTransformerRegistry();
        TypeTransformerRegistryDefaults.RegisterDefaults(transformerRegistry);
        RegisterScannedAndConfiguredTransformers(transformerRegistry, inputs.TransformerTypes, scanResult);

        // Stage 7b — Resolve AttributeDeferredTypeTransformer placeholders (attached by
        // AttributeConvention when [TransformWith(typeof(T))] is seen) against the populated
        // registry. Replace them in-place so the compiler's TransformerExpressionHelper sees the
        // concrete typed transformer.
        for (var i = 0; i < mergedBlueprints.Count; i++)
        {
            mergedBlueprints[i] = ResolveDeferredTransformers(mergedBlueprints[i], transformerRegistry, inputs.Options);
        }

        // Stage 8 — Build the compiler, delegate cache, and pre-compile
        var delegateCache = new MappingDelegateCache();
        var blueprintsByPair = new Dictionary<TypePair, Blueprint>();
        foreach (var bp in mergedBlueprints) blueprintsByPair[bp.TypePair] = bp;

        var compiler = new BlueprintCompiler(
            typeModelCache: typeModelCache,
            delegateCache: delegateCache,
            blueprintResolver: pair => blueprintsByPair.TryGetValue(pair, out var b) ? b : null,
            transformerLookup: (o, t) => transformerRegistry.GetTransformer(o, t),
            inheritanceResolver: inputs.BlueprintBuilderImpl.ResolvedInheritanceResolver);

        if (!inputs.Options.Throughput.LazyBlueprintCompilation)
        {
            foreach (var bp in mergedBlueprints)
            {
                _ = delegateCache.GetOrCompile(bp.TypePair, _ => compiler.Compile(bp));
            }
        }

        // Stage 9 — Validate (post-merge so convention links count toward strict-mode coverage)
        if (inputs.Options.ValidateOnStartup)
        {
            var validator = new BlueprintValidator(typeModelCache);
            var validation = validator.Validate(
                mergedBlueprints,
                inputs.BlueprintBuilderImpl.Bindings,
                strictMode: inputs.Options.StrictMode || inputs.Options.ThrowOnUnlinkedMembers);

            if (!validation.IsValid)
                throw new BlueprintValidationException(validation);
        }

        // Stage 10 — Build the forged configuration and return the sculptor
        var config = new ForgedSculptorConfiguration(
            blueprints: mergedBlueprints,
            options: inputs.Options,
            typeModelCache: typeModelCache,
            delegateCache: delegateCache,
            compiler: compiler,
            transformerRegistry: transformerRegistry,
            compositionBlueprints: compositionBlueprints);

        return new Sculptor(config);
    }

    /// <summary>
    /// Builds a <see cref="ConventionPipeline"/> honouring user-supplied
    /// <see cref="Configuration.ConventionOptions"/> — prefixes/suffixes, abbreviation dictionary,
    /// and any custom <see cref="IPropertyConvention"/> implementations the user registered.
    /// Falls back to <see cref="ConventionPipeline.CreateDefault"/> when no configuration is present.
    /// </summary>
    private static ConventionPipeline BuildConfiguredConventionPipeline(
        Configuration.SculptorOptions options,
        TypeModelCache cache)
    {
        var co = options.Conventions;

        var hasUserPrefixes = co.OriginPrefixes.Count > 0;
        var hasUserSuffixes = co.TargetSuffixes.Count > 0;
        var hasUserAbbreviations = co.AbbreviationExpansionEnabled && co.Abbreviations.Count > 0;
        var hasCustomConventions = co.CustomConventions.Count > 0;

        // When nothing is configured, take the fast path and use the default pipeline.
        if (!hasUserPrefixes && !hasUserSuffixes && !hasUserAbbreviations && !hasCustomConventions)
        {
            return ConventionPipeline.CreateDefault(cache);
        }

        // Merge user-supplied prefixes/suffixes with defaults so users accumulate onto existing behaviour
        // rather than replacing it.
        IReadOnlyList<string>? prefixes = hasUserPrefixes
            ? MergeLists(DefaultPrefixDroppingOriginPrefixes, co.OriginPrefixes)
            : null;
        IReadOnlyList<string>? suffixes = hasUserSuffixes
            ? MergeLists(DefaultPrefixDroppingTargetSuffixes, co.TargetSuffixes)
            : null;

        Dictionary<string, string>? abbreviations = null;
        if (hasUserAbbreviations)
        {
            abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in co.Abbreviations) abbreviations[kvp.Key] = kvp.Value;
        }

        var conventions = new List<IPropertyConvention>
        {
            new AttributeConvention(cache),
            new ExactNameConvention(),
            new CaseConvention(),
            new PrefixDroppingConvention(prefixes, suffixes),
            new MethodToPropertyConvention(),
            new FlatteningConvention(cache),
            new UnflatteningConvention(cache),
            new AbbreviationConvention(abbreviations),
        };

        foreach (var custom in co.CustomConventions)
        {
            conventions.Add(custom);
        }

        return new ConventionPipeline(conventions, cache, new StructuralSimilarityScorer());
    }

    // Mirrors PrefixDroppingConvention's built-in defaults so user prefixes stack additively.
    private static readonly IReadOnlyList<string> DefaultPrefixDroppingOriginPrefixes =
        new[] { "Get", "get", "Str", "str", "m_", "_", "M_" };

    private static readonly IReadOnlyList<string> DefaultPrefixDroppingTargetSuffixes =
        new[] { "Field", "Property", "Prop" };

    private static IReadOnlyList<string> MergeLists(IReadOnlyList<string> defaults, IReadOnlyList<string> extras)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<string>(defaults.Count + extras.Count);
        foreach (var item in defaults)
        {
            if (seen.Add(item)) merged.Add(item);
        }
        foreach (var item in extras)
        {
            if (seen.Add(item)) merged.Add(item);
        }
        return merged;
    }

    /// <summary>
    /// Forge-time guard for composition origin configurations — rejects per-origin uses of the
    /// <c>.When</c> / <c>.OnlyIf</c> / <c>.TransformWith</c> surfaces that the Sprint 8 dispatcher
    /// does not honour. Reserved for Sprint 15 per spec §S8-T08 Technical Considerations bullet 3.
    /// </summary>
    private static void ValidateCompositionOriginConfig(Type targetType, Type originType, Abstractions.BindingConfiguration config)
    {
        static NotSupportedException Reject(Type target, Type origin, string feature) => new(
            $"FromOrigin<{origin.Name}>() for composition target '{target.Name}' used the '{feature}' surface, " +
            "which is reserved for Sprint 15. The Sprint 8 composition dispatcher only honours per-origin " +
            ".Property(...) bindings and convention-discovered links. Remove the call or wait for the Sprint 15 " +
            "release (spec §S8-T08 Technical Considerations bullet 3).");

        if (config.Condition is not null)
            throw Reject(targetType, originType, ".When(...)");

        foreach (var prop in config.PropertyConfigs)
        {
            if (prop.PreCondition is not null)
                throw Reject(targetType, originType, ".OnlyIf(...)");
            if (prop.Condition is not null)
                throw Reject(targetType, originType, ".When(...) (property-level)");
            if (prop.TransformerType is not null || prop.InlineTransform is not null)
                throw Reject(targetType, originType, ".TransformWith(...)");
        }
    }

    private static Blueprint ResolveDeferredTransformers(Blueprint blueprint, TypeTransformerRegistry registry, Configuration.SculptorOptions options)
    {
        var needsRewrite = false;
        foreach (var link in blueprint.Links)
        {
            if (link.Transformer is AttributeDeferredTypeTransformer)
            {
                needsRewrite = true;
                break;
            }
        }
        if (!needsRewrite) return blueprint;

        var rewritten = new List<PropertyLink>(blueprint.Links.Count);
        foreach (var link in blueprint.Links)
        {
            if (link.Transformer is not AttributeDeferredTypeTransformer deferred)
            {
                rewritten.Add(link);
                continue;
            }

            // Per spec §11.4 (S8-T04): route transformer instantiation through the configured
            // IProviderResolver so DI-wired containers can satisfy transformer constructor
            // dependencies (e.g. ILogger<T>, registered singletons). Falls back to registry
            // lookup by (origin, target) member types when the declared type cannot be
            // resolved — matches Sprint 7 behaviour.
            var concrete = TryResolveFromDeclaredType(deferred.TransformerType, options.ProviderResolver)
                ?? registry.GetTransformer(OriginMemberType(link), TargetMemberType(link));

            rewritten.Add(link with { Transformer = concrete });
        }

        return blueprint with { Links = rewritten };
    }

    private static ITypeTransformer? TryResolveFromDeclaredType(Type transformerType, IProviderResolver resolver)
    {
        if (transformerType.IsAbstract || transformerType.IsInterface) return null;
        try
        {
            // The resolver's IServiceProvider argument is null here because forge-time
            // transformer resolution has no ambient request scope. The DI package's
            // DependencyInjectionProviderFactory captures the root IServiceProvider internally
            // and uses ActivatorUtilities.CreateInstance so non-default constructors can be
            // satisfied from root-scoped / singleton services. Transformers that require
            // request-scoped dependencies remain unsupported in Sprint 8 (see docs).
            return resolver.Resolve(transformerType, serviceProvider: null) as ITypeTransformer;
        }
        catch
        {
            return null;
        }
    }

    private static Type OriginMemberType(PropertyLink link)
    {
        // Infer origin type from provider — same heuristic as MappingInspection.
        if (link.Provider is Conventions.PropertyAccessProvider pap)
            return MemberType(pap.OriginMember);
        if (link.Provider is Conventions.ChainedPropertyAccessProvider cpap && cpap.Chain.Count > 0)
            return MemberType(cpap.Chain[cpap.Chain.Count - 1]);
        return TargetMemberType(link);
    }

    private static Type TargetMemberType(PropertyLink link) => MemberType(link.TargetMember);

    private static Type MemberType(System.Reflection.MemberInfo m) => m switch
    {
        System.Reflection.PropertyInfo pi => pi.PropertyType,
        System.Reflection.FieldInfo fi => fi.FieldType,
        _ => typeof(object),
    };

    /// <summary>
    /// Instantiates a <see cref="MappingBlueprint"/> and applies its <c>Design</c>. When
    /// <paramref name="tolerant"/> is true, <see cref="InvalidOperationException"/> from duplicate
    /// pair registrations is silently swallowed — intended for scanner-discovered blueprints that
    /// may collide with already-registered pairs.
    /// </summary>
    private static void InstantiateAndApplyBlueprint(
        Type blueprintType,
        IBlueprintBuilder builder,
        bool tolerant)
    {
        if (blueprintType.IsAbstract) return;
        if (blueprintType.GetConstructor(Type.EmptyTypes) is null)
        {
            if (tolerant) return;
            throw new MappingConfigurationException(
                $"MappingBlueprint type '{blueprintType.FullName}' has no parameterless constructor. " +
                "Either add one or register the blueprint via UseBlueprint(instance).");
        }

        var instance = (MappingBlueprint)Activator.CreateInstance(blueprintType)!;
        try
        {
            instance.Design(builder);
        }
        catch (InvalidOperationException) when (tolerant)
        {
            // Scanner-discovered blueprint collides with an already-registered pair — skip silently.
        }
    }

    /// <summary>
    /// Merges convention-derived links into the blueprint, preserving user-explicit configuration.
    /// User-configured target members remain untouched; un-configured members pick up convention
    /// matches. Also marks members with <c>[Unmapped]</c> as explicitly skipped.
    /// </summary>
    private static Blueprint MergeConventionLinks(
        Blueprint blueprint,
        ConventionPipeline pipeline,
        TypeModelCache cache)
    {
        var userLinks = blueprint.Links;

        // Build a case-insensitive set of member names already configured by the user.
        var userMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in userLinks) userMemberNames.Add(l.TargetMember.Name);

        // Run convention pipeline — produces one link per writable target member.
        var originModel = cache.GetOrAdd(blueprint.OriginType);
        var targetModel = cache.GetOrAdd(blueprint.TargetType);
        var conventionLinks = pipeline.BuildLinks(originModel, targetModel);

        var merged = new List<PropertyLink>(userLinks.Count + conventionLinks.Count);

        // Keep user explicit links first
        foreach (var l in userLinks) merged.Add(l);

        // Add convention links only for members the user did not configure
        foreach (var cl in conventionLinks)
        {
            if (userMemberNames.Add(cl.TargetMember.Name))
                merged.Add(cl);
        }

        // Stable order: preserve insertion order then sort by explicit Order
        merged.Sort((a, b) => a.Order.CompareTo(b.Order));

        return blueprint with { Links = merged };
    }

    private static void RegisterScannedAndConfiguredTransformers(
        TypeTransformerRegistry registry,
        IReadOnlyList<Type> configuredTransformerTypes,
        AssemblyScanResult scanResult)
    {
        var seen = new HashSet<Type>();

        foreach (var type in configuredTransformerTypes)
        {
            if (!seen.Add(type)) continue;
            TryRegisterTransformer(registry, type);
        }

        foreach (var scanned in scanResult.TypeTransformers)
        {
            if (!seen.Add(scanned.ImplementationType)) continue;
            TryRegisterTransformer(registry, scanned.ImplementationType);
        }
    }

    private static void TryRegisterTransformer(TypeTransformerRegistry registry, Type type)
    {
        if (type.IsAbstract || type.IsInterface) return;
        if (type.GetConstructor(Type.EmptyTypes) is null) return;

        ITypeTransformer instance;
        try
        {
            instance = (ITypeTransformer)Activator.CreateInstance(type)!;
        }
        catch
        {
            return;
        }

        // Find every closed ITypeTransformer<,> interface and register under it
        var registered = false;
        foreach (var iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType) continue;
            if (iface.GetGenericTypeDefinition() != typeof(ITypeTransformer<,>)) continue;
            var args = iface.GetGenericArguments();
            registry.Register(args[0], args[1], instance);
            registered = true;
        }

        if (!registered)
        {
            registry.RegisterOpen(instance);
        }
    }
}
