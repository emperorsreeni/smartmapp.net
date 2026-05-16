namespace SmartMapp.Net.Configuration;

/// <summary>
/// Strategy ladder configuration consumed by the Sprint 9 mapping pipeline. Selects which
/// concrete code-generation path the sculptor uses for each <see cref="Blueprint"/> at compile
/// time and how adaptive promotion behaves at run time. Defaults to <see cref="StrategyMode.CompiledOnly"/>
/// which preserves the exact Sprint 8 RC behaviour and the AOT-safe execution path.
/// </summary>
/// <remarks>
/// <para>
/// Users opt in to IL Emit either eagerly (<see cref="StrategyMode.EmitFirst"/> /
/// <see cref="StrategyMode.EmitOnly"/>) or adaptively (<see cref="StrategyMode.Adaptive"/>) by
/// setting <see cref="Mode"/> before calling <c>SculptorBuilder.Forge()</c>. Promotion
/// behaviour (the per-pair invocation count required to trigger background IL emission) is
/// tuned via <see cref="PromotionThreshold"/>.
/// </para>
/// <para>
/// Sprint 9 spec reference: §4.2 (strategy ladder), §9.1 (performance targets), §9.2 (IL Emit
/// engine), §S9-T05 (strategy selection).
/// </para>
/// </remarks>
public sealed class StrategyOptions
{
    private readonly SculptorOptions _owner;

    internal StrategyOptions(SculptorOptions owner) => _owner = owner;

    /// <summary>
    /// Gets or sets the strategy ladder mode. Defaults to <see cref="StrategyMode.CompiledOnly"/>.
    /// </summary>
    public StrategyMode Mode
    {
        get => _mode;
        set { _owner.ThrowIfFrozen(); _mode = value; }
    }
    private StrategyMode _mode = StrategyMode.CompiledOnly;

    /// <summary>
    /// Gets or sets the per-<c>(TOrigin, TTarget)</c> invocation threshold at which the
    /// <c>AdaptivePromotionManager</c> (Sprint 9 · S9-T07) schedules promotion of the
    /// Expression-Compiled delegate to an IL-Emitted one. Default <c>10</c>. Only honoured when
    /// <see cref="Mode"/> is <see cref="StrategyMode.Adaptive"/>; ignored otherwise.
    /// </summary>
    public int PromotionThreshold
    {
        get => _promotionThreshold;
        set
        {
            _owner.ThrowIfFrozen();
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "PromotionThreshold must be positive.");
            _promotionThreshold = value;
        }
    }
    private int _promotionThreshold = 10;

    /// <summary>
    /// Gets or sets a value indicating whether the strategy chain throws a
    /// <c>BlueprintNotEmittableException</c> when <see cref="StrategyMode.EmitOnly"/> meets a
    /// blueprint that the IL Emit capability probe rejects. Defaults to <c>true</c> — the
    /// <c>EmitOnly</c> mode is the canonical opt-in for benchmark / AOT-rehearsal scenarios
    /// where a silent fallback would mask the issue.
    /// </summary>
    public bool ThrowOnEmitOnlyFallback
    {
        get => _throwOnEmitOnlyFallback;
        set { _owner.ThrowIfFrozen(); _throwOnEmitOnlyFallback = value; }
    }
    private bool _throwOnEmitOnlyFallback = true;
}

/// <summary>
/// Selection knob for <see cref="StrategyOptions.Mode"/>. Determines which concrete mapping
/// strategy (<see cref="MappingStrategy.ExpressionCompiled"/> vs
/// <see cref="MappingStrategy.ILEmit"/>) the sculptor uses for each blueprint and whether
/// adaptive promotion runs.
/// </summary>
public enum StrategyMode
{
    /// <summary>
    /// Sprint 8 RC behaviour: every blueprint compiles to an Expression-Compiled delegate. No
    /// IL Emit, no adaptive promotion, no <c>System.Reflection.Emit</c> at run time. The AOT-safe
    /// default.
    /// </summary>
    CompiledOnly = 0,

    /// <summary>
    /// Try IL Emit eagerly at compile time. Blueprints whose
    /// <see cref="Engine.ILEmit.EmitDiagnostics.CanEmit"/> returns <c>true</c> are emitted
    /// straight to a <c>DynamicMethod</c>; the rest silently fall back to Expression-Compiled.
    /// Suitable for warm-up / pre-JIT scenarios.
    /// </summary>
    EmitFirst = 1,

    /// <summary>
    /// Start with Expression-Compiled and let the <c>AdaptivePromotionManager</c> (Sprint 9 ·
    /// S9-T07) promote hot pairs to IL Emit in the background after
    /// <see cref="StrategyOptions.PromotionThreshold"/> invocations. The eventual hot-path default
    /// once the IL Emit pipeline is judged production-ready.
    /// </summary>
    Adaptive = 2,

    /// <summary>
    /// Refuse to fall back: every blueprint must be emit-eligible. Used by benchmarks and the
    /// AOT-rehearsal smoke test (with <see cref="StrategyOptions.ThrowOnEmitOnlyFallback"/>
    /// disabled) to guarantee the IL Emit code path is exercised end-to-end.
    /// </summary>
    EmitOnly = 3,
}
