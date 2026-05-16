using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// A single line in a <see cref="MappingInspection"/> — one per <see cref="PropertyLink"/>.
/// </summary>
public sealed record MappingInspectionLine
{
    /// <summary>Gets the target member being populated.</summary>
    public required MemberInfo TargetMember { get; init; }

    /// <summary>Gets the origin expression/path string (or empty for custom providers).</summary>
    public required string OriginPath { get; init; }

    /// <summary>Gets the convention / attribute source that produced this link.</summary>
    public required string Source { get; init; }

    /// <summary>Gets a value indicating whether the link is an explicit skip.</summary>
    public bool IsSkipped { get; init; }

    /// <summary>Gets the attached transformer, when present.</summary>
    public ITypeTransformer? Transformer { get; init; }

    /// <summary>
    /// Gets the origin type for a nested complex-type mapping, when the target member's type
    /// is itself a complex object that maps from a nested origin type. <c>null</c> for flat links.
    /// </summary>
    public Type? NestedOriginType { get; init; }

    /// <summary>
    /// Gets the target complex type for a nested mapping. <c>null</c> for flat links.
    /// </summary>
    public Type? NestedTargetType { get; init; }

    /// <summary>Renders the line in spec §12.2 format.</summary>
    public override string ToString()
    {
        if (IsSkipped)
            return $"  {TargetMember.Name} -> [SKIPPED]";

        var nestedSuffix = (NestedOriginType is not null && NestedTargetType is not null)
            ? $" (NestedMapping: {NestedOriginType.Name} -> {NestedTargetType.Name})"
            : string.Empty;

        var transformerSuffix = Transformer is not null ? $", {Transformer}" : string.Empty;
        var originText = string.IsNullOrEmpty(OriginPath) ? Source : OriginPath;
        return $"  {originText} -> {TargetMember.Name} ({Source}{transformerSuffix}){nestedSuffix}";
    }
}
