using System.Diagnostics.CodeAnalysis;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.Engine.ILEmit;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.Engine;

/// <summary>
/// Sprint 9 strategy chain. Dispatches each <see cref="Blueprint"/> compilation through the
/// ladder configured on <see cref="StrategyOptions.Mode"/>:
/// <c>CompiledOnly</c> → existing <see cref="Compilation.BlueprintCompiler"/>;
/// <c>EmitFirst</c> → try <see cref="ILEmitMappingCompiler"/> then fall back;
/// <c>Adaptive</c> → start Compiled, hand the pair to the promotion manager (T07) on first compile;
/// <c>EmitOnly</c> → require IL Emit, throw on rejection.
/// </summary>
internal sealed class MappingStrategySelector
{
    private readonly ForgedSculptorConfiguration _config;

    internal MappingStrategySelector(ForgedSculptorConfiguration config)
        => _config = config;

    /// <summary>
    /// Compiles <paramref name="blueprint"/> using the strategy ladder. Records the chosen
    /// <see cref="MappingStrategy"/> on <see cref="ForgedSculptorConfiguration.ActiveStrategies"/>
    /// so <see cref="Diagnostics.MappingInspection"/> and telemetry counters can surface the
    /// actual code-generation path.
    /// </summary>
    internal Func<object, MappingScope, object> Compile(Blueprint blueprint)
    {
        var mode = _config.Options.Strategy.Mode;
        var pair = blueprint.TypePair;

        switch (mode)
        {
            case StrategyMode.CompiledOnly:
                return CompileExpression(blueprint, recordStrategy: MappingStrategy.ExpressionCompiled);

            case StrategyMode.EmitFirst:
                {
                    var emitted = TryCompileEmit(blueprint);
                    if (emitted is not null)
                    {
                        _config.ActiveStrategies[pair] = MappingStrategy.ILEmit;
                        return emitted;
                    }
                    return CompileExpression(blueprint, recordStrategy: MappingStrategy.ExpressionCompiled);
                }

            case StrategyMode.Adaptive:
                {
                    // First compile is always Expression-Compiled. Promotion (S9-T07/T08) swaps
                    // the delegate atomically once the per-pair invocation counter crosses
                    // StrategyOptions.PromotionThreshold.
                    var del = CompileExpression(blueprint, recordStrategy: MappingStrategy.ExpressionCompiled);
                    _config.AdaptivePromotion?.Register(blueprint);
                    return del;
                }

            case StrategyMode.EmitOnly:
                {
                    if (TryCompileEmit(blueprint, out var reason) is { } emitted)
                    {
                        _config.ActiveStrategies[pair] = MappingStrategy.ILEmit;
                        return emitted;
                    }
                    if (_config.Options.Strategy.ThrowOnEmitOnlyFallback)
                        throw new BlueprintNotEmittableException(pair, reason);
                    return CompileExpression(blueprint, recordStrategy: MappingStrategy.ExpressionCompiled);
                }

            default:
                throw new InvalidOperationException($"Unknown StrategyMode '{mode}'.");
        }
    }

    [SuppressMessage("Trimming", "IL3050:RequiresDynamicCode",
        Justification = "Caller-side guard: this method is only reached when StrategyOptions.Mode is not CompiledOnly. CompiledOnly is the AOT-safe default; non-default modes are documented as RequiresDynamicCode opt-in.")]
    internal Func<object, MappingScope, object>? TryCompileEmit(Blueprint blueprint)
        => TryCompileEmit(blueprint, out _);

    [SuppressMessage("Trimming", "IL3050:RequiresDynamicCode",
        Justification = "Same as TryCompileEmit(Blueprint) — opt-in only.")]
    internal Func<object, MappingScope, object>? TryCompileEmit(Blueprint blueprint, out UnsupportedReason reason)
    {
        return ILEmitMappingCompiler.Instance.Compile(
            blueprint,
            nestedResolver: pair =>
            {
                var nested = _config.TryGetBlueprint(pair);
                if (nested is null) return null;
                return _config.DelegateCache.GetOrCompile(pair, _ => Compile(nested));
            },
            transformerLookup: (origin, target) => _config.TransformerRegistry.GetTransformer(origin, target),
            reason: out reason);
    }

    private Func<object, MappingScope, object> CompileExpression(Blueprint blueprint, MappingStrategy recordStrategy)
    {
        var del = _config.Compiler.Compile(blueprint);
        _config.ActiveStrategies[blueprint.TypePair] = recordStrategy;
        return del;
    }
}
