namespace SmartMapp.Net.Attributes;

/// <summary>
/// Declares that the decorated type is the target of an auto-mapping from the specified origin type.
/// The <c>AssemblyScanner</c> picks these up and registers
/// <c>(OriginType, AttributedType)</c> as a mappable pair without any fluent configuration.
/// <para>
/// Example:
/// <code>
/// [MappedBy(typeof(Order))]
/// public record OrderDto { public int Id { get; init; } }
/// </code>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
public sealed class MappedByAttribute : Attribute
{
    /// <summary>
    /// Gets the origin type this attributed type is mapped from.
    /// </summary>
    public Type OriginType { get; }

    /// <summary>
    /// Initializes a new <see cref="MappedByAttribute"/> for the specified origin type.
    /// </summary>
    /// <param name="originType">The origin type.</param>
    public MappedByAttribute(Type originType)
    {
        OriginType = originType ?? throw new ArgumentNullException(nameof(originType));
    }
}

#if NET7_0_OR_GREATER
/// <summary>
/// Generic form of <see cref="MappedByAttribute"/> — preferred on .NET 7+ for type-safe origin declaration.
/// Usage: <c>[MappedBy&lt;Order&gt;]</c>.
/// </summary>
/// <typeparam name="TOrigin">The origin type.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
public sealed class MappedByAttribute<TOrigin> : Attribute
{
    /// <summary>
    /// Gets the origin type this attributed type is mapped from.
    /// </summary>
    public Type OriginType => typeof(TOrigin);
}
#endif
