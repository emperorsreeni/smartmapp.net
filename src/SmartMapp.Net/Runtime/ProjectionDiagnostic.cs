namespace SmartMapp.Net.Runtime;

/// <summary>
/// Diagnostic record emitted by <see cref="SculptorProjectionBuilder"/> when a
/// <see cref="PropertyLink"/> cannot be translated to a pure LINQ expression suitable for
/// <see cref="System.Linq.IQueryable"/> providers (e.g., EF Core). The affected member is
/// skipped in the generated projection — it assumes the target member's default value —
/// and the diagnostic is stashed on
/// <c>ForgedSculptorConfiguration.ProjectionDiagnostics</c> for downstream inspection.
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Sprint 8 · S8-T06 per spec §8.10. The Sprint 18 insights endpoint will surface
/// these diagnostics to developers so they can see at a glance which members fell off the EF
/// translation path.
/// </para>
/// </remarks>
public sealed record ProjectionDiagnostic
{
    /// <summary>
    /// Gets the <see cref="TypePair"/> whose projection triggered the diagnostic.
    /// </summary>
    public required TypePair Pair { get; init; }

    /// <summary>
    /// Gets the name of the target member that could not be translated.
    /// </summary>
    public required string TargetMemberName { get; init; }

    /// <summary>
    /// Gets the human-readable reason (e.g., "custom IValueProvider is not EF-translatable").
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Gets the severity of the diagnostic. Projection always produces
    /// <see cref="ProjectionDiagnosticSeverity.Warning"/> in Sprint 8 — errors are reserved for
    /// future tightening via <see cref="Configuration.SculptorOptions.StrictMode"/>.
    /// </summary>
    public ProjectionDiagnosticSeverity Severity { get; init; } = ProjectionDiagnosticSeverity.Warning;

    /// <inheritdoc />
    public override string ToString() =>
        $"[{Severity}] {Pair.OriginType.Name} -> {Pair.TargetType.Name}.{TargetMemberName}: {Reason}";
}

/// <summary>
/// Severity level for a <see cref="ProjectionDiagnostic"/>.
/// </summary>
public enum ProjectionDiagnosticSeverity
{
    /// <summary>A warning; projection still succeeds but the affected member uses its default value.</summary>
    Warning,

    /// <summary>Reserved for future use; currently not emitted by <see cref="SculptorProjectionBuilder"/>.</summary>
    Error,
}
