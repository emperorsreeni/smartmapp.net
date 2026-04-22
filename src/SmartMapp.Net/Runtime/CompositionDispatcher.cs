using System.Reflection;
using System.Text;
using SmartMapp.Net.Composition;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Executes multi-origin composition at runtime per spec §8.11 / Sprint 8 · S8-T08. Given a
/// <see cref="CompositionBlueprint"/> and a caller-supplied origins array, matches each
/// instance to a declared origin slot by runtime type, runs each origin's partial blueprint,
/// and merges the contributed target members into a single target instance.
/// </summary>
/// <remarks>
/// <para>
/// Origins are applied in <b>declaration order</b> (the order of <c>FromOrigin&lt;TOrigin&gt;()</c>
/// calls on the composition rule), which makes the composition call-site order-independent —
/// <c>Compose&lt;T&gt;(a, b)</c> and <c>Compose&lt;T&gt;(b, a)</c> produce the same result
/// (spec §S8-T08 Acceptance bullet 3). Collision resolution is last-origin-wins against
/// declaration order; the richer per-origin <c>.From</c> / <c>.When</c> override surface is
/// Sprint 15.
/// </para>
/// <para>
/// <see cref="ForgedSculptorConfiguration.CompositionOriginDelegates"/> caches the compiled
/// <see cref="Func{Object, MappingScope, Object}"/> per <c>(TargetType, OriginType)</c> so
/// repeat calls amortise the compilation cost.
/// </para>
/// </remarks>
internal static class CompositionDispatcher
{
    internal static TTarget Dispatch<TTarget>(
        ForgedSculptorConfiguration config,
        object?[] origins)
    {
        if (origins is null) throw new ArgumentNullException(nameof(origins));

        var targetType = typeof(TTarget);
        var blueprint = config.TryGetCompositionBlueprint(targetType)
            ?? throw new MappingConfigurationException(
                $"No composition blueprint registered for target '{targetType.FullName}'. " +
                "Register via options.Compose<TTarget>().FromOrigin<TOrigin>() or SculptorBuilder.Compose<TTarget>().",
                new TypePair(typeof(object), targetType));

        // Build a caller-type signature string and try the cached slot-assignment plan first
        // (spec §S8-T08 Technical Considerations bullet 1 — cache keyed by ordered origin types
        // avoids per-call reflection). Cache miss: compute the assignment, validate ambiguity,
        // then memoise on success.
        var signature = BuildCallerSignature(targetType, origins);
        if (!config.CompositionSlotAssignments.TryGetValue(signature, out var slotAssignments))
        {
            slotAssignments = ComputeSlotAssignments(blueprint, origins, targetType);
            config.CompositionSlotAssignments.TryAdd(signature, slotAssignments);
        }

        // Dispatch slot -> instance from the assignment plan. Slot values match blueprint.Origins
        // index order (declaration order) so collision-resolution stays last-declared-wins.
        var slots = new object?[blueprint.Origins.Count];
        for (var i = 0; i < origins.Length; i++)
        {
            var instance = origins[i];
            if (instance is null) continue;
            var slot = slotAssignments[i];
            if (slot < 0) continue; // instance didn't match any declared slot — silently skipped
            if (slots[slot] is not null)
            {
                // Two different caller instances matched the same declared slot. The cache was
                // populated by a prior call that DID succeed (e.g., with null in one position);
                // this caller happens to collide. Surface the ambiguous-match exception.
                throw new MappingConfigurationException(
                    $"Ambiguous composition origin: two caller instances both match declared origin " +
                    $"'{blueprint.Origins[slot].OriginType.FullName}' for target '{targetType.FullName}'. " +
                    "Each declared origin type may receive at most one instance per Compose call " +
                    "(spec §S8-T08 Constraints).",
                    new TypePair(blueprint.Origins[slot].OriginType, targetType));
            }
            slots[slot] = instance;
        }

        // Instantiate the target.
        var ctor = targetType.GetConstructor(Type.EmptyTypes)
            ?? throw new MappingConfigurationException(
                $"Target '{targetType.FullName}' has no public parameterless constructor; " +
                "composition requires a default constructor. Register a custom factory via " +
                "FromOrigin<TOrigin>(r => r.BuildWith(...)) once Sprint 15's richer surface lands.",
                new TypePair(typeof(object), targetType));
        var target = (TTarget)ctor.Invoke(null);

