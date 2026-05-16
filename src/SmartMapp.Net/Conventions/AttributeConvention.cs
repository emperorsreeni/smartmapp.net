using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Convention that reads SmartMapp.Net mapping attributes (<see cref="LinkedFromAttribute"/>,
/// <see cref="UnmappedAttribute"/>, <see cref="ProvideWithAttribute"/>, <see cref="LinksToAttribute"/>)
/// and translates them into <see cref="IValueProvider"/> instances — ensuring attribute-driven
/// configuration overrides every name-based convention.
/// <para>
/// Registered with the lowest <see cref="Priority"/> value so it runs before all other conventions.
/// </para>
/// </summary>
public sealed class AttributeConvention : IPropertyConvention
{
    /// <summary>
    /// Sentinel <see cref="IValueProvider"/> returned when a target member is decorated with
    /// <see cref="UnmappedAttribute"/>. Recognized by <see cref="ConventionPipeline"/> which
    /// produces a skipped <see cref="PropertyLink"/>.
    /// </summary>
    public static readonly IValueProvider UnmappedMarker = new UnmappedSentinel();

    private readonly TypeModelCache _cache;

    /// <summary>
    /// Initializes a new <see cref="AttributeConvention"/>.
    /// </summary>
    /// <param name="cache">The type model cache used for dotted <c>[LinkedFrom]</c> path resolution.</param>
    public AttributeConvention(TypeModelCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public int Priority => 50;

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        provider = null;

        var unmapped = targetMember.GetCustomAttribute<UnmappedAttribute>(inherit: false);
        var linkedFrom = targetMember.GetCustomAttribute<LinkedFromAttribute>(inherit: false);
        var provideWith = AttributeReader.GetProviderType(targetMember);

        if (unmapped is not null && (linkedFrom is not null || provideWith is not null))
        {
            throw new InvalidOperationException(
                $"Conflicting attributes on '{targetMember.DeclaringType?.Name}.{targetMember.Name}': " +
                "[Unmapped] cannot be combined with [LinkedFrom] or [ProvideWith].");
        }

        if (unmapped is not null)
        {
            provider = UnmappedMarker;
            return true;
        }

        if (provideWith is not null)
        {
            provider = new AttributeDeferredValueProvider(provideWith);
            return true;
        }

        if (linkedFrom is not null)
        {
            provider = ResolveLinkedFrom(linkedFrom.OriginMemberName, originModel);
            if (provider is null)
            {
                throw new InvalidOperationException(
                    $"[LinkedFrom(\"{linkedFrom.OriginMemberName}\")] on " +
                    $"'{targetMember.DeclaringType?.Name}.{targetMember.Name}' could not be resolved: " +
                    $"origin type '{originModel.ClrType.Name}' has no member matching that path.");
            }
            return true;
        }

        // Fall back: honour reverse hints via [LinksTo] on origin members. When an origin member
        // declares [LinksTo("TargetMemberName")] matching this target member, we emit a direct link.
        foreach (var originMember in originModel.ReadableMembers)
        {
            var linksTo = originMember.MemberInfo.GetCustomAttributes<LinksToAttribute>(inherit: false);
            foreach (var lt in linksTo)
            {
                if (string.Equals(lt.TargetMemberName, targetMember.Name, StringComparison.OrdinalIgnoreCase))
                {
                    provider = new PropertyAccessProvider(originMember.MemberInfo);
                    return true;
                }
            }
        }

        return false;
    }

    private IValueProvider? ResolveLinkedFrom(string path, TypeModel originModel)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        if (!path.Contains('.'))
        {
            var member = originModel.GetMember(path);
            return member is null ? null : new PropertyAccessProvider(member.MemberInfo, path);
        }

        // Dotted path: walk each segment explicitly through type models
        var parts = path.Split('.');
        var chain = new List<MemberInfo>(parts.Length);
        var currentModel = originModel;
        for (var i = 0; i < parts.Length; i++)
        {
            var member = currentModel.GetMember(parts[i]);
            if (member is null) return null;
            chain.Add(member.MemberInfo);
            if (i < parts.Length - 1)
            {
                currentModel = _cache.GetOrAdd(member.MemberType);
            }
        }

        return new ChainedPropertyAccessProvider(chain, path);
    }

    /// <summary>
    /// Sentinel provider used to indicate <c>[Unmapped]</c>.
    /// </summary>
    private sealed class UnmappedSentinel : IValueProvider
    {
        public object? Provide(object origin, object target, string targetMemberName, MappingScope scope) => null;
        public override string ToString() => "UnmappedMarker";
    }
}

/// <summary>
/// Internal provider placeholder for <c>[ProvideWith]</c>-declared value providers.
/// Resolved from <see cref="MappingScope.ServiceProvider"/> at mapping time.
/// </summary>
internal sealed class AttributeDeferredValueProvider : IValueProvider
{
    internal Type ProviderType { get; }

    internal AttributeDeferredValueProvider(Type providerType)
    {
        ProviderType = providerType;
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        // Per spec §11.4 (S8-T04): same resolution path as DeferredValueProvider — route through
        // the scope's IProviderResolver so DI-registered providers are injected, with Activator
        // fallback for builder-only / non-DI contexts.
        var instance = scope.ProviderResolver.Resolve(ProviderType, scope.ServiceProvider);
        if (instance is not IValueProvider resolved)
        {
            throw new InvalidOperationException(
                $"Type '{ProviderType.FullName}' declared via [ProvideWith] was resolved but does not implement IValueProvider.");
        }

        return resolved.Provide(origin, target, targetMemberName, scope);
    }

    /// <inheritdoc />
    public override string ToString() => $"DeferredProvider({ProviderType.Name})";
}
