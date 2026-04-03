using System.Linq.Expressions;
using FluentAssertions;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Tests.Unit.Compilation;

/// <summary>
/// Direct unit tests for <see cref="DepthLimitGuard"/> expression tree fragments.
/// Verifies that depth checking returns default when limit reached and passes through otherwise.
/// </summary>
public class DepthLimitGuardTests
{
    private readonly ParameterExpression _scopeParam = Expression.Parameter(typeof(MappingScope), "scope");

    [Fact]
    public void WrapWithDepthCheck_DepthNotReached_ReturnsMappingBody()
    {
        var bodyExpr = Expression.Constant("mapped", typeof(object));

        var wrapped = DepthLimitGuard.WrapWithDepthCheck(bodyExpr, _scopeParam, typeof(object));

        var lambda = Expression.Lambda<Func<MappingScope, object>>(wrapped, _scopeParam).Compile();

        var scope = new MappingScope { MaxDepth = 10 };
        var result = lambda(scope);

        result.Should().Be("mapped");
    }

    [Fact]
    public void WrapWithDepthCheck_DepthReached_ReturnsDefault()
    {
        var bodyExpr = Expression.Constant("should-not-reach", typeof(string));

        var wrapped = DepthLimitGuard.WrapWithDepthCheck(bodyExpr, _scopeParam, typeof(string));

        var lambda = Expression.Lambda<Func<MappingScope, string>>(wrapped, _scopeParam).Compile();

        // Create a scope that is already at max depth
        var scope = new MappingScope { MaxDepth = 0 };
        var result = lambda(scope);

        result.Should().BeNull(); // default(string) is null
    }

    [Fact]
    public void WrapWithDepthCheck_ValueTypeTarget_DepthReached_ReturnsDefaultValue()
    {
        var bodyExpr = Expression.Constant(999, typeof(int));

        var wrapped = DepthLimitGuard.WrapWithDepthCheck(bodyExpr, _scopeParam, typeof(int));

        var lambda = Expression.Lambda<Func<MappingScope, int>>(wrapped, _scopeParam).Compile();

        var scope = new MappingScope { MaxDepth = 0 };
        var result = lambda(scope);

        result.Should().Be(0); // default(int) is 0
    }

    [Fact]
    public void WrapWithDepthCheck_DefaultMaxDepth_NeverBlocks()
    {
        var bodyExpr = Expression.Constant("deep-value", typeof(string));

        var wrapped = DepthLimitGuard.WrapWithDepthCheck(bodyExpr, _scopeParam, typeof(string));

        var lambda = Expression.Lambda<Func<MappingScope, string>>(wrapped, _scopeParam).Compile();

        // Default MappingScope with int.MaxValue depth — should never block
        var scope = new MappingScope();
        var result = lambda(scope);

        result.Should().Be("deep-value");
    }
}
