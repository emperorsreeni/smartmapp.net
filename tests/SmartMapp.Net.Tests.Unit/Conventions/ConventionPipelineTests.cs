using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class ConventionPipelineTests
{
    private readonly TypeModelCache _cache = new();

    private ConventionPipeline CreateDefaultPipeline() => ConventionPipeline.CreateDefault(_cache);

    [Fact]
    public void BuildLinks_SimpleDto_AllMembersLinked()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(ExactSource), typeof(ExactTarget));

        links.Should().HaveCount(3); // Id, Name, Email
        links.Should().OnlyContain(l => !l.IsSkipped);
    }

    [Fact]
    public void BuildLinks_FlattenedDto_FlattensCorrectly()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(FlatteningOrder), typeof(FlatOrderDto1));

        links.Should().HaveCount(4); // Id, CustomerFirstName, CustomerLastName, Status

        var idLink = links.First(l => l.TargetMember.Name == "Id");
        idLink.IsSkipped.Should().BeFalse();
        idLink.LinkedBy.ConventionName.Should().Be("ExactNameConvention");

        var custFirstLink = links.First(l => l.TargetMember.Name == "CustomerFirstName");
        custFirstLink.IsSkipped.Should().BeFalse();
        custFirstLink.LinkedBy.ConventionName.Should().Be("FlatteningConvention");
    }

    [Fact]
    public void BuildLinks_UnlinkedMember_IsSkipped()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(PipelineOrder), typeof(PipelineOrderDto));

        var nonExistent = links.First(l => l.TargetMember.Name == "NonExistent");
        nonExistent.IsSkipped.Should().BeTrue();
        nonExistent.LinkedBy.ConventionName.Should().Be("None");
        nonExistent.LinkedBy.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void BuildLinks_ConventionPriority_FirstMatchWins()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(ExactSource), typeof(ExactTarget));

        // "Name" should be matched by ExactNameConvention (priority 100), not CaseConvention (200)
        var nameLink = links.First(l => l.TargetMember.Name == "Name");
        nameLink.LinkedBy.ConventionName.Should().Be("ExactNameConvention");
    }

    [Fact]
    public void BuildLinks_MethodToProperty_LinksGetTotal()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(PipelineOrder), typeof(PipelineOrderDto));

        var totalLink = links.First(l => l.TargetMember.Name == "Total");
        totalLink.IsSkipped.Should().BeFalse();
        totalLink.LinkedBy.ConventionName.Should().Be("MethodToPropertyConvention");
    }

    [Fact]
    public void BuildLinks_ConventionMatchRecordsConventionName()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(ExactSource), typeof(ExactTarget));

        foreach (var link in links)
        {
            link.LinkedBy.Should().NotBeNull();
            link.LinkedBy.ConventionName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void BuildLinks_EmptyTarget_ReturnsEmptyList()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(ExactSource), typeof(EmptyClass));

        links.Should().BeEmpty();
    }

    [Fact]
    public void BuildLinks_NoConventions_AllSkipped()
    {
        var pipeline = new ConventionPipeline(
            Array.Empty<IPropertyConvention>(),
            _cache,
            new StructuralSimilarityScorer());

        var links = pipeline.BuildLinks(typeof(ExactSource), typeof(ExactTarget));

        links.Should().HaveCount(3);
        links.Should().OnlyContain(l => l.IsSkipped);
    }

    [Fact]
    public void BuildLinks_SingleConvention_OnlyThatConventionMatches()
    {
        var pipeline = new ConventionPipeline(
            new IPropertyConvention[] { new ExactNameConvention() },
            _cache,
            new StructuralSimilarityScorer());

        var links = pipeline.BuildLinks(typeof(ExactSource), typeof(ExactTarget));

        links.Should().HaveCount(3);
        links.Where(l => !l.IsSkipped).Should().OnlyContain(l =>
            l.LinkedBy.ConventionName == "ExactNameConvention");
    }

    [Fact]
    public void BuildLinks_EndToEnd_OrderToOrderDto()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(PipelineOrder), typeof(PipelineOrderDto));

        // Id — ExactName
        links.First(l => l.TargetMember.Name == "Id").IsSkipped.Should().BeFalse();
        // Name — ExactName
        links.First(l => l.TargetMember.Name == "Name").IsSkipped.Should().BeFalse();
        // CustomerFirstName — Flattening
        links.First(l => l.TargetMember.Name == "CustomerFirstName").IsSkipped.Should().BeFalse();
        // CustomerLastName — Flattening
        links.First(l => l.TargetMember.Name == "CustomerLastName").IsSkipped.Should().BeFalse();
        // Total — MethodToProperty
        links.First(l => l.TargetMember.Name == "Total").IsSkipped.Should().BeFalse();
        // NonExistent — Skipped
        links.First(l => l.TargetMember.Name == "NonExistent").IsSkipped.Should().BeTrue();
    }

    [Fact]
    public void BuildLinks_EndToEnd_FlatOrderDto2()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(FlatteningOrder), typeof(FlatOrderDto2));

        links.Should().HaveCount(3); // Id, CustomerAddressCity, CustomerAddressStreet

        var cityLink = links.First(l => l.TargetMember.Name == "CustomerAddressCity");
        cityLink.IsSkipped.Should().BeFalse();
        cityLink.LinkedBy.ConventionName.Should().Be("FlatteningConvention");
        cityLink.LinkedBy.OriginMemberPath.Should().Be("Customer.Address.City");
    }

    [Fact]
    public void CreateDefault_IncludesAllBuiltInConventions()
    {
        var pipeline = CreateDefaultPipeline();

        pipeline.Conventions.Should().HaveCount(7);
        pipeline.Conventions.Select(c => c.GetType()).Should().Contain(new[]
        {
            typeof(ExactNameConvention),
            typeof(CaseConvention),
            typeof(PrefixDroppingConvention),
            typeof(MethodToPropertyConvention),
            typeof(FlatteningConvention),
            typeof(UnflatteningConvention),
            typeof(AbbreviationConvention),
        });
    }

    [Fact]
    public void Conventions_AreSortedByPriority()
    {
        var pipeline = CreateDefaultPipeline();
        var priorities = pipeline.Conventions.Select(c => c.Priority).ToList();

        priorities.Should().BeInAscendingOrder();
    }

    [Fact]
    public void StrictMode_ThrowsOnUnlinkedRequiredMember()
    {
        var pipeline = CreateDefaultPipeline();
        pipeline.StrictMode = true;

        var act = () => pipeline.BuildLinks(typeof(StrictSource), typeof(StrictTarget));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*required*Code*");
    }

    [Fact]
    public void StrictMode_DoesNotThrowWhenAllRequiredLinked()
    {
        var pipeline = CreateDefaultPipeline();
        pipeline.StrictMode = true;

        // ExactSource → ExactTarget: all members link
        var act = () => pipeline.BuildLinks(typeof(ExactSource), typeof(ExactTarget));

        act.Should().NotThrow();
    }

    [Fact]
    public void IgnoreUnlinked_FiltersOutSkippedLinks()
    {
        var pipeline = CreateDefaultPipeline();
        pipeline.IgnoreUnlinked = true;

        var links = pipeline.BuildLinks(typeof(PartialSource), typeof(PartialTarget));

        // Only "Id" should remain; Missing1 and Missing2 are filtered out
        links.Should().HaveCount(1);
        links[0].TargetMember.Name.Should().Be("Id");
        links[0].IsSkipped.Should().BeFalse();
    }

    [Fact]
    public void IgnoreUnlinked_False_IncludesSkippedLinks()
    {
        var pipeline = CreateDefaultPipeline();
        pipeline.IgnoreUnlinked = false;

        var links = pipeline.BuildLinks(typeof(PartialSource), typeof(PartialTarget));

        links.Should().HaveCount(3); // Id + Missing1 + Missing2
        links.Count(l => l.IsSkipped).Should().Be(2);
    }

    [Fact]
    public void BuildLinks_GoldenTest_OrderToOrderDto_WithValues()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(PipelineOrder), typeof(PipelineOrderDto));

        var order = new PipelineOrder
        {
            Id = 42,
            Name = "Test Order",
            Customer = new PipelineCustomer
            {
                FirstName = "Alice",
                LastName = "Smith",
            },
        };

        // Verify actual values through providers
        var idLink = links.First(l => l.TargetMember.Name == "Id");
        idLink.Provider.Provide(order, null!, "Id", new MappingScope()).Should().Be(42);

        var nameLink = links.First(l => l.TargetMember.Name == "Name");
        nameLink.Provider.Provide(order, null!, "Name", new MappingScope()).Should().Be("Test Order");

        var custFirst = links.First(l => l.TargetMember.Name == "CustomerFirstName");
        custFirst.Provider.Provide(order, null!, "CustomerFirstName", new MappingScope()).Should().Be("Alice");

        var custLast = links.First(l => l.TargetMember.Name == "CustomerLastName");
        custLast.Provider.Provide(order, null!, "CustomerLastName", new MappingScope()).Should().Be("Smith");

        var total = links.First(l => l.TargetMember.Name == "Total");
        total.Provider.Provide(order, null!, "Total", new MappingScope()).Should().Be(99.99m);
    }

    [Fact]
    public void BuildLinks_GoldenTest_OrderToFlatOrderDto1_WithValues()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(FlatteningOrder), typeof(FlatOrderDto1));

        var order = new FlatteningOrder
        {
            Id = 1,
            Status = "Active",
            Customer = new FlatteningCustomer
            {
                FirstName = "Bob",
                LastName = "Jones",
            },
        };

        links.First(l => l.TargetMember.Name == "Id")
            .Provider.Provide(order, null!, "Id", new MappingScope()).Should().Be(1);
        links.First(l => l.TargetMember.Name == "Status")
            .Provider.Provide(order, null!, "Status", new MappingScope()).Should().Be("Active");
        links.First(l => l.TargetMember.Name == "CustomerFirstName")
            .Provider.Provide(order, null!, "CustomerFirstName", new MappingScope()).Should().Be("Bob");
        links.First(l => l.TargetMember.Name == "CustomerLastName")
            .Provider.Provide(order, null!, "CustomerLastName", new MappingScope()).Should().Be("Jones");
    }

    [Fact]
    public void BuildLinks_MixedConventions_AllTypesLinked()
    {
        var pipeline = CreateDefaultPipeline();
        var links = pipeline.BuildLinks(typeof(MixedConventionSource), typeof(MixedConventionTarget));

        // Id → ExactNameConvention
        var idLink = links.First(l => l.TargetMember.Name == "Id");
        idLink.IsSkipped.Should().BeFalse();
        idLink.LinkedBy.ConventionName.Should().Be("ExactNameConvention");

        // FirstName ← first_name → CaseConvention
        var fnLink = links.First(l => l.TargetMember.Name == "FirstName");
        fnLink.IsSkipped.Should().BeFalse();
        fnLink.LinkedBy.ConventionName.Should().Be("CaseConvention");

        // Age ← GetAge() → MethodToPropertyConvention
        var ageLink = links.First(l => l.TargetMember.Name == "Age");
        ageLink.IsSkipped.Should().BeFalse();
        ageLink.LinkedBy.ConventionName.Should().Be("MethodToPropertyConvention");

        // Address ← Addr → AbbreviationConvention
        var addrLink = links.First(l => l.TargetMember.Name == "Address");
        addrLink.IsSkipped.Should().BeFalse();
        addrLink.LinkedBy.ConventionName.Should().Be("AbbreviationConvention");

        // InfoCity ← Info.City → FlatteningConvention
        var cityLink = links.First(l => l.TargetMember.Name == "InfoCity");
        cityLink.IsSkipped.Should().BeFalse();
        cityLink.LinkedBy.ConventionName.Should().Be("FlatteningConvention");
    }

    [Fact]
    public async Task BuildLinks_ThreadSafe_LocalStateOnly()
    {
        // BuildLinks should use only local state — safe to call from multiple threads
        var pipeline = CreateDefaultPipeline();

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() =>
                pipeline.BuildLinks(typeof(ExactSource), typeof(ExactTarget))))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var links in results)
        {
            links.Should().HaveCount(3);
            links.Should().OnlyContain(l => !l.IsSkipped);
        }
    }
}
