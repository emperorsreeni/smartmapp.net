using System.Reflection;
using System.Runtime.CompilerServices;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;

namespace SmartMapp.Net.Discovery;

/// <summary>
/// Discovers mapping artifacts in one or more assemblies:
/// <see cref="MappingBlueprint"/> subclasses, closed-generic implementations of
/// <see cref="IValueProvider{TOrigin,TTarget,TMember}"/> and
/// <see cref="ITypeTransformer{TOrigin,TTarget}"/>, and attributed type pairs
/// produced by <see cref="MappedByAttribute"/> / <see cref="MapsIntoAttribute"/>.
/// Pure metadata producer — never instantiates discovered types.
/// </summary>
public sealed class AssemblyScanner
{
    private static readonly Type IValueProviderOpenGeneric = typeof(IValueProvider<,,>);
    private static readonly Type ITypeTransformerOpenGeneric = typeof(ITypeTransformer<,>);

    // Per-assembly scan cache (T02 AC). ConditionalWeakTable holds a weak reference to the
    // Assembly key so assemblies unloaded from collectible ALCs don't leak.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Assembly, AssemblyScanResult> SingleAssemblyCache = new();

    /// <summary>
    /// Scans the supplied assemblies and returns an <see cref="AssemblyScanResult"/>.
    /// Results are deterministically ordered by <see cref="Type.FullName"/>.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan. Null or empty returns <see cref="AssemblyScanResult.Empty"/>.</param>
    /// <returns>The accumulated scan result.</returns>
    public AssemblyScanResult Scan(params Assembly[]? assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            return AssemblyScanResult.Empty;

        var dedupedAssemblies = new List<Assembly>(assemblies.Length);
        var seen = new HashSet<Assembly>();
        foreach (var asm in assemblies)
        {
            if (asm is not null && seen.Add(asm))
                dedupedAssemblies.Add(asm);
        }

        // Fast path: single assembly — serve directly from the cache.
        if (dedupedAssemblies.Count == 1)
        {
            return GetOrAddForSingleAssembly(dedupedAssemblies[0]);
        }

        // Multi-assembly path: merge per-assembly cached snapshots so each assembly is reflected at
        // most once even across multiple scan calls.
        var blueprintTypes = new List<Type>();
        var valueProviders = new List<ScannedClosedGeneric>();
        var typeTransformers = new List<ScannedClosedGeneric>();
        var attributedPairs = new List<ScannedTypePair>();

        foreach (var assembly in dedupedAssemblies)
        {
            var perAssembly = GetOrAddForSingleAssembly(assembly);
            blueprintTypes.AddRange(perAssembly.BlueprintTypes);
            valueProviders.AddRange(perAssembly.ValueProviders);
            typeTransformers.AddRange(perAssembly.TypeTransformers);
            attributedPairs.AddRange(perAssembly.AttributedPairs);
        }

        blueprintTypes.Sort(TypeFullNameComparer.Instance);
        valueProviders.Sort((a, b) => TypeFullNameComparer.Instance.Compare(a.ImplementationType, b.ImplementationType));
        typeTransformers.Sort((a, b) => TypeFullNameComparer.Instance.Compare(a.ImplementationType, b.ImplementationType));
        attributedPairs.Sort(ScannedTypePairComparer.Instance);

        return new AssemblyScanResult
        {
            ScannedAssemblies = dedupedAssemblies,
            BlueprintTypes = blueprintTypes,
            ValueProviders = valueProviders,
            TypeTransformers = typeTransformers,
            AttributedPairs = attributedPairs,
        };
    }

    private static AssemblyScanResult GetOrAddForSingleAssembly(Assembly assembly)
    {
        if (SingleAssemblyCache.TryGetValue(assembly, out var cached))
            return cached;

        var blueprintTypes = new List<Type>();
        var valueProviders = new List<ScannedClosedGeneric>();
        var typeTransformers = new List<ScannedClosedGeneric>();
        var attributedPairs = new List<ScannedTypePair>();

        foreach (var type in GetLoadableTypes(assembly))
        {
            if (!IsConsiderable(type)) continue;
            ClassifyType(type, blueprintTypes, valueProviders, typeTransformers, attributedPairs);
        }

        blueprintTypes.Sort(TypeFullNameComparer.Instance);
        valueProviders.Sort((a, b) => TypeFullNameComparer.Instance.Compare(a.ImplementationType, b.ImplementationType));
        typeTransformers.Sort((a, b) => TypeFullNameComparer.Instance.Compare(a.ImplementationType, b.ImplementationType));
        attributedPairs.Sort(ScannedTypePairComparer.Instance);

        var result = new AssemblyScanResult
        {
            ScannedAssemblies = new[] { assembly },
            BlueprintTypes = blueprintTypes,
            ValueProviders = valueProviders,
            TypeTransformers = typeTransformers,
            AttributedPairs = attributedPairs,
        };

        // GetValue-with-factory would be racier (factory may run twice) but idempotent; using
        // AddOrUpdate-style shim: atomic create-if-missing.
        SingleAssemblyCache.AddOrUpdate(assembly, result);
        return result;
    }

