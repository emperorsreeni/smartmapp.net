using FluentAssertions;
using NSubstitute;

namespace SmartMapp.Net.Tests.Unit;

public class MappingScopeTests
{
    [Fact]
    public void CreateChild_IncrementsDepth()
    {
        var scope = new MappingScope();
        var child = scope.CreateChild();

        scope.CurrentDepth.Should().Be(0);
        child.CurrentDepth.Should().Be(1);
    }

    [Fact]
    public void CreateChild_SharesVisitedMap()
    {
        var scope = new MappingScope();
        var origin = new object();
        var target = new object();

        scope.TrackVisited(origin, target);
        var child = scope.CreateChild();

        child.TryGetVisited(origin, out var found).Should().BeTrue();
        found.Should().BeSameAs(target);
    }

    [Fact]
    public void IsMaxDepthReached_AtBoundary()
    {
        var scope = new MappingScope { MaxDepth = 2 };

        scope.IsMaxDepthReached.Should().BeFalse();

        var child1 = scope.CreateChild();
        child1.IsMaxDepthReached.Should().BeFalse();

        var child2 = child1.CreateChild();
        child2.IsMaxDepthReached.Should().BeTrue();
    }

    [Fact]
    public void TryGetVisited_TrackVisited_RoundTrip()
    {
        var scope = new MappingScope();
        var origin = new object();
        var target = new object();

        scope.TryGetVisited(origin, out _).Should().BeFalse();

        scope.TrackVisited(origin, target);

        scope.TryGetVisited(origin, out var found).Should().BeTrue();
        found.Should().BeSameAs(target);
    }

    [Fact]
    public void TrackVisited_UsesReferenceEquality()
    {
        var scope = new MappingScope();

        // Two strings with same content but different references
        var str1 = new string("hello".ToCharArray());
        var str2 = new string("hello".ToCharArray());
        var target1 = new object();
        var target2 = new object();

        scope.TrackVisited(str1, target1);
        scope.TrackVisited(str2, target2);

        scope.TryGetVisited(str1, out var found1).Should().BeTrue();
        found1.Should().BeSameAs(target1);

        scope.TryGetVisited(str2, out var found2).Should().BeTrue();
        found2.Should().BeSameAs(target2);
    }

    [Fact]
    public void GetService_ThrowsWhenNoProvider()
    {
        var scope = new MappingScope();

        var act = () => scope.GetService<IDisposable>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no ServiceProvider*");
    }

    [Fact]
    public void GetService_ThrowsWhenServiceNotFound()
    {
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IDisposable)).Returns((object?)null);

        var scope = new MappingScope { ServiceProvider = sp };

        var act = () => scope.GetService<IDisposable>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resolve service*");
    }

    [Fact]
    public void TryGetService_ReturnsNullWhenNotFound()
    {
        var scope = new MappingScope();

        scope.TryGetService<IDisposable>().Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var scope = new MappingScope { MaxDepth = 5 };
        var origin = new object();
        scope.TrackVisited(origin, new object());
        scope.Items["key"] = "value";

        // Get to depth 1 via CreateChild, then reset the parent
        scope.Reset();

        scope.CurrentDepth.Should().Be(0);
        scope.TryGetVisited(origin, out _).Should().BeFalse();
        scope.Items.Should().BeEmpty();
    }

    [Fact]
    public void Items_CanStoreAndRetrieveData()
    {
        var scope = new MappingScope();
        scope.Items["key"] = "value";

        scope.Items["key"].Should().Be("value");
    }

    [Fact]
    public void CancellationToken_IsPropagatedToChild()
    {
        var cts = new CancellationTokenSource();
        var scope = new MappingScope { CancellationToken = cts.Token };
        var child = scope.CreateChild();

        child.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void GetService_ReturnsService_WhenRegistered()
    {
        var sp = Substitute.For<IServiceProvider>();
        var service = Substitute.For<IDisposable>();
        sp.GetService(typeof(IDisposable)).Returns(service);

        var scope = new MappingScope { ServiceProvider = sp };

        scope.GetService<IDisposable>().Should().BeSameAs(service);
    }

    [Fact]
    public void TryGetService_ReturnsService_WhenRegistered()
    {
        var sp = Substitute.For<IServiceProvider>();
        var service = Substitute.For<IDisposable>();
        sp.GetService(typeof(IDisposable)).Returns(service);

        var scope = new MappingScope { ServiceProvider = sp };

        scope.TryGetService<IDisposable>().Should().BeSameAs(service);
    }

    [Fact]
    public void CreateChild_SharesServiceProvider()
    {
        var sp = Substitute.For<IServiceProvider>();
        var scope = new MappingScope { ServiceProvider = sp };
        var child = scope.CreateChild();

        child.ServiceProvider.Should().BeSameAs(sp);
    }

    [Fact]
    public void ChildVisitedWrite_VisibleFromParent()
    {
        var scope = new MappingScope();
        var origin = new object();
        var target = new object();

        // Initiate visited map from parent
        scope.TrackVisited(new object(), new object());
        var child = scope.CreateChild();

        child.TrackVisited(origin, target);

        // Parent should see the child's write because they share the map
        scope.TryGetVisited(origin, out var found).Should().BeTrue();
        found.Should().BeSameAs(target);
    }
}
