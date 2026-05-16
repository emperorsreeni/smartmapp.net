namespace SmartMapp.Net.Discovery;

/// <summary>
/// A concrete type that closes a given open generic interface, with the captured
/// closed generic arguments cached to avoid repeated reflection.
/// </summary>
public sealed record ScannedClosedGeneric
{
    /// <summary>
    /// Gets the concrete type implementing the interface.
    /// </summary>
    public required Type ImplementationType { get; init; }

    /// <summary>
    /// Gets the closed generic interface (e.g., <c>IValueProvider&lt;Order, OrderDto, string&gt;</c>).
    /// </summary>
    public required Type ClosedInterface { get; init; }

    /// <summary>
    /// Gets the generic type arguments of <see cref="ClosedInterface"/>.
    /// </summary>
    public required IReadOnlyList<Type> GenericArguments { get; init; }
}
