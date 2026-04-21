using System.Text.Json.Serialization;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// A node in the <see cref="MappingAtlas"/> graph — representing a single CLR type that
/// participates in at least one registered mapping.
/// </summary>
public sealed record MappingAtlasNode
{
    /// <summary>Gets the CLR type this node represents. Excluded from JSON serialization
    /// because <see cref="System.Type"/> is not JSON-serializable; use
    /// <see cref="ClrTypeFullName"/> for the equivalent string surface.
    /// Always initialised by <see cref="MappingAtlas.Build"/>.</summary>
    [JsonIgnore]
    public Type ClrType { get; init; } = null!;

    /// <summary>Gets the human-readable type label (e.g., <c>"List&lt;Order&gt;"</c>).</summary>
    public required string Label { get; init; }

    /// <summary>Gets the <see cref="Type.FullName"/> of <see cref="ClrType"/> for JSON output.</summary>
    public string ClrTypeFullName => ClrType.FullName ?? ClrType.Name;

    /// <summary>Gets the assembly-qualified name of <see cref="ClrType"/> for JSON output.</summary>
    public string AssemblyQualifiedName => ClrType.AssemblyQualifiedName ?? ClrType.FullName ?? ClrType.Name;
}
