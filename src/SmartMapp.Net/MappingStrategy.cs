using System.ComponentModel;

namespace SmartMapp.Net;

/// <summary>
/// Defines the code generation approach used for a mapping delegate.
/// </summary>
public enum MappingStrategy
{
    /// <summary>
    /// Default cold-path: compile <c>Expression&lt;Func&lt;&gt;&gt;</c> at first use.
    /// Every mapping starts here before potential promotion.
    /// </summary>
    [Description("Expression tree compiled at first use")]
    ExpressionCompiled = 0,

    /// <summary>
    /// Hot-path: <c>DynamicMethod</c> emitted after adaptive promotion threshold.
    /// Fastest execution, promoted automatically from <see cref="ExpressionCompiled"/>.
    /// </summary>
    [Description("IL emitted via DynamicMethod after promotion threshold")]
    ILEmit = 1,

    /// <summary>
    /// Build-time: code emitted by Roslyn source generator. AOT-safe and trimmer-safe.
    /// </summary>
    [Description("Code emitted at compile time by source generator")]
    SourceGenerated = 2,

    /// <summary>
    /// Debug mode: step-through traceability with slowest execution.
    /// Useful for diagnostics and troubleshooting mapping behavior.
    /// </summary>
    [Description("Interpreted execution for debugging")]
    Interpreted = 3,
}