        // Walk declared origins in order; run each matching origin's partial blueprint and
        // reflection-copy the property-link members into the shared target — last declaration
        // wins on collisions, matching the declaration-order semantic.
        var scope = MappingExecutor.CreateScope(config);
        for (var slotIdx = 0; slotIdx < blueprint.Origins.Count; slotIdx++)
        {
            var originValue = slots[slotIdx];
            if (originValue is null) continue;

            var decl = blueprint.Origins[slotIdx];
            var originType = decl.OriginType;
            var targetPartialType = decl.PartialBlueprint.TargetType;
            var cacheKey = (targetPartialType, originType);
            var del = config.CompositionOriginDelegates.GetOrAdd(
                cacheKey,
                static (_, state) => state.config.Compiler.Compile(state.partial),
                (config, partial: decl.PartialBlueprint));

            var partialTarget = del(originValue, scope);

            foreach (var link in decl.PartialBlueprint.Links)
            {
                if (link.IsSkipped) continue;
                CopyMember(partialTarget, target!, link.TargetMember);
            }
        }

        return target!;
    }

    /// <summary>
    /// Builds a deterministic string signature from the caller's target type + per-origin
    /// runtime types. Used as the key for <see cref="ForgedSculptorConfiguration.CompositionSlotAssignments"/>
    /// so repeated dispatch calls with the same caller shape hit the cache.
    /// </summary>
    private static string BuildCallerSignature(Type targetType, object?[] origins)
    {
        var sb = new StringBuilder(targetType.FullName ?? targetType.Name);
        foreach (var o in origins)
        {
            sb.Append('|');
            sb.Append(o is null ? "null" : (o.GetType().FullName ?? o.GetType().Name));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Computes the caller-origin-index → declared-slot-index mapping by iterating the
    /// blueprint's declared origins once per caller origin. Throws
    /// <see cref="MappingConfigurationException"/> when one caller instance matches more than
    /// one declared origin (declared-side ambiguity); caller-side ambiguity (two instances
    /// matching the same slot) is detected during dispatch so the cache can still be populated
    /// for the successful shape.
    /// </summary>
    private static int[] ComputeSlotAssignments(CompositionBlueprint blueprint, object?[] origins, Type targetType)
    {
        var assignments = new int[origins.Length];
        for (var i = 0; i < origins.Length; i++)
        {
            var instance = origins[i];
            if (instance is null)
            {
                assignments[i] = -1;
                continue;
            }

            var runtimeType = instance.GetType();
            var matchedIdx = -1;
            for (var j = 0; j < blueprint.Origins.Count; j++)
            {
                if (blueprint.Origins[j].OriginType.IsAssignableFrom(runtimeType))
                {
                    if (matchedIdx >= 0)
                    {
                        throw new MappingConfigurationException(
                            $"Ambiguous composition origin: instance of '{runtimeType.FullName}' matches " +
                            $"multiple declared origins for target '{targetType.FullName}' " +
                            $"('{blueprint.Origins[matchedIdx].OriginType.Name}' and " +
                            $"'{blueprint.Origins[j].OriginType.Name}'). Tighten the declared origin types " +
                            "so each instance maps to exactly one slot (spec §S8-T08 Constraints: ambiguous match).",
                            new TypePair(runtimeType, targetType));
                    }
                    matchedIdx = j;
                }
            }
            assignments[i] = matchedIdx;
        }
        return assignments;
    }

    private static void CopyMember(object source, object destination, MemberInfo member)
    {
        switch (member)
        {
            case PropertyInfo prop when prop.CanRead && prop.CanWrite:
                prop.SetValue(destination, prop.GetValue(source));
                break;
            case FieldInfo field when !field.IsInitOnly:
                field.SetValue(destination, field.GetValue(source));
                break;
            // Init-only properties / readonly fields fall through silently — the partial
            // target's constructor-set values already carry the correct data, but we can't
            // rewrite them on a shared accumulator. Sprint 15 lifts this restriction via
            // per-origin factory support.
        }
    }
}
