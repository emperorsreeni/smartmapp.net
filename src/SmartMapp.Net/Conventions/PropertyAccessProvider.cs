using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// A value provider that reads a single property or field from the origin object
/// using a compiled accessor delegate for high performance.
/// </summary>
public sealed class PropertyAccessProvider : IValueProvider
{
    private readonly Func<object, object?> _accessor;

    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.MemberInfo"/> being accessed.
    /// </summary>
    public MemberInfo OriginMember { get; }

    /// <summary>
    /// Gets the dotted member path (e.g., <c>"Name"</c> for a simple property).
    /// </summary>
    public string MemberPath { get; }

    /// <summary>
    /// Initializes a new <see cref="PropertyAccessProvider"/> for the specified member.
    /// </summary>
    /// <param name="originMember">The property or field to read from the origin.</param>
    /// <param name="memberPath">Optional dotted path override. Defaults to the member name.</param>
    public PropertyAccessProvider(MemberInfo originMember, string? memberPath = null)
    {
        OriginMember = originMember;
        MemberPath = memberPath ?? originMember.Name;
        _accessor = BuildAccessor(originMember);
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        return _accessor(origin);
    }

    /// <inheritdoc />
    public override string ToString() => $"PropertyAccess({MemberPath})";

    private static Func<object, object?> BuildAccessor(MemberInfo member)
    {
        // origin => (object?)((OriginType)origin).MemberName
        var param = Expression.Parameter(typeof(object), "origin");
        var declaringType = member.DeclaringType ?? throw new InvalidOperationException(
            $"Member '{member.Name}' has no declaring type.");

        var cast = Expression.Convert(param, declaringType);
        var access = Expression.MakeMemberAccess(cast, member);
        var boxed = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
    }
}
