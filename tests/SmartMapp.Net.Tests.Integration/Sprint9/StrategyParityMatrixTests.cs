// SPDX-License-Identifier: MIT
// Sprint 9 · S9-T11 — strategy-ladder parity matrix.
// Same blueprint, same input, run under every StrategyMode; assert byte-identical output and
// that the IL Emit pipeline actually fires when it was supposed to (no silent fall-back).

using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Configuration;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.Sprint9;

public sealed class StrategyParityMatrixTests
{
    public sealed class Source
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public sealed class Target
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    private sealed class FlatBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan) => plan.Bind<Source, Target>();
    }

    private static Source MakeSource(int seed) => new()
    {
        Id = seed,
        Name = $"Name-{seed}",
        Quantity = seed * 3,
        Price = seed * 9.99m,
    };

    private static ISculptor Forge(StrategyMode mode) =>
        new SculptorBuilder()
            .Configure(opt =>
            {
                opt.Strategy.Mode = mode;
                opt.UseBlueprint<FlatBlueprint>();
            })
            .Forge();

    [Theory]
    [InlineData(StrategyMode.CompiledOnly, MappingStrategy.ExpressionCompiled)]
    [InlineData(StrategyMode.EmitFirst, MappingStrategy.ILEmit)]
    [InlineData(StrategyMode.Adaptive, MappingStrategy.ExpressionCompiled)] // pre-promotion
    [InlineData(StrategyMode.EmitOnly, MappingStrategy.ILEmit)]
    public void ActiveStrategy_reflects_mode(StrategyMode mode, MappingStrategy expected)
    {
        var sculptor = Forge(mode);
        // Triggers compile and records ActiveStrategy.
        var _ = sculptor.Map<Source, Target>(MakeSource(1));
        var inspect = sculptor.Inspect<Source, Target>();
        inspect.ActiveStrategy.Should().Be(expected);
    }

    [Fact]
    public void Mapping_output_identical_across_all_modes()
    {
        var input = Enumerable.Range(1, 100).Select(MakeSource).ToArray();

        var baseline = Forge(StrategyMode.CompiledOnly)
            .MapAll<Source, Target>(input)
            .Select(t => (t.Id, t.Name, t.Quantity, t.Price))
            .ToArray();

        foreach (var mode in new[] { StrategyMode.EmitFirst, StrategyMode.Adaptive, StrategyMode.EmitOnly })
        {
            var actual = Forge(mode)
                .MapAll<Source, Target>(input)
                .Select(t => (t.Id, t.Name, t.Quantity, t.Price))
                .ToArray();

            actual.Should().Equal(baseline, $"Strategy mode {mode} must produce byte-identical output vs CompiledOnly");
        }
    }

    [Fact]
    public async Task Adaptive_promotion_swaps_to_ILEmit_after_threshold()
    {
        var sculptor = new SculptorBuilder()
            .Configure(opt =>
            {
                opt.Strategy.Mode = StrategyMode.Adaptive;
                opt.Strategy.PromotionThreshold = 3; // trigger fast
                opt.UseBlueprint<FlatBlueprint>();
            })
            .Forge();

        var input = MakeSource(42);
        // Pre-promotion: ExpressionCompiled.
        sculptor.Map<Source, Target>(input);
        var pre = sculptor.Inspect<Source, Target>();
        pre.ActiveStrategy.Should().Be(MappingStrategy.ExpressionCompiled);
        pre.PromotionState.Should().Be(SmartMapp.Net.Engine.Promotion.PromotionState.Cold);

        // Cross the threshold (3 more calls puts count at threshold = 3 inside Observe).
        for (var i = 0; i < 4; i++) sculptor.Map<Source, Target>(input);

        // Give the BG worker time to drain.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        SmartMapp.Net.Engine.Promotion.PromotionState? observed = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var ins = sculptor.Inspect<Source, Target>();
            observed = ins.PromotionState;
            if (observed == SmartMapp.Net.Engine.Promotion.PromotionState.Promoted)
                break;
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        observed.Should().Be(
            SmartMapp.Net.Engine.Promotion.PromotionState.Promoted,
            "the background promotion worker should swap in the IL-Emit delegate within 5 s of crossing the threshold");

        var post = sculptor.Inspect<Source, Target>();
        post.ActiveStrategy.Should().Be(MappingStrategy.ILEmit);
        post.LastPromotedAt.Should().NotBeNull();

        // Functional parity post-swap.
        var output = sculptor.Map<Source, Target>(MakeSource(7));
        output.Id.Should().Be(7);
        output.Name.Should().Be("Name-7");
        output.Quantity.Should().Be(21);
        output.Price.Should().Be(7 * 9.99m);
    }

    [Fact]
    public void Concurrent_mapping_during_promotion_returns_correct_results()
    {
        var sculptor = new SculptorBuilder()
            .Configure(opt =>
            {
                opt.Strategy.Mode = StrategyMode.Adaptive;
                opt.Strategy.PromotionThreshold = 5;
                opt.UseBlueprint<FlatBlueprint>();
            })
            .Forge();

        const int threadCount = 8;
        const int callsPerThread = 2000;
        var bag = new System.Collections.Concurrent.ConcurrentBag<(int seed, int id, string name)>();

        Parallel.For(0, threadCount, t =>
        {
            for (var i = 0; i < callsPerThread; i++)
            {
                var seed = t * callsPerThread + i;
                var output = sculptor.Map<Source, Target>(MakeSource(seed));
                bag.Add((seed, output.Id, output.Name));
            }
        });

        bag.Should().HaveCount(threadCount * callsPerThread);
        foreach (var (seed, id, name) in bag)
        {
            id.Should().Be(seed);
            name.Should().Be($"Name-{seed}");
        }
    }
}
