using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// A simple <see cref="IValueProvider"/> that reads a value from a specific member (property or field)
/// on the origin object via reflection. Used by <see cref="BlueprintCompiler"/> when auto-building
/// nested blueprints for type pairs that don't have an explicitly registered blueprint.
/// </summary>
internal sealed class DirectMemberProvider : IValueProvider
{
    private readonly MemberInfo _member;

    /// <summary>
    /// Gets the member this provider reads from.
    /// </summary>
    internal MemberInfo Member => _member;

    /// <summary>
    /// Initializes a new instance of <see cref="DirectMemberProvider"/>.
    /// </summary>
    /// <param name="member">The origin member to read.</param>
    internal DirectMemberProvider(MemberInfo member)
    {
        _member = member;
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        return _member switch
        {
            PropertyInfo pi => pi.GetValue(origin),
            FieldInfo fi => fi.GetValue(origin),
            _ => throw new MappingCompilationException(
                $"Unsupported member type '{_member.GetType().Name}' for member '{_member.Name}'.")
        };
    }
}
