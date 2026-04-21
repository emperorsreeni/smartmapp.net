namespace SmartMapp.Net.Attributes;

/// <summary>
/// Declares that the decorated target member is populated by the specified
/// <see cref="Abstractions.IValueProvider"/> implementation.
/// The provider is resolved from the current <see cref="MappingScope.ServiceProvider"/> at mapping time.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ProvideWithAttribute : Attribute
{
    /// <summary>
    /// Gets the provider type implementing <see cref="Abstractions.IValueProvider"/>.
    /// </summary>
    public Type ProviderType { get; }

    /// <summary>
    /// Initializes a new <see cref="ProvideWithAttribute"/>.
    /// </summary>
    /// <param name="providerType">The value provider type.</param>
    public ProvideWithAttribute(Type providerType)
    {
        ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
    }
}

#if NET7_0_OR_GREATER
/// <summary>
/// Generic form of <see cref="ProvideWithAttribute"/> — preferred on .NET 7+ for type-safe provider declaration.
/// </summary>
/// <typeparam name="TProvider">The provider type implementing <see cref="Abstractions.IValueProvider"/>.</typeparam>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ProvideWithAttribute<TProvider> : Attribute
    where TProvider : Abstractions.IValueProvider
{
    /// <summary>
    /// Gets the provider type.
    /// </summary>
    public Type ProviderType => typeof(TProvider);
}
#endif
