using FluentAssertions;
using NSubstitute;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Tests.Unit;

public class AbstractionTests
{
    [Fact]
    public void ITypeTransformer_CanBeImplemented()
    {
        var transformer = Substitute.For<ITypeTransformer<string, int>>();
        transformer.CanTransform(typeof(string), typeof(int)).Returns(true);
        transformer.Transform("42", Arg.Any<MappingScope>()).Returns(42);

        transformer.CanTransform(typeof(string), typeof(int)).Should().BeTrue();
        transformer.Transform("42", new MappingScope()).Should().Be(42);
    }

    [Fact]
    public void IValueProvider_CanBeImplemented()
    {
        var provider = Substitute.For<IValueProvider>();
        provider.Provide(Arg.Any<object>(), Arg.Any<object>(), "Name", Arg.Any<MappingScope>())
            .Returns("TestValue");

        var result = provider.Provide(new object(), new object(), "Name", new MappingScope());
        result.Should().Be("TestValue");
    }

    [Fact]
    public async Task IMappingFilter_CanBeImplemented()
    {
        var filter = Substitute.For<IMappingFilter>();
        filter.ApplyAsync(Arg.Any<MappingContext>(), Arg.Any<MappingDelegate>())
            .Returns(Task.FromResult<object?>("filtered"));

        var context = new MappingContext
        {
            OriginType = typeof(string),
            TargetType = typeof(int),
            Origin = "test",
            Scope = new MappingScope()
        };

        var result = await filter.ApplyAsync(context, _ => Task.FromResult<object?>(null));
        result.Should().Be("filtered");
    }

    [Fact]
    public void MappingContext_CanBeConstructed()
    {
        var scope = new MappingScope();
        var context = new MappingContext
        {
            OriginType = typeof(string),
            TargetType = typeof(int),
            Origin = "hello",
            Target = 42,
            Scope = scope
        };

        context.OriginType.Should().Be(typeof(string));
        context.TargetType.Should().Be(typeof(int));
        context.Origin.Should().Be("hello");
        context.Target.Should().Be(42);
        context.Scope.Should().BeSameAs(scope);
        context.Items.Should().BeEmpty();
    }
}
