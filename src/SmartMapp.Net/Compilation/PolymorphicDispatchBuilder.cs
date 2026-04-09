using System.Linq.Expressions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Builds polymorphic dispatch expression trees for inheritance hierarchies.
/// Generates a chain of <c>if (origin is DerivedType) return DerivedMapper(origin, scope)</c> checks
/// ordered by type specificity (most derived first), with a fallback to the base mapper.
/// </summary>
internal sealed class PolymorphicDispatchBuilder
{
    private readonly InheritanceResolver _inheritanceResolver;
    private readonly MappingDelegateCache _delegateCache;

    /// <summary>
    /// Initializes a new <see cref="PolymorphicDispatchBuilder"/>.
    /// </summary>
    internal PolymorphicDispatchBuilder(
        InheritanceResolver inheritanceResolver,
        MappingDelegateCache delegateCache)
    {
        _inheritanceResolver = inheritanceResolver;
        _delegateCache = delegateCache;
    }

    /// <summary>
    /// Builds a polymorphic dispatch delegate that checks the runtime origin type
    /// and dispatches to the correct derived mapping delegate.
    /// Returns <c>null</c> if no derived pairs exist (no polymorphism needed).
    /// </summary>
    /// <param name="basePair">The declared base type pair.</param>
    /// <param name="baseDelegate">The compiled delegate for the base type pair.</param>
    /// <param name="compileDerived">Factory to compile a derived type pair's delegate on demand.</param>
    /// <returns>A polymorphic dispatch delegate, or <c>null</c> if no derived pairs exist.</returns>
    internal Func<object, MappingScope, object>? BuildDispatchDelegate(
        TypePair basePair,
        Func<object, MappingScope, object> baseDelegate,
        Func<TypePair, Func<object, MappingScope, object>> compileDerived)
    {
        // Check for discriminator-based dispatch first
        var discriminator = _inheritanceResolver.GetDiscriminator(basePair);
        if (discriminator is not null)
        {
            return BuildDiscriminatorDispatch(basePair, baseDelegate, discriminator, compileDerived);
        }

        // Type-based polymorphic dispatch
        var derivedPairs = _inheritanceResolver.GetDerivedPairs(basePair);
        if (derivedPairs.Count == 0)
            return null;

        return BuildTypeDispatch(basePair, baseDelegate, derivedPairs, compileDerived);
    }

    /// <summary>
    /// Builds a type-check-based dispatch delegate.
    /// Pattern: if (origin is Circle) return CircleMapper(...); else if ... else return BaseMapper(...)
    /// </summary>
    private Func<object, MappingScope, object> BuildTypeDispatch(
        TypePair basePair,
        Func<object, MappingScope, object> baseDelegate,
        IReadOnlyList<TypePair> derivedPairs,
        Func<TypePair, Func<object, MappingScope, object>> compileDerived)
    {
        // Pre-compile all derived delegates
        var derivedDelegates = new List<(Type originType, Type targetType, Func<object, MappingScope, object> del)>();

        foreach (var pair in derivedPairs)
        {
            var del = _delegateCache.GetOrCompile(pair, compileDerived);
            derivedDelegates.Add((pair.OriginType, pair.TargetType, del));
        }

        // Return a dispatch delegate that checks runtime type
        return (origin, scope) =>
        {
            if (origin is null)
                return null!;

            var runtimeType = origin.GetType();

            // Skip dispatch if runtime type matches base exactly
            if (runtimeType == basePair.OriginType)
                return baseDelegate(origin, scope);

            // Check derived types (most specific first)
            foreach (var (originType, targetType, del) in derivedDelegates)
            {
                if (originType.IsAssignableFrom(runtimeType))
                    return del(origin, scope);
            }

            // Fallback to base
            return baseDelegate(origin, scope);
        };
    }

    /// <summary>
    /// Builds a discriminator-value-based dispatch delegate.
    /// Pattern: var disc = origin.Prop; if (disc == "X") return XMapper(...); else return OtherwiseMapper(...)
    /// </summary>
    private Func<object, MappingScope, object> BuildDiscriminatorDispatch(
        TypePair basePair,
        Func<object, MappingScope, object> baseDelegate,
        DiscriminatorConfig discriminator,
        Func<TypePair, Func<object, MappingScope, object>> compileDerived)
    {
        // Compile the discriminator expression
        var discFunc = discriminator.DiscriminatorExpression.Compile();

        // Pre-compile all When-clause delegates
        var whenDelegates = new List<(object value, Func<object, MappingScope, object> del)>();
        foreach (var clause in discriminator.WhenClauses)
        {
            var del = _delegateCache.GetOrCompile(clause.TargetPair, compileDerived);
            whenDelegates.Add((clause.Value, del));
        }

        // Compile Otherwise delegate
        Func<object, MappingScope, object> otherwiseDelegate = baseDelegate;
        if (discriminator.OtherwisePair.HasValue)
        {
            otherwiseDelegate = _delegateCache.GetOrCompile(discriminator.OtherwisePair.Value, compileDerived);
        }

        return (origin, scope) =>
        {
            if (origin is null)
                return null!;

            var discValue = discFunc.DynamicInvoke(origin);

            foreach (var (value, del) in whenDelegates)
            {
                // Use StringComparer.Ordinal for string discriminators per spec §S6-T02
                if (discValue is string strDisc && value is string strVal)
                {
                    if (StringComparer.Ordinal.Equals(strDisc, strVal))
                        return del(origin, scope);
                }
                else if (Equals(discValue, value))
                {
                    return del(origin, scope);
                }
            }

            return otherwiseDelegate(origin, scope);
        };
    }
}
