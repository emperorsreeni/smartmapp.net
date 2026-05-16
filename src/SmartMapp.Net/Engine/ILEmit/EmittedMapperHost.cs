namespace SmartMapp.Net.Engine.ILEmit;

/// <summary>
/// Owner type passed to <c>DynamicMethod</c> for every IL-emitted mapping delegate produced by
/// <see cref="ILEmitMappingCompiler"/>. Using a dedicated, sealed, internal host type bounds the
/// reflection-visibility of every emitted method (<c>skipVisibility: true</c> applies relative to
/// this module) and gives integration tests a stable <see cref="System.Reflection.Module"/>
/// reference for assertions (spec §S9-T01 Acceptance bullet 4).
/// </summary>
/// <remarks>
/// The class is intentionally empty: it never holds state and is never instantiated. It exists
/// purely so the runtime has a concrete <see cref="System.Type"/> to root each
/// <c>DynamicMethod</c> against. Storage for nested-delegate and transformer slots lives on the
/// closed-generic helper types in <see cref="Internal"/> so emitted IL can reach those slots via
/// a single <c>Ldsfld</c>.
/// </remarks>
internal static class EmittedMapperHost
{
    /// <summary>
    /// The static module reference used as the owner module for every <c>DynamicMethod</c>
    /// emitted by <see cref="ILEmitMappingCompiler"/>. Exposed for diagnostics and tests.
    /// </summary>
    internal static System.Reflection.Module HostModule { get; } = typeof(EmittedMapperHost).Module;
}
