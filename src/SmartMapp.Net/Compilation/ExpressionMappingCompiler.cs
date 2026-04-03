using System.Linq.Expressions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Produces strongly-typed <see cref="Expression{TDelegate}"/> for a mapping pair.
/// The uncompiled expression can be used by IQueryable providers (e.g., EF Core)
/// for <c>SelectAs&lt;T&gt;()</c> projection.
/// </summary>
internal sealed class ExpressionMappingCompiler
{
    private readonly BlueprintCompiler _compiler;

    /// <summary>
    /// Initializes a new instance of <see cref="ExpressionMappingCompiler"/>.
    /// </summary>
    /// <param name="typeModelCache">The shared type model cache.</param>
    /// <param name="delegateCache">The delegate cache.</param>
    /// <param name="blueprintResolver">Optional resolver for nested type pair blueprints.</param>
    /// <param name="transformerLookup">Optional transformer registry lookup.</param>
    internal ExpressionMappingCompiler(
        TypeModelCache typeModelCache,
        MappingDelegateCache delegateCache,
        Func<TypePair, Blueprint?>? blueprintResolver = null,
        Func<Type, Type, ITypeTransformer?>? transformerLookup = null)
    {
        _compiler = new BlueprintCompiler(typeModelCache, delegateCache, blueprintResolver, transformerLookup);
    }

    /// <summary>
    /// Compiles the blueprint into an uncompiled lambda expression.
    /// </summary>
    /// <param name="blueprint">The mapping blueprint.</param>
    /// <returns>An uncompiled <see cref="Expression{TDelegate}"/>.</returns>
    internal Expression<Func<object, MappingScope, object>> CompileToExpression(Blueprint blueprint)
    {
        return _compiler.CompileToLambda(blueprint);
    }

    /// <summary>
    /// Compiles the blueprint into a ready-to-execute delegate.
    /// </summary>
    /// <param name="blueprint">The mapping blueprint.</param>
    /// <returns>The compiled mapping delegate.</returns>
    internal Func<object, MappingScope, object> Compile(Blueprint blueprint)
    {
        return _compiler.Compile(blueprint);
    }

    /// <summary>
    /// Compiles the blueprint into a strongly-typed <see cref="Expression{TDelegate}"/>
    /// suitable for IQueryable providers (e.g., EF Core's <c>Select()</c>).
    /// The resulting expression has the form <c>(TOrigin origin) =&gt; new TTarget { ... }</c>.
    /// </summary>
    /// <typeparam name="TOrigin">The origin (source) type.</typeparam>
    /// <typeparam name="TTarget">The target (destination) type.</typeparam>
    /// <param name="blueprint">The mapping blueprint.</param>
    /// <returns>A strongly-typed uncompiled lambda expression for IQueryable projection.</returns>
    internal Expression<Func<TOrigin, TTarget>> CompileToProjectionExpression<TOrigin, TTarget>(Blueprint blueprint)
    {
        // Get the untyped lambda: (object origin, MappingScope scope) => (object)mapped
        var untypedLambda = _compiler.CompileToLambda(blueprint);

        // Rebuild as strongly-typed: (TOrigin origin) => (TTarget)body
        // For IQueryable projection we don't use MappingScope (no runtime state in DB queries)
        var originParam = Expression.Parameter(typeof(TOrigin), "origin");

        // Replace the untyped origin param and scope with typed equivalents
        var replacer = new ParameterReplacer(
            untypedLambda.Parameters[0],
            Expression.Convert(originParam, typeof(object)));

        var rewrittenBody = replacer.Visit(untypedLambda.Body);

        // Replace scope param with a default MappingScope constant (not used in projections)
        var scopeReplacer = new ParameterReplacer(
            untypedLambda.Parameters[1],
            Expression.Constant(new MappingScope(), typeof(MappingScope)));

        rewrittenBody = scopeReplacer.Visit(rewrittenBody);

        // Strip the outer Convert to object and re-convert to TTarget
        if (rewrittenBody is UnaryExpression { NodeType: ExpressionType.Convert } outerConvert
            && outerConvert.Type == typeof(object))
        {
            rewrittenBody = Expression.Convert(outerConvert.Operand, typeof(TTarget));
        }
        else
        {
            rewrittenBody = Expression.Convert(rewrittenBody, typeof(TTarget));
        }

        return Expression.Lambda<Func<TOrigin, TTarget>>(rewrittenBody, originParam);
    }

    /// <summary>
    /// Simple expression visitor that replaces a specific parameter with a substitute expression.
    /// </summary>
    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParam;
        private readonly Expression _replacement;

        internal ParameterReplacer(ParameterExpression oldParam, Expression replacement)
        {
            _oldParam = oldParam;
            _replacement = replacement;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParam ? _replacement : base.VisitParameter(node);
        }
    }
}
