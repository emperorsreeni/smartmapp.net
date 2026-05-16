namespace SmartMapp.Net.Engine.Promotion;

/// <summary>
/// State-machine value tracked by <see cref="AdaptivePromotionManager"/> for each
/// <see cref="TypePair"/> participating in adaptive promotion. Exposed on
/// <see cref="Diagnostics.MappingInspection"/> (S9-T10) so callers can observe the lifecycle
/// without reaching into internal promotion records.
/// </summary>
public enum PromotionState
{
    /// <summary>The pair has not yet crossed the promotion threshold.</summary>
    Cold = 0,

    /// <summary>The pair has crossed the threshold and a promotion has been scheduled.</summary>
    Hot = 1,

    /// <summary>The background worker is actively compiling the IL-Emit delegate.</summary>
    Compiling = 2,

    /// <summary>The IL-Emit delegate has been atomically swapped into the slot.</summary>
    Promoted = 3,

    /// <summary>The promotion attempt failed; the Expression-Compiled delegate remains active.</summary>
    Failed = 4,

    /// <summary>The pair is ineligible for promotion (CanEmit rejected the blueprint or the
    /// sculptor is not in <see cref="Configuration.StrategyMode.Adaptive"/>).</summary>
    Disabled = 5,
}
