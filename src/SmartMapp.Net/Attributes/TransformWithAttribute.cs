namespace SmartMapp.Net.Attributes;

/// <summary>
/// Declares that values flowing into the decorated target member are transformed by the specified
/// <see cref="Abstractions.ITypeTransformer"/> implementation. Resolved from DI first, then via
/// <c>Activator.CreateInstance</c> as a fallback.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class TransformWithAttribute : Attribute
{
    /// <summary>
    /// Gets the transformer type implementing <see cref="Abstractions.ITypeTransformer"/>.
    /// </summary>
    public Type TransformerType { get; }

    /// <summary>
    /// Initializes a new <see cref="TransformWithAttribute"/>.
    /// </summary>
    /// <param name="transformerType">The transformer type.</param>
    public TransformWithAttribute(Type transformerType)
    {
        TransformerType = transformerType ?? throw new ArgumentNullException(nameof(transformerType));
    }
}

#if NET7_0_OR_GREATER
/// <summary>
/// Generic form of <see cref="TransformWithAttribute"/> — preferred on .NET 7+ for type-safe transformer declaration.
/// </summary>
/// <typeparam name="TTransformer">The transformer type implementing <see cref="Abstractions.ITypeTransformer"/>.</typeparam>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class TransformWithAttribute<TTransformer> : Attribute
    where TTransformer : Abstractions.ITypeTransformer
{
    /// <summary>
    /// Gets the transformer type.
    /// </summary>
    public Type TransformerType => typeof(TTransformer);
}
#endif
