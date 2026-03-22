using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Compilation;

/// <summary>
/// End-to-end integration tests combining multiple Sprint 4 features:
/// construction + assignment + null-safe + transformer + circular refs + depth limits.
/// </summary>
public class EndToEndCompilationTests
{
    private readonly TypeModelCache _typeModelCache = new();
    private readonly MappingDelegateCache _delegateCache = new();
    private readonly BlueprintCompiler _compiler;

    public EndToEndCompilationTests()
    {
        _compiler = new BlueprintCompiler(_typeModelCache, _delegateCache);
    }

    private Blueprint BuildBlueprint<TOrigin, TTarget>(
        bool trackReferences = false, int maxDepth = int.MaxValue)
    {
        var originModel = _typeModelCache.GetOrAdd<TOrigin>();
        var targetModel = _typeModelCache.GetOrAdd<TTarget>();
        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is null) continue;

            links.Add(new PropertyLink
            {
                TargetMember = targetMember.MemberInfo,
                Provider = new DirectMemberProvider(originMember.MemberInfo),
                LinkedBy = ConventionMatch.ExactName(originMember.Name),
            });
        }

        return new Blueprint
        {
            OriginType = typeof(TOrigin),
            TargetType = typeof(TTarget),
            Links = links,
            TrackReferences = trackReferences,
            MaxDepth = maxDepth,
        };
    }

    // ═══════════════════════════════════════════════════
    // E2E: Nested + Hooks + Conditions combined
    // ═══════════════════════════════════════════════════

    [Fact]
    public void EndToEnd_NestedWithHooksAndConditions()
    {
        var onMappingCalled = false;
        var onMappedCalled = false;

        var originModel = _typeModelCache.GetOrAdd<NestedOrder>();
        var targetModel = _typeModelCache.GetOrAdd<NestedOrderDto>();
        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is null) continue;

            links.Add(new PropertyLink
            {
                TargetMember = targetMember.MemberInfo,
                Provider = new DirectMemberProvider(originMember.MemberInfo),
                LinkedBy = ConventionMatch.ExactName(originMember.Name),
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(NestedOrder),
            TargetType = typeof(NestedOrderDto),
            Links = links,
            OnMapping = (o, t) => onMappingCalled = true,
            OnMapped = (o, t) => onMappedCalled = true,
        };

        var del = _compiler.Compile(blueprint);
        var origin = new NestedOrder
        {
            Id = 1,
            Customer = new NestedCustomer
            {
                Name = "Alice",
                Address = new NestedAddress { City = "NYC", Street = "5th Ave" },
            },
        };

        var result = (NestedOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Customer.Should().NotBeNull();
        result.Customer.Name.Should().Be("Alice");
        result.Customer.Address.Should().NotBeNull();
        result.Customer.Address.City.Should().Be("NYC");
        onMappingCalled.Should().BeTrue();
        onMappedCalled.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════
    // E2E: Circular ref + Depth limit combined
    // ═══════════════════════════════════════════════════

    [Fact]
    public void EndToEnd_CircularRefWithDepthLimit()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(
            trackReferences: true, maxDepth: 5);
        var del = _compiler.Compile(blueprint);

        var alice = new SelfRefEmployee { Id = 1, Name = "Alice" };
        var bob = new SelfRefEmployee { Id = 2, Name = "Bob", Manager = alice };
        alice.Manager = bob;

        var scope = new MappingScope { MaxDepth = 5 };
        var result = (SelfRefEmployeeDto)del(alice, scope);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Alice");
    }

    // ═══════════════════════════════════════════════════
    // E2E: Same-type deep copy
    // ═══════════════════════════════════════════════════

    [Fact]
    public void EndToEnd_SameTypeDeepCopy()
    {
        var blueprint = BuildBlueprint<SameTypePerson, SameTypePerson>();
        var del = _compiler.Compile(blueprint);

        var origin = new SameTypePerson
        {
            Id = 1,
            Name = "Alice",
            Address = new SameTypeAddress { City = "NYC", Street = "5th Ave" },
        };

        var result = (SameTypePerson)del(origin, new MappingScope());

        result.Should().NotBeNull();
        result.Should().NotBeSameAs(origin);
        result.Id.Should().Be(1);
        result.Name.Should().Be("Alice");
        result.Address.Should().NotBeNull();
        result.Address.Should().NotBeSameAs(origin.Address);
        result.Address!.City.Should().Be("NYC");
        result.Address.Street.Should().Be("5th Ave");
    }

    // ═══════════════════════════════════════════════════
    // E2E: Record ctor + init props + type widening combined
    // ═══════════════════════════════════════════════════

    [Fact]
    public void EndToEnd_RecordCtorPlusInitPlusWidening()
    {
        var blueprint = BuildBlueprint<FlatOrder, RecordWithInitDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder
        {
            Id = 42, Name = "Combined", Total = 99.99m, Category = "Electronics",
        };

        var result = (RecordWithInitDto)del(origin, new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be("Combined");
        result.Total.Should().Be(99.99m);
        result.Category.Should().Be("Electronics");
    }

    // ═══════════════════════════════════════════════════
    // E2E: Blueprint resolver for nested types
    // ═══════════════════════════════════════════════════

    [Fact]
    public void EndToEnd_BlueprintResolverUsedForNestedTypes()
    {
        var resolverCalled = false;
        Blueprint? Resolver(TypePair pair)
        {
            if (pair.OriginType == typeof(NestedCustomer) && pair.TargetType == typeof(NestedCustomerDto))
            {
                resolverCalled = true;
                var om = _typeModelCache.GetOrAdd<NestedCustomer>();
                var tm = _typeModelCache.GetOrAdd<NestedCustomerDto>();
                var links = new List<PropertyLink>();
                foreach (var targetMember in tm.WritableMembers)
                {
                    var originMember = om.GetMember(targetMember.Name);
                    if (originMember is null) continue;
                    links.Add(new PropertyLink
                    {
                        TargetMember = targetMember.MemberInfo,
                        Provider = new DirectMemberProvider(originMember.MemberInfo),
                        LinkedBy = ConventionMatch.ExactName(originMember.Name),
                    });
                }
                return new Blueprint
                {
                    OriginType = typeof(NestedCustomer),
                    TargetType = typeof(NestedCustomerDto),
                    Links = links,
                };
            }
            return null;
        }

        var compiler = new BlueprintCompiler(_typeModelCache, _delegateCache, Resolver);
        var blueprint = BuildBlueprint<NestedOrder, NestedOrderDto>();
        var del = compiler.Compile(blueprint);

        var origin = new NestedOrder
        {
            Id = 1,
            Customer = new NestedCustomer { Name = "Bob" },
        };

        var result = (NestedOrderDto)del(origin, new MappingScope());

        result.Customer.Should().NotBeNull();
        result.Customer.Name.Should().Be("Bob");
        resolverCalled.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════
    // E2E: Fallback + Condition + Hooks + Nested combined
    // ═══════════════════════════════════════════════════

    [Fact]
    public void EndToEnd_MultipleFeaturesCombined()
    {
        var hookValues = new List<string>();

        var originModel = _typeModelCache.GetOrAdd<FlatOrder>();
        var targetModel = _typeModelCache.GetOrAdd<FlatOrderDto>();
        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is null) continue;

            links.Add(new PropertyLink
            {
                TargetMember = targetMember.MemberInfo,
                Provider = new DirectMemberProvider(originMember.MemberInfo),
                LinkedBy = ConventionMatch.ExactName(originMember.Name),
                Fallback = targetMember.Name == "Description" ? "N/A" : null,
                Condition = targetMember.Name == "Category"
                    ? (Func<object, bool>)(o => ((FlatOrder)o).IsActive)
                    : null,
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = links,
            OnMapping = (o, t) => hookValues.Add("mapping"),
            OnMapped = (o, t) => hookValues.Add("mapped"),
        };

        var del = _compiler.Compile(blueprint);
        var origin = new FlatOrder
        {
            Id = 1, Name = "Test", IsActive = false,
            Description = null!, Category = "Electronics",
        };

        var result = (FlatOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Description.Should().Be("N/A"); // fallback used
        result.Category.Should().Be(string.Empty); // condition false, not assigned
        hookValues.Should().ContainInOrder("mapping", "mapped");
    }
}
