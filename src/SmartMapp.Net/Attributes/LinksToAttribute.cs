namespace SmartMapp.Net.Attributes;

/// <summary>
/// Declares that the decorated origin member should be linked to the specified target member name
/// even when the convention pipeline would produce a different match. Complements <see cref="LinkedFromAttribute"/>
/// on the origin side.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public sealed class LinksToAttribute : Attribute
{
    /// <summary>
    /// Gets the target member name (flat, not dotted).
    /// </summary>
    public string TargetMemberName { get; }

    /// <summary>
    /// Initializes a new <see cref="LinksToAttribute"/>.
    /// </summary>
    /// <param name="targetMemberName">The target member name.</param>
    public LinksToAttribute(string targetMemberName)
    {
        if (string.IsNullOrWhiteSpace(targetMemberName))
            throw new ArgumentException("Target member name cannot be null or whitespace.", nameof(targetMemberName));
        TargetMemberName = targetMemberName;
    }
}
