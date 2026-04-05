namespace SmartMapp.Net.Compilation;

/// <summary>
/// Defines how the expression compiler constructs target object instances.
/// </summary>
internal enum ConstructionStrategy
{
    /// <summary>
    /// Target type has a public parameterless constructor.
    /// </summary>
    Parameterless,

    /// <summary>
    /// Target type has a primary constructor (record positional ctor or single ctor).
    /// </summary>
    PrimaryConstructor,

    /// <summary>
    /// Target type has multiple constructors — the best-scoring one is selected.
    /// </summary>
    BestMatchConstructor,

    /// <summary>
    /// A user-supplied factory function is used via <c>Blueprint.TargetFactory</c>.
    /// </summary>
    Factory,

    /// <summary>
    /// Target type is an interface or abstract class — resolved via
    /// <c>InterfaceMaterializer</c> using <c>Materialize&lt;T&gt;()</c> or <c>DispatchProxy</c>.
    /// </summary>
    Materialized,
}
