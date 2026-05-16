namespace SmartMapp.Net.Attributes;

/// <summary>
/// Overrides convention-based property linking by specifying the origin member name
/// (supports dotted paths like <c>"Customer.FirstName"</c>) for the decorated target member.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class LinkedFromAttribute : Attribute
{
    /// <summary>
    /// Gets the origin member name or dotted path.
    /// </summary>
    public string OriginMemberName { get; }

    /// <summary>
    /// Gets or sets an optional transform descriptor (e.g., <c>"Sum(Quantity * UnitPrice)"</c>).
    /// Interpreted by downstream conventions when present.
    /// </summary>
    public string? Transform { get; set; }

    /// <summary>
    /// Initializes a new <see cref="LinkedFromAttribute"/>.
    /// </summary>
    /// <param name="originMemberName">The origin member name or dotted path.</param>
    public LinkedFromAttribute(string originMemberName)
    {
        if (string.IsNullOrWhiteSpace(originMemberName))
            throw new ArgumentException("Origin member name cannot be null or whitespace.", nameof(originMemberName));
        OriginMemberName = originMemberName;
    }
}
