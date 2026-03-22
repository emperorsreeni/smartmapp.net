using System.Linq.Expressions;
using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Tests.Unit.Compilation;

public class TransformerExpressionHelperTests
{
    private readonly ParameterExpression _scopeParam = Expression.Parameter(typeof(MappingScope), "scope");

    [Fact]
    public void SameType_NoConversion()
    {
        var originExpr = Expression.Constant(42, typeof(int));

        var result = TransformerExpressionHelper.BuildTransformExpression(
            originExpr, typeof(int), typeof(int), _scopeParam, null);

        result.Should().BeSameAs(originExpr);
    }

    [Fact]
    public void AssignableType_DirectAssignment()
    {
        var originExpr = Expression.Constant("hello", typeof(string));

        var result = TransformerExpressionHelper.BuildTransformExpression(
            originExpr, typeof(string), typeof(object), _scopeParam, null);

        // string is assignable to object, so no conversion needed
        result.Should().BeSameAs(originExpr);
    }

    [Fact]
    public void NumericWidening_IntToLong()
    {
        var param = Expression.Parameter(typeof(int), "value");

        var result = TransformerExpressionHelper.BuildTransformExpression(
            param, typeof(int), typeof(long), _scopeParam, null);

        var lambda = Expression.Lambda<Func<int, long>>(result, param).Compile();
        lambda(42).Should().Be(42L);
    }

    [Fact]
    public void NullableWrapping_IntToNullableInt()
    {
        var param = Expression.Parameter(typeof(int), "value");

        var result = TransformerExpressionHelper.BuildTransformExpression(
            param, typeof(int), typeof(int?), _scopeParam, null);

        var lambda = Expression.Lambda<Func<int, int?>>(result, param).Compile();
        lambda(42).Should().Be(42);
    }

    [Fact]
    public void NullableUnwrapping_NullableIntToInt()
    {
        var param = Expression.Parameter(typeof(int?), "value");

        var result = TransformerExpressionHelper.BuildTransformExpression(
            param, typeof(int?), typeof(int), _scopeParam, null);

        var lambda = Expression.Lambda<Func<int?, int>>(result, param).Compile();
        lambda(42).Should().Be(42);
    }

    [Fact]
    public void ExplicitTransformer_Invoked()
    {
        var transformer = new TestIntToStringTransformer();
        var param = Expression.Parameter(typeof(int), "value");

        var result = TransformerExpressionHelper.BuildTransformExpression(
            param, typeof(int), typeof(string), _scopeParam, transformer);

        var scopeParam2 = _scopeParam;
        var lambda = Expression.Lambda<Func<int, MappingScope, string>>(result, param, scopeParam2).Compile();
        lambda(42, new MappingScope()).Should().Be("42");
    }

    [Fact]
    public void NoConversion_ThrowsMappingCompilationException()
    {
        var param = Expression.Parameter(typeof(DateTime), "value");

        var act = () => TransformerExpressionHelper.BuildTransformExpression(
            param, typeof(DateTime), typeof(List<int>), _scopeParam, null);

        act.Should().Throw<MappingCompilationException>();
    }

    // Test transformer
    private sealed class TestIntToStringTransformer : ITypeTransformer<int, string>
    {
        public bool CanTransform(Type originType, Type targetType)
            => originType == typeof(int) && targetType == typeof(string);

        public string Transform(int origin, MappingScope scope)
            => origin.ToString();
    }
}
