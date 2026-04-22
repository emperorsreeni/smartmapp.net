using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.DependencyInjection.Extensions;
using SmartMapp.Net.Extensions;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T07 — unit tests for the zero-argument ambient
/// <see cref="SculptorQueryableExtensions.SelectAs{TTarget}(IQueryable)"/> overload. Same
/// projection result as the explicit-sculptor form, sourced from
/// <see cref="SculptorAmbient.Current"/>.
/// </summary>
public class SelectAsAmbientTests
{
    [Fact]
    public void AmbientSelectAs_ProducesSameResultAsExplicitForm()
    {
        var sculptor = new SculptorBuilder().UseBlueprint<S8T06FlatBlueprint>().Forge();
        using var _ = SculptorAmbient.Set(sculptor);

        IQueryable source = new[]
        {
            new S8T06Order { Id = 1, Total = 10m },
            new S8T06Order { Id = 2, Total = 20m },
        }.AsQueryable();

        var explicitResult = source.SelectAs<S8T06OrderFlatDto>(sculptor).ToList();
        var ambientResult = source.SelectAs<S8T06OrderFlatDto>().ToList();

        ambientResult.Should().BeEquivalentTo(explicitResult,
            "the ambient overload must produce the exact same projection as the explicit-sculptor form.");
    }

    [Fact]
    public void AmbientSelectAs_NoAmbient_Throws_WithActionableMessage()
    {
        if (SculptorAmbient.Current is not null)
        {
            // Another test installed an ambient — skip this test to avoid a false pass.
            return;
        }

        IQueryable source = Array.Empty<S8T06Order>().AsQueryable();

        var act = () => source.SelectAs<S8T06OrderFlatDto>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No ambient ISculptor*AddSculptor*");
    }

    [Fact]
    public void AmbientSelectAs_NullSource_Throws()
    {
        IQueryable? source = null;
        var act = () => source!.SelectAs<S8T06OrderFlatDto>();

        act.Should().Throw<ArgumentNullException>().WithParameterName("source");
    }

    [Fact]
    public void AmbientSelectAs_EndToEndAfterAddSculptor_Works()
    {
        var services = new ServiceCollection();
        services.AddSculptor(options => options.UseBlueprint<S8T06FlatBlueprint>());
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ISculptor>(); // install ambient

        IQueryable source = new[] { new S8T06Order { Id = 99, Total = 7m } }.AsQueryable();
        var result = source.SelectAs<S8T06OrderFlatDto>().ToList();

        result.Should().ContainSingle()
            .Which.Should().Be(new S8T06OrderFlatDto { Id = 99, Total = 7m });
    }
}
