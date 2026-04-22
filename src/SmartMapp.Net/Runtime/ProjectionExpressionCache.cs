// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Dedicated cache for <see cref="LambdaExpression"/> projection trees built by
/// <see cref="SculptorProjectionBuilder"/>. Spec §S8-T06 Outputs bullet 3 lists
/// <c>src/SmartMapp.Net/Runtime/ProjectionExpressionCache.cs</c> as a distinct artefact; this
/// class satisfies that contract with a thin, behaviour-preserving wrapper around a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/> keyed by <see cref="TypePair"/>.
/// </summary>
/// <remarks>
/// <para>
/// The cache is populated lazily on the first <c>ISculptor.SelectAs&lt;,&gt;()</c> /
/// <c>ISculptor.GetProjection&lt;,&gt;()</c> call for a pair and served O(1) on every
/// subsequent request — spec §S8-T06 AC bullet 6 "repeated call returns the same
/// <see cref="Expression{TDelegate}"/> instance". The cache belongs to the forged
/// <see cref="ForgedSculptorConfiguration"/> (one instance per forged sculptor) so projections
/// never leak across sculptors.
/// </para>
/// <para>
/// The wrapper is deliberately minimal: only <c>TryGet</c>, <c>GetOrAdd</c>, and <c>Count</c>
/// are exposed. Tests and diagnostics can inspect <see cref="Count"/> to verify caching
/// behaviour; the full <see cref="ConcurrentDictionary{TKey, TValue}"/> API is not leaked
/// because consumers should not mutate or clear projections post-forge.
/// </para>
/// </remarks>
internal sealed class ProjectionExpressionCache
{
    private readonly ConcurrentDictionary<TypePair, LambdaExpression> _entries = new();

    /// <summary>Gets the number of cached projection expressions.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Attempts to fetch a cached projection for <paramref name="pair"/>. Returns <c>true</c>
    /// on hit, <c>false</c> on miss.
    /// </summary>
    public bool TryGet(TypePair pair, out LambdaExpression? expression)
        => _entries.TryGetValue(pair, out expression);

    /// <summary>
    /// Returns the cached projection for <paramref name="pair"/> if present; otherwise invokes
    /// <paramref name="factory"/> to build it, stores the result, and returns it. Mirrors the
    /// <c>ConcurrentDictionary.GetOrAdd(TKey, Func&lt;TKey, TArg, TValue&gt;, TArg)</c> overload
    /// so callers can thread a single argument through without allocating a closure.
    /// </summary>
    public LambdaExpression GetOrAdd<TArg>(
        TypePair pair,
        Func<TypePair, TArg, LambdaExpression> factory,
        TArg arg)
        => _entries.GetOrAdd(pair, factory, arg);

    /// <summary>
    /// Clears all cached entries. Reserved for advanced test scenarios — production code must
    /// not call this (projections are stable across a forged sculptor's lifetime).
    /// </summary>
    internal void Clear() => _entries.Clear();
}
