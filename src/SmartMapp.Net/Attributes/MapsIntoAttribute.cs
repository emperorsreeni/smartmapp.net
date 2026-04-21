namespace SmartMapp.Net.Attributes;

/// <summary>
/// Declares that the decorated type auto-maps into the specified target type.
/// The <c>AssemblyScanner</c> picks these up and registers
/// <c>(AttributedType, TargetType)</c> as a mappable pair without any fluent configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
public sealed class MapsIntoAttribute : Attribute
{
    /// <summary>
    /// Gets the target type this attributed type is mapped into.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// Initializes a new <see cref="MapsIntoAttribute"/> for the specified target type.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    public MapsIntoAttribute(Type targetType)
    {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }
}

#if NET7_0_OR_GREATER
/// <summary>
/// Generic form of <see cref="MapsIntoAttribute"/> — preferred on .NET 7+ for type-safe target declaration.
/// Usage: <c>[MapsInto&lt;OrderDto&gt;]</c>.
/// </summary>
/// <typeparam name="TTarget">The target type.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = true)]
public sealed class MapsIntoAttribute<TTarget> : Attribute
{
    /// <summary>
    /// Gets the target type this attributed type is mapped into.
    /// </summary>
    public Type TargetType => typeof(TTarget);
}
#endif
