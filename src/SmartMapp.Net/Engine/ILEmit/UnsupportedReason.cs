namespace SmartMapp.Net.Engine.ILEmit;

/// <summary>
/// Reason returned by <see cref="EmitDiagnostics.CanEmit"/> when a <see cref="Blueprint"/> cannot
/// be lowered to IL by the Sprint 9 emit pipeline. The strategy chain (spec §S9-T05) uses these
/// values to log fall-back decisions and surface them on <c>BlueprintNotEmittableException</c>.
/// </summary>
public enum UnsupportedReason
{
    /// <summary>The blueprint is fully emit-eligible.</summary>
    None = 0,

    /// <summary>The target type lacks an accessible parameterless constructor.</summary>
    MissingParameterlessCtor = 1,

    /// <summary>One or more property links use a value provider the emitter cannot inline.</summary>
    UnsupportedValueProvider = 2,

    /// <summary>The blueprint contains a link with a custom condition / fallback / order
    /// shape that the Sprint 9 emitter doesn't lower (yet).</summary>
    UnsupportedLinkShape = 3,

    /// <summary>The blueprint targets an open generic type.</summary>
    OpenGeneric = 4,

    /// <summary>The blueprint references a type transformer marked <c>ScopedTransformer</c>
    /// (Sprint 8 T04) whose instance is per-call and therefore cannot be cached in a static
    /// IL slot.</summary>
    ScopedTransformer = 5,

    /// <summary>The target type is abstract or an interface — construction is impossible
    /// without a polymorphic factory, which the IL Emit fast path doesn't host.</summary>
    AbstractOrInterfaceTarget = 6,

    /// <summary>The blueprint declares <c>TrackReferences</c>, an explicit <c>TargetFactory</c>,
    /// pre/post hooks, or a type-level condition — these flow through the Expression Compiler
    /// to preserve identical semantics.</summary>
    UsesAdvancedBlueprintFeatures = 7,

    /// <summary>The blueprint declares a type-level transformer
    /// (<see cref="Blueprint.TypeTransformer"/>) — IL Emit defers to the Expression Compiler
    /// so post-processing semantics stay byte-identical.</summary>
    HasTypeLevelTransformer = 8,

    /// <summary>The link targets a member shape the emitter doesn't support yet (e.g. fields,
    /// indexer properties, write-only setters).</summary>
    UnsupportedTargetMember = 9,

    /// <summary>The link types are not assignment-compatible via the Sprint 9 coercion table
    /// (same-type, widening numeric, <c>Nullable&lt;T&gt;</c> unwrap, reference identity).</summary>
    UnsupportedTypeCoercion = 10,

    /// <summary>The blueprint declares a <c>StrictRequiredMembers</c> contract — defer to the
    /// Expression Compiler which already performs the validation.</summary>
    StrictRequiredMembers = 11,

    /// <summary>The blueprint targets a nested generic / value-type combination not yet covered
    /// by the Sprint 9 emit-pipeline (e.g. nested struct with circular reference).</summary>
    NestedShapeNotSupported = 12,
}
