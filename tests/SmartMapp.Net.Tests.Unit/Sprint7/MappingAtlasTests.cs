using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Diagnostics;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class MappingAtlasTests
{
    [Fact]
    public void Build_ProducesNodes_AndEdges_PerBlueprint()
    {
        var sculptor = ForgeTwoPair();
        var atlas = sculptor.GetMappingAtlas();

        atlas.Blueprints.Should().HaveCount(2);
        atlas.Edges.Should().HaveCount(2);
        atlas.Nodes.Should().HaveCountGreaterThanOrEqualTo(3); // 2 origins + 2 targets (duplicates possible → ≥3)

        atlas.Nodes.Should().Contain(n => n.ClrType == typeof(Sprint7Order));
        atlas.Nodes.Should().Contain(n => n.ClrType == typeof(Sprint7OrderDto));
    }

    [Fact]
    public void Atlas_Cached_SameInstance_OnRepeatedCalls()
    {
        var sculptor = ForgeTwoPair();
        var first = sculptor.GetMappingAtlas();
        var second = sculptor.GetMappingAtlas();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetOutgoing_ReturnsEdges_OriginatingFromType()
    {
        var sculptor = ForgeTwoPair();
        var atlas = sculptor.GetMappingAtlas();

        var outgoing = atlas.GetOutgoing(typeof(Sprint7Order)).ToList();
        outgoing.Should().Contain(e => e.Pair.TargetType == typeof(Sprint7OrderDto));
    }

    [Fact]
    public void GetIncoming_ReturnsEdges_TargetingType()
    {
        var sculptor = ForgeTwoPair();
        var atlas = sculptor.GetMappingAtlas();

        var incoming = atlas.GetIncoming(typeof(Sprint7OrderDto)).ToList();
        incoming.Should().Contain(e => e.Pair.OriginType == typeof(Sprint7Order));
    }

    [Fact]
    public void GetNeighbors_UnionsOutgoing_AndIncoming()
    {
        var sculptor = ForgeTwoPair();
        var atlas = sculptor.GetMappingAtlas();

        var neighbors = atlas.GetNeighbors(typeof(Sprint7Order)).ToList();
        neighbors.Should().Contain(e => e.Pair.TargetType == typeof(Sprint7OrderDto));
    }

    [Fact]
    public void ToDotFormat_ProducesValidGraphvizStructure()
    {
        var sculptor = ForgeTwoPair();
        var dot = sculptor.GetMappingAtlas().ToDotFormat();

        dot.Should().StartWith("digraph SmartMappNet {");
        dot.Should().Contain("rankdir=LR;");
        dot.Should().Contain("\"SmartMapp.Net.Tests.Unit.Sprint7.Sprint7Order\"");
        dot.Should().Contain("\"SmartMapp.Net.Tests.Unit.Sprint7.Sprint7OrderDto\"");
        dot.Should().Contain("label=\"ExpressionCompiled,");
        dot.Should().EndWith("}");
    }

    [Fact]
    public void ToDotFormat_EscapesQuotesInLabels()
    {
        var nodes = new MappingAtlasNode[]
        {
            new() { ClrType = typeof(string), Label = "Has\"Quote" },
        };
        var edges = Array.Empty<MappingAtlasEdge>();

        var dot = DotFormatWriterAdapter.Write(nodes, edges);
        dot.Should().Contain("\\\"Quote");
    }

    [Fact]
    public void Build_NullBlueprints_Throws()
    {
        var act = () => MappingAtlas.Build(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_EmptyBlueprints_ReturnsEmptyAtlas()
    {
        var atlas = MappingAtlas.Build(Array.Empty<Blueprint>());
        atlas.Blueprints.Should().BeEmpty();
        atlas.Nodes.Should().BeEmpty();
        atlas.Edges.Should().BeEmpty();
        atlas.ToDotFormat().Should().StartWith("digraph SmartMappNet {");
    }

    private static ISculptor ForgeTwoPair()
    {
        var b = new SculptorBuilder();
        b.Bind<Sprint7Order, Sprint7OrderDto>();
        b.Bind<Sprint7Customer, Sprint7OrderDto>();
        return b.Forge();
    }
}

/// <summary>
/// Test-only adapter exposing the internal <c>DotFormatWriter</c> through a friend assembly.
/// </summary>
internal static class DotFormatWriterAdapter
{
    internal static string Write(
        IReadOnlyList<MappingAtlasNode> nodes,
        IReadOnlyList<MappingAtlasEdge> edges)
        => typeof(MappingAtlas).Assembly
            .GetType("SmartMapp.Net.Diagnostics.DotFormatWriter")!
            .GetMethod("Write", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { nodes, edges }) as string ?? string.Empty;
}
