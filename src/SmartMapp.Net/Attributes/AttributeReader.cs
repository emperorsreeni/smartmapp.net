using System.Reflection;

namespace SmartMapp.Net.Attributes;

/// <summary>
/// Reads SmartMapp.Net mapping attributes from types and members, collapsing the generic (.NET 7+)
/// and non-generic attribute forms into a uniform reflection surface.
/// </summary>
public static class AttributeReader
{
    /// <summary>
    /// Returns every origin type declared on <paramref name="targetType"/> via
    /// <see cref="MappedByAttribute"/> or its generic form.
    /// </summary>
    /// <param name="targetType">The target type to inspect.</param>
    /// <returns>A list of origin types (possibly empty).</returns>
    public static IReadOnlyList<Type> GetMappedByOriginTypes(Type targetType)
    {
        if (targetType is null) throw new ArgumentNullException(nameof(targetType));

        var results = new List<Type>();
        foreach (var attr in targetType.GetCustomAttributes(inherit: false))
        {
            if (attr is MappedByAttribute mapped)
            {
                results.Add(mapped.OriginType);
                continue;
            }

            var origin = TryReadGenericTypeAttribute(attr, "MappedByAttribute`1");
            if (origin is not null)
                results.Add(origin);
        }
        return results;
    }

    /// <summary>
    /// Returns every target type declared on <paramref name="originType"/> via
    /// <see cref="MapsIntoAttribute"/> or its generic form.
    /// </summary>
    /// <param name="originType">The origin type to inspect.</param>
    /// <returns>A list of target types (possibly empty).</returns>
    public static IReadOnlyList<Type> GetMapsIntoTargetTypes(Type originType)
    {
        if (originType is null) throw new ArgumentNullException(nameof(originType));

        var results = new List<Type>();
        foreach (var attr in originType.GetCustomAttributes(inherit: false))
        {
            if (attr is MapsIntoAttribute into)
            {
                results.Add(into.TargetType);
                continue;
            }

            var target = TryReadGenericTypeAttribute(attr, "MapsIntoAttribute`1");
            if (target is not null)
                results.Add(target);
        }
        return results;
    }

    /// <summary>
    /// Returns the transformer type declared via <see cref="TransformWithAttribute"/> on <paramref name="member"/>,
    /// or <c>null</c> if none is present.
    /// </summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns>The transformer type or <c>null</c>.</returns>
    public static Type? GetTransformerType(MemberInfo member)
    {
        if (member is null) throw new ArgumentNullException(nameof(member));

        foreach (var attr in member.GetCustomAttributes(inherit: false))
        {
            if (attr is TransformWithAttribute t)
                return t.TransformerType;

            var type = TryReadGenericTypeAttribute(attr, "TransformWithAttribute`1");
            if (type is not null)
                return type;
        }
        return null;
    }

    /// <summary>
    /// Returns the provider type declared via <see cref="ProvideWithAttribute"/> on <paramref name="member"/>,
    /// or <c>null</c> if none is present.
    /// </summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns>The provider type or <c>null</c>.</returns>
    public static Type? GetProviderType(MemberInfo member)
    {
        if (member is null) throw new ArgumentNullException(nameof(member));

        foreach (var attr in member.GetCustomAttributes(inherit: false))
        {
            if (attr is ProvideWithAttribute p)
                return p.ProviderType;

            var type = TryReadGenericTypeAttribute(attr, "ProvideWithAttribute`1");
            if (type is not null)
                return type;
        }
        return null;
    }

    /// <summary>
    /// Attempts to extract the single generic argument from a generic attribute instance whose open
    /// generic type is named <paramref name="openTypeName"/> (e.g., <c>"MappedByAttribute`1"</c>).
    /// </summary>
    private static Type? TryReadGenericTypeAttribute(object attribute, string openTypeName)
    {
        var type = attribute.GetType();
        if (!type.IsGenericType) return null;

        var definition = type.GetGenericTypeDefinition();
        if (definition.Name != openTypeName) return null;
        if (definition.Namespace != typeof(AttributeReader).Namespace) return null;

        var args = type.GetGenericArguments();
        return args.Length == 1 ? args[0] : null;
    }
}
