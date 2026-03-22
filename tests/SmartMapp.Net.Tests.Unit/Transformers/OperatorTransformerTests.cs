using FluentAssertions;
using SmartMapp.Net.Tests.Unit.TestTypes;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class OperatorTransformerTests
{
    private readonly ImplicitExplicitOperatorTransformer _transformer = new();
    private readonly MappingScope _scope = new();

    [Fact]
    public void CanTransform_MoneyToDecimal_Implicit_ReturnsTrue()
    {
        _transformer.CanTransform(typeof(Money), typeof(decimal)).Should().BeTrue();
    }

    [Fact]
    public void CanTransform_DecimalToMoney_Explicit_ReturnsTrue()
    {
        _transformer.CanTransform(typeof(decimal), typeof(Money)).Should().BeTrue();
    }

    [Fact]
    public void CanTransform_NoOperator_ReturnsFalse()
    {
        _transformer.CanTransform(typeof(NoOperatorType), typeof(string)).Should().BeFalse();
    }

    [Fact]
    public void Transform_MoneyToDecimal_UsesImplicitOperator()
    {
        var money = new Money { Amount = 99.99m, Currency = "USD" };

        var result = _transformer.Transform(money, typeof(Money), typeof(decimal), _scope);

        result.Should().Be(99.99m);
    }

    [Fact]
    public void Transform_DecimalToMoney_UsesExplicitOperator()
    {
        var result = _transformer.Transform(42.50m, typeof(decimal), typeof(Money), _scope);

        result.Should().BeOfType<Money>();
        ((Money)result).Amount.Should().Be(42.50m);
    }

    [Fact]
    public void AllowExplicit_False_BlocksExplicit()
    {
        var transformer = new ImplicitExplicitOperatorTransformer(allowExplicit: false);

        // Money → decimal has implicit — should work
        transformer.CanTransform(typeof(Money), typeof(decimal)).Should().BeTrue();

        // decimal → Money only has explicit — should be blocked
        transformer.CanTransform(typeof(decimal), typeof(Money)).Should().BeFalse();
    }

    [Fact]
    public void CachedResult_NoOperator_ReturnsFalseOnSubsequentCalls()
    {
        // First call scans and caches null
        _transformer.CanTransform(typeof(NoOperatorType), typeof(int)).Should().BeFalse();

        // Second call should use cache and still return false
        _transformer.CanTransform(typeof(NoOperatorType), typeof(int)).Should().BeFalse();
    }

    [Fact]
    public void Transform_NullOrigin_Throws()
    {
        var act = () => _transformer.Transform(null, typeof(Money), typeof(decimal), _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void PrefersImplicit_WhenBothExist_SameDirection()
    {
        // Temperature has implicit operator double(Temperature) and explicit operator Temperature(double)
        // Temperature → double should resolve via implicit
        _transformer.CanTransform(typeof(Temperature), typeof(double)).Should().BeTrue();

        var temp = new Temperature { Celsius = 36.6 };
        var result = _transformer.Transform(temp, typeof(Temperature), typeof(double), _scope);
        result.Should().Be(36.6);
    }

    [Fact]
    public void DetectsOperator_DefinedOnTargetType()
    {
        // Percentage defines implicit operator Percentage(int) on the TARGET type
        // The scanner must check both origin and target types
        _transformer.CanTransform(typeof(int), typeof(Percentage)).Should().BeTrue();

        var result = _transformer.Transform(75, typeof(int), typeof(Percentage), _scope);
        result.Should().BeOfType<Percentage>();
        ((Percentage)result).Value.Should().Be(75);
    }
}
