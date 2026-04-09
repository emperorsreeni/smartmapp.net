using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Collections;

public sealed class FlattenUnflattenMappingTests
{
    private readonly TypeModelCache _typeModelCache = new();
    private readonly MappingDelegateCache _delegateCache = new();

    private Func<object, MappingScope, object> Compile(Blueprint blueprint)
    {
        var compiler = new BlueprintCompiler(_typeModelCache, _delegateCache);
        return compiler.Compile(blueprint);
    }

    private Blueprint BuildFlatteningBlueprint<TOrigin, TTarget>()
    {
        var originModel = _typeModelCache.GetOrAdd(typeof(TOrigin));
        var targetModel = _typeModelCache.GetOrAdd(typeof(TTarget));

        var convention = new FlatteningConvention(_typeModelCache);
        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            // Try exact name first
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is not null)
            {
                links.Add(new PropertyLink
                {
                    TargetMember = targetMember.MemberInfo,
                    Provider = new DirectMemberProvider(originMember.MemberInfo),
                    LinkedBy = ConventionMatch.ExactName(originMember.Name),
                });
                continue;
            }

            // Try flattening convention
            if (convention.TryLink(targetMember.MemberInfo, originModel, out var provider) && provider is not null)
            {
                var chain = originModel.GetMemberPath(targetMember.Name, t => _typeModelCache.GetOrAdd(t));
                var path = string.Join(".", chain.Select(m => m.Name));

                links.Add(new PropertyLink
                {
                    TargetMember = targetMember.MemberInfo,
                    Provider = provider,
                    LinkedBy = ConventionMatch.Flattened(path),
                });
            }
        }

        return new Blueprint
        {
            OriginType = typeof(TOrigin),
            TargetType = typeof(TTarget),
            Links = links,
        };
    }

    private Blueprint BuildUnflatteningBlueprint<TOrigin, TTarget>()
    {
        var originModel = _typeModelCache.GetOrAdd(typeof(TOrigin));
        var targetModel = _typeModelCache.GetOrAdd(typeof(TTarget));

        var unflatteningConvention = new UnflatteningConvention(_typeModelCache);
        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            // Try exact name first
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is not null)
            {
                links.Add(new PropertyLink
                {
                    TargetMember = targetMember.MemberInfo,
                    Provider = new DirectMemberProvider(originMember.MemberInfo),
                    LinkedBy = ConventionMatch.ExactName(originMember.Name),
                });
                continue;
            }

            // Try unflattening convention
            if (unflatteningConvention.TryLink(targetMember.MemberInfo, originModel, out var provider) && provider is not null)
            {
                links.Add(new PropertyLink
                {
                    TargetMember = targetMember.MemberInfo,
                    Provider = provider,
                    LinkedBy = ConventionMatch.Unflattened(targetMember.Name),
                });
            }
        }

        return new Blueprint
        {
            OriginType = typeof(TOrigin),
            TargetType = typeof(TTarget),
            Links = links,
        };
    }

    // ──────────────── Flattening Tests ────────────────

    [Fact]
    public void Flatten_1Level_MapsCustomerFirstName()
    {
        var blueprint = BuildFlatteningBlueprint<FlattenOrigin, FlattenTargetDto>();
        var mapper = Compile(blueprint);

        var origin = new FlattenOrigin
        {
            Id = 1,
            Customer = new FlattenCustomer
            {
                FirstName = "Alice",
                LastName = "Smith",
                Address = new FlattenAddress { City = "Seattle", Street = "Main St", ZipCode = "98101" },
            }
        };

        var result = (FlattenTargetDto)mapper(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.CustomerFirstName.Should().Be("Alice");
        result.CustomerLastName.Should().Be("Smith");
    }

    [Fact]
    public void Flatten_3Levels_MapsCustomerAddressCity()
    {
        var blueprint = BuildFlatteningBlueprint<FlattenOrigin, FlattenTargetDto>();
        var mapper = Compile(blueprint);

        var origin = new FlattenOrigin
        {
            Id = 1,
            Customer = new FlattenCustomer
            {
                FirstName = "Bob",
                LastName = "Jones",
                Address = new FlattenAddress { City = "Portland", Street = "Oak Ave", ZipCode = "97201" },
            }
        };

        var result = (FlattenTargetDto)mapper(origin, new MappingScope());

        result.CustomerAddressCity.Should().Be("Portland");
        result.CustomerAddressStreet.Should().Be("Oak Ave");
        result.CustomerAddressZipCode.Should().Be("97201");
    }

    [Fact]
    public void Flatten_2Levels_MapsCustomerFirstAndLastName()
    {
        var blueprint = BuildFlatteningBlueprint<FlattenOrigin, Flatten2LevelTargetDto>();
        var mapper = Compile(blueprint);

        var origin = new FlattenOrigin
        {
            Id = 1,
            Customer = new FlattenCustomer
            {
                FirstName = "Dana",
                LastName = "White",
                Address = new FlattenAddress { City = "Austin", Street = "Elm", ZipCode = "73301" },
            }
        };

        var result = (Flatten2LevelTargetDto)mapper(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.CustomerFirstName.Should().Be("Dana");
        result.CustomerLastName.Should().Be("White");
    }

    [Fact]
    public void Flatten_NullIntermediate_ReturnsDefault()
    {
        var blueprint = BuildFlatteningBlueprint<FlattenOrigin, FlattenTargetDto>();
        var mapper = Compile(blueprint);

        var origin = new FlattenOrigin
        {
            Id = 1,
            Customer = null!,
        };

        var result = (FlattenTargetDto)mapper(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.CustomerFirstName.Should().BeNull();
        result.CustomerAddressCity.Should().BeNull();
    }

    // ──────────────── Unflattening Tests ────────────────

    [Fact]
    public void Unflatten_MapsToNestedObject()
    {
        var blueprint = BuildUnflatteningBlueprint<UnflattenOriginDto, UnflattenTarget>();
        var mapper = Compile(blueprint);

        var origin = new UnflattenOriginDto
        {
            Id = 1,
            CustomerFirstName = "Alice",
            CustomerLastName = "Smith",
            CustomerAddressCity = "Seattle",
        };

        var result = (UnflattenTarget)mapper(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Customer.Should().NotBeNull();
        result.Customer.FirstName.Should().Be("Alice");
        result.Customer.LastName.Should().Be("Smith");
        result.Customer.Address.Should().NotBeNull();
        result.Customer.Address.City.Should().Be("Seattle");
    }

    // ──────────────── Mixed: flat + flattened + collection in same Blueprint (S5-T07) ────────────────

    [Fact]
    public void Mixed_FlatAndFlattenedAndCollection_InSameBlueprint()
    {
        var blueprint = BuildFlatteningBlueprint<MixedFlattenOrigin, MixedFlattenTargetDto>();
        var mapper = Compile(blueprint);

        var origin = new MixedFlattenOrigin
        {
            Id = 42,
            Name = "Mixed",
            Customer = new FlattenCustomer
            {
                FirstName = "Eve",
                LastName = "Taylor",
                Address = new FlattenAddress { City = "NYC", Street = "5th Ave", ZipCode = "10001" },
            },
            Tags = new List<int> { 1, 2, 3 }
        };

        var result = (MixedFlattenTargetDto)mapper(origin, new MappingScope());

        // Flat properties
        result.Id.Should().Be(42);
        result.Name.Should().Be("Mixed");
        // Flattened properties
        result.CustomerFirstName.Should().Be("Eve");
        result.CustomerLastName.Should().Be("Taylor");
        // Collection property
        result.Tags.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    // ──────────────── Unflattening: 3 Levels Deep (S5-T07) ────────────────

    [Fact]
    public void Unflatten_3Levels_MapsAllAddressFields()
    {
        var blueprint = BuildUnflatteningBlueprint<Unflatten3LevelOriginDto, UnflattenTarget>();
        var mapper = Compile(blueprint);

        var origin = new Unflatten3LevelOriginDto
        {
            Id = 7,
            CustomerFirstName = "Grace",
            CustomerLastName = "Hopper",
            CustomerAddressCity = "Arlington",
            CustomerAddressStreet = "Navy Blvd",
            CustomerAddressZipCode = "22201",
        };

        var result = (UnflattenTarget)mapper(origin, new MappingScope());

        result.Id.Should().Be(7);
        result.Customer.Should().NotBeNull();
        result.Customer.FirstName.Should().Be("Grace");
        result.Customer.LastName.Should().Be("Hopper");
        result.Customer.Address.Should().NotBeNull();
        result.Customer.Address.City.Should().Be("Arlington");
        result.Customer.Address.Street.Should().Be("Navy Blvd");
        result.Customer.Address.ZipCode.Should().Be("22201");
    }

    // ──────────────── Unflattening: Auto-Construct Null Intermediates (S5-T07) ────────────────

    [Fact]
    public void Unflatten_NullIntermediates_AutoConstructed()
    {
        var blueprint = BuildUnflatteningBlueprint<UnflattenOriginDto, UnflattenTargetNullInit>();
        var mapper = Compile(blueprint);

        var origin = new UnflattenOriginDto
        {
            Id = 5,
            CustomerFirstName = "Ada",
            CustomerLastName = "Lovelace",
            CustomerAddressCity = "London",
        };

        var result = (UnflattenTargetNullInit)mapper(origin, new MappingScope());

        // Customer starts as null — must be auto-constructed
        result.Id.Should().Be(5);
        result.Customer.Should().NotBeNull();
        result.Customer!.FirstName.Should().Be("Ada");
        result.Customer.LastName.Should().Be("Lovelace");
        result.Customer.Address.Should().NotBeNull();
        result.Customer.Address.City.Should().Be("London");
    }

    // ──────────────── Round-trip ────────────────

    [Fact]
    public void RoundTrip_FlattenThenUnflatten_PreservesValues()
    {
        var flattenBp = BuildFlatteningBlueprint<FlattenOrigin, FlattenTargetDto>();
        var flatten = Compile(flattenBp);

        var unflattenBp = BuildUnflatteningBlueprint<FlattenTargetDto, FlattenOrigin>();
        var unflatten = Compile(unflattenBp);

        var original = new FlattenOrigin
        {
            Id = 42,
            Customer = new FlattenCustomer
            {
                FirstName = "Charlie",
                LastName = "Brown",
                Address = new FlattenAddress { City = "Denver", Street = "Elm St", ZipCode = "80201" },
            }
        };

        var scope = new MappingScope();
        var flat = (FlattenTargetDto)flatten(original, scope);
        var roundTripped = (FlattenOrigin)unflatten(flat, new MappingScope());

        roundTripped.Id.Should().Be(42);
        roundTripped.Customer.FirstName.Should().Be("Charlie");
        roundTripped.Customer.LastName.Should().Be("Brown");
        roundTripped.Customer.Address.City.Should().Be("Denver");
    }
}
