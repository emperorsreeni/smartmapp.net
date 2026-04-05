using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class PropertyRuleTests
{
    private readonly PropertyConfiguration _config;
    private readonly PropertyRule<SimpleClass, SimpleDto, string> _rule;

    public PropertyRuleTests()
    {
        _config = new PropertyConfiguration
        {
            TargetMember = typeof(SimpleDto).GetProperty("Name")!
        };
        _rule = new PropertyRule<SimpleClass, SimpleDto, string>(_config);
    }

    [Fact]
    public void From_SetsOriginExpression()
    {
        _rule.From(s => s.Name);

        _config.OriginExpression.Should().NotBeNull();
    }

    [Fact]
    public void From_AfterSkip_Throws()
    {
        _config.IsSkipped = true;

        var act = () => _rule.From(s => s.Name);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Skip()*");
    }

    [Fact]
    public void FromProvider_SetsProviderType()
    {
        _rule.From<TestValueProvider>();

        _config.ProviderType.Should().Be(typeof(TestValueProvider));
    }

    [Fact]
    public void FromProvider_AfterSkip_Throws()
    {
        _config.IsSkipped = true;

        var act = () => _rule.From<TestValueProvider>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Skip()*");
    }

    [Fact]
    public void Skip_SetsFlag()
    {
        _rule.Skip();

        _config.IsSkipped.Should().BeTrue();
    }

    [Fact]
    public void Skip_AfterFrom_Throws()
    {
        _rule.From(s => s.Name);

        var act = () => _rule.Skip();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*From()*");
    }

    [Fact]
    public void FallbackTo_SetsFallback()
    {
        _rule.FallbackTo("default");

        _config.HasFallback.Should().BeTrue();
        _config.FallbackValue.Should().Be("default");
    }

    [Fact]
    public void When_SetsCondition()
    {
        _rule.When(s => s.Id > 0);

        _config.Condition.Should().NotBeNull();
    }

    [Fact]
    public void OnlyIf_SetsPreCondition()
    {
        _rule.OnlyIf(s => s.Id > 0);

        _config.PreCondition.Should().NotBeNull();
    }

    [Fact]
    public void TransformWith_SetsTransformerType()
    {
        _rule.TransformWith<TestTransformer>();

        _config.TransformerType.Should().Be(typeof(TestTransformer));
    }

    [Fact]
    public void TransformWith_InlineExpression_SetsInlineTransform()
    {
        _rule.TransformWith(v => v.ToUpper());

        _config.InlineTransform.Should().NotBeNull();
    }

    [Fact]
    public void SetOrder_SetsOrder()
    {
        _rule.SetOrder(5);

        _config.Order.Should().Be(5);
    }

    [Fact]
    public void PostProcess_SetsExpression()
    {
        _rule.PostProcess(v => v.Trim());

        _config.PostProcess.Should().NotBeNull();
    }

    [Fact]
    public void FluentChaining_AllMethodsReturnSameRule()
    {
        var result = _rule
            .From(s => s.Name)
            .FallbackTo("default")
            .When(s => s.Id > 0)
            .OnlyIf(s => s.Name != null)
            .SetOrder(1);

        result.Should().BeSameAs(_rule);
    }

    // Test helper types
    private class TestValueProvider : IValueProvider
    {
        public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
            => "test";
    }

    private class TestTransformer : ITypeTransformer
    {
        public bool CanTransform(Type originType, Type targetType) => true;
    }
}
