namespace SmartMapp.Net.Engine.ILEmit;

/// <summary>
/// Thrown by the Sprint 9 strategy chain when <see cref="Configuration.StrategyMode.EmitOnly"/>
/// is active and a <see cref="Blueprint"/> is rejected by <see cref="EmitDiagnostics.CanEmit"/>.
/// Surfaces the precise <see cref="UnsupportedReason"/> so users and benchmarks can fix the
/// blueprint instead of silently falling back to Expression-Compiled execution.
/// </summary>
public sealed class BlueprintNotEmittableException : SmartMappException
{
    /// <summary>The blueprint pair that failed the IL Emit capability probe.</summary>
    public TypePair TypePair { get; }

    /// <summary>The first disqualifying observation reported by <see cref="EmitDiagnostics"/>.</summary>
    public UnsupportedReason Reason { get; }

    internal BlueprintNotEmittableException(TypePair pair, UnsupportedReason reason)
        : base(BuildMessage(pair, reason))
    {
        TypePair = pair;
        Reason = reason;
    }

    private static string BuildMessage(TypePair pair, UnsupportedReason reason)
        => $"Blueprint '{pair}' is not IL-Emit-eligible (Reason: {reason}). "
         + "Either revise the blueprint to satisfy EmitDiagnostics.CanEmit, switch the "
         + "sculptor to StrategyMode.EmitFirst / Adaptive (which silently falls back to the "
         + "Expression Compiler), or set Options.Strategy.ThrowOnEmitOnlyFallback = false to "
         + "permit the fall-back in EmitOnly mode.";
}
