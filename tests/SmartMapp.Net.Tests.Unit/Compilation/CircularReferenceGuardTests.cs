using System.Linq.Expressions;
using FluentAssertions;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Tests.Unit.Compilation;

/// <summary>
/// Direct unit tests for <see cref="CircularReferenceGuard"/> expression tree fragments.
/// Verifies that reference tracking, cache hits, and value-type bypass work at the expression level.
/// </summary>
public class CircularReferenceGuardTests
{
    private readonly ParameterExpression _scopeParam = Expression.Parameter(typeof(MappingScope), "scope");

    [Fact]
    public void WrapWithReferenceTracking_TrackingDisabled_ReturnsMappingBody()
    {
        var originExpr = Expression.Constant("origin", typeof(object));
        var bodyExpr = Expression.Constant("mapped", typeof(object));

        var result = CircularReferenceGuard.WrapWithReferenceTracking(
            originExpr, typeof(object), () => bodyExpr, _scopeParam, trackReferences: false);

        // When tracking is disabled, the mapping body should be returned as-is
        result.Should().BeSameAs(bodyExpr);
    }

    [Fact]
    public void WrapWithReferenceTracking_ValueType_ReturnsMappingBody()
    {
        var originExpr = Expression.Constant(42, typeof(int));
        var bodyExpr = Expression.Constant(42, typeof(int));

        var result = CircularReferenceGuard.WrapWithReferenceTracking(
            originExpr, typeof(int), () => bodyExpr, _scopeParam, trackReferences: true);

        // Value types should bypass tracking
        result.Should().BeSameAs(bodyExpr);
    }

    [Fact]
    public void WrapWithReferenceTracking_ReferenceType_EmitsCheckAndFallthrough()
    {
        // Build a lambda that we can actually invoke:
        // (object origin, MappingScope scope) => WrapWithReferenceTracking(...)
        var originParam = Expression.Parameter(typeof(object), "origin");

        Expression mappingBody = Expression.Constant("new-mapped", typeof(object));

        var wrapped = CircularReferenceGuard.WrapWithReferenceTracking(
            originParam, typeof(object), () => mappingBody, _scopeParam, trackReferences: true);

        var lambda = Expression.Lambda<Func<object, MappingScope, object>>(
            wrapped, originParam, _scopeParam).Compile();

        // First call: not visited → should return "new-mapped"
        var scope = new MappingScope();
        var origin = new object();
        var result = lambda(origin, scope);
        result.Should().Be("new-mapped");
    }

    [Fact]
    public void WrapWithReferenceTracking_AlreadyVisited_ReturnsCachedInstance()
    {
        var originParam = Expression.Parameter(typeof(object), "origin");

        Expression mappingBody = Expression.Constant("should-not-reach", typeof(object));

        var wrapped = CircularReferenceGuard.WrapWithReferenceTracking(
            originParam, typeof(object), () => mappingBody, _scopeParam, trackReferences: true);

        var lambda = Expression.Lambda<Func<object, MappingScope, object>>(
            wrapped, originParam, _scopeParam).Compile();

        var scope = new MappingScope();
        var origin = new object();
        var cached = new object();

        // Pre-track the origin so TryGetVisited returns true
        scope.TrackVisited(origin, cached);

        var result = lambda(origin, scope);
        result.Should().BeSameAs(cached);
    }

    [Fact]
    public void BuildTrackExpression_AddsToVisitedMap()
    {
        var originParam = Expression.Parameter(typeof(object), "origin");
        var targetParam = Expression.Parameter(typeof(object), "target");

        var trackExpr = CircularReferenceGuard.BuildTrackExpression(originParam, targetParam, _scopeParam);

        var lambda = Expression.Lambda<Action<object, object, MappingScope>>(
            trackExpr, originParam, targetParam, _scopeParam).Compile();

        var scope = new MappingScope();
        var origin = new object();
        var target = new object();

        lambda(origin, target, scope);

        scope.TryGetVisited(origin, out var retrieved).Should().BeTrue();
        retrieved.Should().BeSameAs(target);
    }
}
