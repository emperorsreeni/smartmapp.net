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
}