    /// <summary>
    /// Scans assemblies identified by marker types — equivalent to
    /// <see cref="Scan(Assembly[])"/> with <c>typeof(T).Assembly</c>.
    /// </summary>
    /// <param name="markerTypes">Types whose assemblies should be scanned.</param>
    /// <returns>The accumulated scan result.</returns>
    public AssemblyScanResult ScanContaining(params Type[] markerTypes)
    {
        if (markerTypes is null || markerTypes.Length == 0)
            return AssemblyScanResult.Empty;

        var assemblies = new Assembly[markerTypes.Length];
        for (var i = 0; i < markerTypes.Length; i++)
        {
            assemblies[i] = markerTypes[i].Assembly;
        }
        return Scan(assemblies);
    }

    private static void ClassifyType(
        Type type,
        List<Type> blueprintTypes,
        List<ScannedClosedGeneric> valueProviders,
        List<ScannedClosedGeneric> typeTransformers,
        List<ScannedTypePair> attributedPairs)
    {
        // Blueprints
        if (typeof(MappingBlueprint).IsAssignableFrom(type))
        {
            blueprintTypes.Add(type);
        }

        // Closed-generic interface implementations
        var interfaces = SafeGetInterfaces(type);
        for (var i = 0; i < interfaces.Length; i++)
        {
            var iface = interfaces[i];
            if (!iface.IsGenericType) continue;

            var def = iface.GetGenericTypeDefinition();
            if (def == IValueProviderOpenGeneric)
            {
                valueProviders.Add(new ScannedClosedGeneric
                {
                    ImplementationType = type,
                    ClosedInterface = iface,
                    GenericArguments = iface.GetGenericArguments(),
                });
            }
            else if (def == ITypeTransformerOpenGeneric)
            {
                typeTransformers.Add(new ScannedClosedGeneric
                {
                    ImplementationType = type,
                    ClosedInterface = iface,
                    GenericArguments = iface.GetGenericArguments(),
                });
            }
        }

        // Attributed pairs
        foreach (var originType in AttributeReader.GetMappedByOriginTypes(type))
        {
            attributedPairs.Add(new ScannedTypePair
            {
                OriginType = originType,
                TargetType = type,
                Source = AttributeSource.MappedBy,
            });
        }

        foreach (var targetType in AttributeReader.GetMapsIntoTargetTypes(type))
        {
            attributedPairs.Add(new ScannedTypePair
            {
                OriginType = type,
                TargetType = targetType,
                Source = AttributeSource.MapsInto,
            });
        }
    }

    private static bool IsConsiderable(Type type)
    {
        if (type.IsAbstract) return false;
        if (type.IsInterface) return false;
        if (type.IsGenericTypeDefinition) return false;
        // Skip non-publicly-visible types (e.g., private nested test fixtures).
        // IsVisible is true only if the type and every containing type are public.
        if (!type.IsVisible) return false;
        if (type.Name.StartsWith("<", StringComparison.Ordinal)) return false; // compiler-generated
        if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) return false;
        return true;
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var types = ex.Types;
            var loadable = new List<Type>(types.Length);
            for (var i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (t is not null) loadable.Add(t);
            }
            return loadable.ToArray();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static Type[] SafeGetInterfaces(Type type)
    {
        try
        {
            return type.GetInterfaces();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private sealed class TypeFullNameComparer : IComparer<Type>
    {
        internal static readonly TypeFullNameComparer Instance = new();
        public int Compare(Type? x, Type? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return string.Compare(x.FullName, y.FullName, StringComparison.Ordinal);
        }
    }

    private sealed class ScannedTypePairComparer : IComparer<ScannedTypePair>
    {
        internal static readonly ScannedTypePairComparer Instance = new();
        public int Compare(ScannedTypePair? x, ScannedTypePair? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var originCmp = string.Compare(x.OriginType.FullName, y.OriginType.FullName, StringComparison.Ordinal);
            if (originCmp != 0) return originCmp;
            var targetCmp = string.Compare(x.TargetType.FullName, y.TargetType.FullName, StringComparison.Ordinal);
            if (targetCmp != 0) return targetCmp;
            return x.Source.CompareTo(y.Source);
        }
    }
}
