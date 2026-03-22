using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Validates that all <c>required</c> members on a target type have corresponding
/// <see cref="PropertyLink"/> entries in the <see cref="Blueprint"/>.
/// On frameworks prior to .NET 7, this validator is a no-op since the <c>required</c>
/// keyword is not supported at runtime.
/// </summary>
internal static class RequiredMemberValidator
{
    /// <summary>
    /// Validates that all required members on the target type are covered by the blueprint's links
    /// or by constructor parameters.
    /// </summary>
    /// <param name="targetModel">The target type model.</param>
    /// <param name="blueprint">The mapping blueprint.</param>
    /// <param name="consumedByConstructor">Member names consumed by the constructor.</param>
    /// <param name="strict">
    /// If <c>true</c>, throws <see cref="MappingCompilationException"/> for missing required members.
    /// If <c>false</c>, missing required members are silently ignored.
    /// </param>
    /// <returns>A list of required member names that are NOT covered, for diagnostic purposes.</returns>
    internal static IReadOnlyList<string> Validate(
        TypeModel targetModel,
        Blueprint blueprint,
        HashSet<string> consumedByConstructor,
        bool strict)
    {
#if NET7_0_OR_GREATER
        var missing = new List<string>();

        foreach (var member in targetModel.WritableMembers)
        {
            if (!member.IsRequired)
                continue;

            // Check if covered by constructor
            if (consumedByConstructor.Contains(member.Name))
                continue;

            // Check if covered by a non-skipped property link
            var hasLink = false;
            foreach (var link in blueprint.Links)
            {
                if (!link.IsSkipped && string.Equals(link.TargetMember.Name, member.Name, StringComparison.OrdinalIgnoreCase))
                {
                    hasLink = true;
                    break;
                }
            }

            if (!hasLink)
            {
                missing.Add(member.Name);
            }
        }

        if (missing.Count > 0 && strict)
        {
            throw new MappingCompilationException(
                $"Required member(s) on '{targetModel.ClrType.Name}' are not mapped: {string.Join(", ", missing)}. " +
                $"Add PropertyLink entries or constructor parameters for these members, or disable strict mode.",
                new TypePair(blueprint.OriginType, blueprint.TargetType));
        }

        return missing;
#else
        // required keyword not supported on netstandard2.1 / net6.0 — no-op
        return Array.Empty<string>();
#endif
    }
}
