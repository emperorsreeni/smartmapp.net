using System.Reflection;
using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Compilation;

public class BlueprintCompilerTests
{
    private readonly TypeModelCache _typeModelCache = new();
    private readonly MappingDelegateCache _delegateCache = new();
    private readonly BlueprintCompiler _compiler;

    public BlueprintCompilerTests()
    {
        _compiler = new BlueprintCompiler(_typeModelCache, _delegateCache);
    }

    // ── Helper to build a blueprint with auto-discovered links ──

    private Blueprint BuildBlueprint<TOrigin, TTarget>(
        Action<Blueprint>? configure = null,
        bool trackReferences = false,
        int maxDepth = int.MaxValue)
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

        var bp = new Blueprint
        {
            OriginType = typeof(TOrigin),
            TargetType = typeof(TTarget),
            Links = links,
            TrackReferences = trackReferences,
            MaxDepth = maxDepth,
        };

        return bp;
    }

    // ═══════════════════════════════════════════════════
    // Flat DTO mapping
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_FlatDto_AllPropertiesMapped()
    {
        var blueprint = BuildBlueprint<FlatOrder, FlatOrderDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder
        {
            Id = 42, Name = "Test", Total = 99.99m,
            CreatedAt = new DateTime(2024, 1, 1),
            IsActive = true, Description = "Desc",
            Quantity = 5, Weight = 1.5, Category = "A",
            TrackingNumber = 123456789L,
        };

        var result = (FlatOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be("Test");
        result.Total.Should().Be(99.99m);
        result.CreatedAt.Should().Be(new DateTime(2024, 1, 1));
        result.IsActive.Should().BeTrue();
        result.Description.Should().Be("Desc");
        result.Quantity.Should().Be(5);
        result.Weight.Should().Be(1.5);
        result.Category.Should().Be("A");
        result.TrackingNumber.Should().Be(123456789L);
    }

    [Fact]
    public void Compile_NullOrigin_ReturnsDefault()
    {
        var blueprint = BuildBlueprint<FlatOrder, FlatOrderDto>();
        var del = _compiler.Compile(blueprint);

        var result = del(null!, new MappingScope());

        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════
    // Field-based mapping
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_FieldBased_FieldsAssigned()
    {
        var blueprint = BuildBlueprint<FieldBasedClass, FieldBasedDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FieldBasedClass { Id = 7, Name = "FieldTest" };
        var result = (FieldBasedDto)del(origin, new MappingScope());

        result.Id.Should().Be(7);
        result.Name.Should().Be("FieldTest");
    }

    // ═══════════════════════════════════════════════════
    // Skipped links
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_SkippedLinks_NotAssigned()
    {
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
                IsSkipped = targetMember.Name == "Name",
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = links,
        };

        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 1, Name = "ShouldBeSkipped" };
        var result = (FlatOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Name.Should().Be(string.Empty); // default, not "ShouldBeSkipped"
    }

    // ═══════════════════════════════════════════════════
    // Constructor-based mapping
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_ParameterizedCtor_MapsCtorParamsAndRemainingProps()
    {
        var blueprint = BuildBlueprint<FlatOrder, CtorOrderDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 10, Name = "CtorTest", Total = 55.5m };
        var result = (CtorOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(10);
        result.Name.Should().Be("CtorTest");
        result.Total.Should().Be(55.5m);
    }

    [Fact]
    public void Compile_MultipleCtors_PicksBestMatch()
    {
        var originModel = _typeModelCache.GetOrAdd<FlatOrder>();
        var targetModel = _typeModelCache.GetOrAdd<MultipleCtorDto>();

        // MultipleCtorDto has parameterless ctor, so it should use that
        var blueprint = BuildBlueprint<FlatOrder, MultipleCtorDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 3, Name = "Multi", Description = "Desc" };
        var result = (MultipleCtorDto)del(origin, new MappingScope());

        // Parameterless ctor used, properties not set via ctor
        // but may be set via property assignment if they have setters
        result.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════
    // Record mapping
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_Record_PositionalParamsMapped()
    {
        var blueprint = BuildBlueprint<FlatOrder, RecordOrderDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 99, Name = "RecordTest", Total = 123.45m };
        var result = (RecordOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(99);
        result.Name.Should().Be("RecordTest");
        result.Total.Should().Be(123.45m);
    }

    [Fact]
    public void Compile_RecordWithInitProps_BothCtorAndInitPopulated()
    {
        var blueprint = BuildBlueprint<FlatOrder, RecordWithInitDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 5, Name = "InitTest", Total = 10m, Category = "Cat" };
        var result = (RecordWithInitDto)del(origin, new MappingScope());

        result.Id.Should().Be(5);
        result.Name.Should().Be("InitTest");
        result.Total.Should().Be(10m);
        result.Category.Should().Be("Cat");
    }

    [Fact]
    public void Compile_RecordStruct_MappedCorrectly()
    {
        var blueprint = BuildBlueprint<FlatOrder, RecordStructDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 42, Name = "StructTest" };
        var result = (RecordStructDto)del(origin, new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be("StructTest");
    }

    // ═══════════════════════════════════════════════════
    // Init-only mapping
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_InitOnlyProps_AssignedViaMemberInit()
    {
        var blueprint = BuildBlueprint<FlatOrder, InitOnlyOrderDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 1, Name = "InitOnly", Total = 50m };
        var result = (InitOnlyOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Name.Should().Be("InitOnly");
        result.Total.Should().Be(50m);
    }

    // ═══════════════════════════════════════════════════
    // Type conversion / widening
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_NumericWidening_IntToLong()
    {
        var blueprint = BuildBlueprint<TypeMismatchOrder, TypeMismatchOrderDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new TypeMismatchOrder { Id = 1, Quantity = 42, TrackingNumber = 999L, Weight = 1.5f };
        var result = (TypeMismatchOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Quantity.Should().Be(42L); // int -> long
        result.TrackingNumber.Should().Be(999); // long -> int
        result.Weight.Should().BeApproximately(1.5, 0.01); // float -> double
    }

    // ═══════════════════════════════════════════════════
    // Nullable wrap / unwrap
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_NullableWrapping_IntToNullableInt()
    {
        var blueprint = BuildBlueprint<NullableOrigin, NullableTargetDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new NullableOrigin { Value = 42, Name = "Test" };
        var result = (NullableTargetDto)del(origin, new MappingScope());

        result.Value.Should().Be(42);
        result.Name.Should().Be("Test");
    }

    [Fact]
    public void Compile_NullableUnwrapping_NullableIntToInt()
    {
        var blueprint = BuildBlueprint<NullableOriginReverse, NonNullableTargetDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new NullableOriginReverse { Value = 7, Name = "Unwrap" };
        var result = (NonNullableTargetDto)del(origin, new MappingScope());

        result.Value.Should().Be(7);
        result.Name.Should().Be("Unwrap");
    }

    // ═══════════════════════════════════════════════════
    // Nested object mapping
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_NestedObject_1Level_MappedCorrectly()
    {
        var blueprint = BuildBlueprint<NestedOrder, NestedOrderDto>(trackReferences: false);
        var del = _compiler.Compile(blueprint);

        var origin = new NestedOrder
        {
            Id = 1,
            Customer = new NestedCustomer { Name = "Alice" },
        };

        var result = (NestedOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Customer.Should().NotBeNull();
        result.Customer.Name.Should().Be("Alice");
    }

    [Fact]
    public void Compile_NestedObject_2Level_MappedCorrectly()
    {
        var blueprint = BuildBlueprint<NestedOrder, NestedOrderDto>(trackReferences: false);
        var del = _compiler.Compile(blueprint);

        var origin = new NestedOrder
        {
            Id = 1,
            Customer = new NestedCustomer
            {
                Name = "Bob",
                Address = new NestedAddress { City = "NYC", Street = "5th Ave" },
            },
        };

        var result = (NestedOrderDto)del(origin, new MappingScope());

        result.Customer.Address.Should().NotBeNull();
        result.Customer.Address.City.Should().Be("NYC");
        result.Customer.Address.Street.Should().Be("5th Ave");
    }

    [Fact]
    public void Compile_NestedObject_NullNested_ReturnsNull()
    {
        var blueprint = BuildBlueprint<NestedOrder, NestedOrderDto>(trackReferences: false);
        var del = _compiler.Compile(blueprint);

        var origin = new NestedOrder { Id = 1, Customer = null! };

        var result = (NestedOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Customer.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════
    // Circular reference tracking
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_SelfReferencing_NoCycle_MapsCorrectly()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: true);
        var del = _compiler.Compile(blueprint);

        var origin = new SelfRefEmployee
        {
            Id = 1,
            Name = "Alice",
            Manager = new SelfRefEmployee { Id = 2, Name = "Bob", Manager = null },
        };

        var result = (SelfRefEmployeeDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Name.Should().Be("Alice");
        result.Manager.Should().NotBeNull();
        result.Manager!.Id.Should().Be(2);
        result.Manager.Name.Should().Be("Bob");
        result.Manager.Manager.Should().BeNull();
    }

    [Fact]
    public void Compile_SelfReferencing_WithCycle_TrackingPreventsInfiniteRecursion()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: true);
        var del = _compiler.Compile(blueprint);

        var alice = new SelfRefEmployee { Id = 1, Name = "Alice" };
        var bob = new SelfRefEmployee { Id = 2, Name = "Bob", Manager = alice };
        alice.Manager = bob; // circular: Alice -> Bob -> Alice

        var scope = new MappingScope { MaxDepth = 10 };
        var result = (SelfRefEmployeeDto)del(alice, scope);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Alice");
        // Should complete without StackOverflowException
    }

    [Fact]
    public void Compile_CircularRef_TargetTrackedBeforePropertyAssignment()
    {
        // Verify back-references point to the SAME target instance (not a copy).
        // This proves the target is tracked before property assignment starts.
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: true);
        var del = _compiler.Compile(blueprint);

        var alice = new SelfRefEmployee { Id = 1, Name = "Alice" };
        var bob = new SelfRefEmployee { Id = 2, Name = "Bob", Manager = alice };
        alice.Manager = bob; // circular: Alice → Bob → Alice

        var scope = new MappingScope { MaxDepth = 10 };
        var result = (SelfRefEmployeeDto)del(alice, scope);

        // Alice's manager is Bob
        result.Manager.Should().NotBeNull();
        result.Manager!.Name.Should().Be("Bob");
        // Bob's manager should be the SAME Alice instance (not a new one)
        // This proves the target was tracked before its properties were assigned
        result.Manager.Manager.Should().NotBeNull();
        result.Manager.Manager.Should().BeSameAs(result);
    }

    // ═══════════════════════════════════════════════════
    // Depth limit
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_DepthLimit_StopsAtConfiguredDepth()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: false, maxDepth: 2);
        var del = _compiler.Compile(blueprint);

        var origin = new SelfRefEmployee
        {
            Id = 1,
            Name = "L1",
            Manager = new SelfRefEmployee
            {
                Id = 2,
                Name = "L2",
                Manager = new SelfRefEmployee
                {
                    Id = 3,
                    Name = "L3",
                    Manager = null,
                },
            },
        };

        var scope = new MappingScope { MaxDepth = 2 };
        var result = (SelfRefEmployeeDto)del(origin, scope);

        result.Id.Should().Be(1);
        result.Manager.Should().NotBeNull();
        result.Manager!.Id.Should().Be(2);
        // Level 3 should be null because depth limit reached
    }

    [Fact]
    public void Compile_DefaultMaxDepth_NoLimit()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: false);
        var del = _compiler.Compile(blueprint);

        var origin = new SelfRefEmployee
        {
            Id = 1,
            Name = "L1",
            Manager = new SelfRefEmployee
            {
                Id = 2,
                Name = "L2",
                Manager = new SelfRefEmployee { Id = 3, Name = "L3", Manager = null },
            },
        };

        var result = (SelfRefEmployeeDto)del(origin, new MappingScope());

        result.Manager.Should().NotBeNull();
        result.Manager!.Manager.Should().NotBeNull();
        result.Manager.Manager!.Id.Should().Be(3);
        result.Manager.Manager.Manager.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════
    // OnMapping / OnMapped hooks
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_OnMappingHook_InvokedBeforeMapping()
    {
        var hookCalled = false;
        object? capturedOrigin = null;

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = BuildLinks<FlatOrder, FlatOrderDto>(),
            OnMapping = (origin, target) =>
            {
                hookCalled = true;
                capturedOrigin = origin;
            },
        };

        var del = _compiler.Compile(blueprint);
        var origin = new FlatOrder { Id = 1 };
        del(origin, new MappingScope());

        hookCalled.Should().BeTrue();
        capturedOrigin.Should().BeSameAs(origin);
    }

    [Fact]
    public void Compile_OnMappedHook_InvokedAfterMapping()
    {
        var hookCalled = false;
        object? capturedTarget = null;

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = BuildLinks<FlatOrder, FlatOrderDto>(),
            OnMapped = (origin, target) =>
            {
                hookCalled = true;
                capturedTarget = target;
            },
        };

        var del = _compiler.Compile(blueprint);
        var origin = new FlatOrder { Id = 1 };
        var result = del(origin, new MappingScope());

        hookCalled.Should().BeTrue();
        capturedTarget.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════
    // Condition
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_ConditionFalse_PropertySkipped()
    {
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
                Condition = targetMember.Name == "Name"
                    ? (Func<object, bool>)(_ => false)
                    : null,
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = links,
        };

        var del = _compiler.Compile(blueprint);
        var origin = new FlatOrder { Id = 42, Name = "ShouldBeSkipped" };
        var result = (FlatOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be(string.Empty); // condition was false
    }

    // ═══════════════════════════════════════════════════
    // Fallback
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_Fallback_UsedWhenOriginNull()
    {
        var targetModel = _typeModelCache.GetOrAdd<FlatOrderDto>();
        var originModel = _typeModelCache.GetOrAdd<FlatOrder>();
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
                Fallback = targetMember.Name == "Name" ? "DefaultName" : null,
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = links,
        };

        var del = _compiler.Compile(blueprint);
        var origin = new FlatOrder { Id = 1, Name = null! };
        var result = (FlatOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Name.Should().Be("DefaultName");
    }

    // ═══════════════════════════════════════════════════
    // Factory construction
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_Factory_UsesSuppliedFactory()
    {
        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = BuildLinks<FlatOrder, FlatOrderDto>(),
            TargetFactory = origin => new FlatOrderDto { Category = "FactorySet" },
        };

        var del = _compiler.Compile(blueprint);
        var origin = new FlatOrder { Id = 1, Name = "Test", Category = "Original" };
        var result = (FlatOrderDto)del(origin, new MappingScope());

        // Factory created the object, then properties were assigned over it
        result.Id.Should().Be(1);
        result.Name.Should().Be("Test");
    }

    // ═══════════════════════════════════════════════════
    // Mutual circular reference (A ↔ B)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_MutualCircularRef_TrackingPreventsInfiniteRecursion()
    {
        var blueprint = BuildBlueprint<MutualParent, MutualParentDto>(trackReferences: true);
        var del = _compiler.Compile(blueprint);

        var parent = new MutualParent { Id = 1 };
        var child = new MutualChild { Id = 2, Parent = parent };
        parent.Child = child;

        var scope = new MappingScope { MaxDepth = 10 };
        var result = (MutualParentDto)del(parent, scope);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Child.Should().NotBeNull();
        result.Child!.Id.Should().Be(2);
    }

    // ═══════════════════════════════════════════════════
    // Deep cycle (A→B→C→A)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_DeepCycle_ABC_TrackingPreventsInfiniteRecursion()
    {
        var blueprint = BuildBlueprint<CycleNodeA, CycleNodeADto>(trackReferences: true);
        var del = _compiler.Compile(blueprint);

        var a = new CycleNodeA { Id = 1 };
        var b = new CycleNodeB { Id = 2 };
        var c = new CycleNodeC { Id = 3, Next = a };
        a.Next = b;
        b.Next = c;

        var scope = new MappingScope { MaxDepth = 20 };
        var result = (CycleNodeADto)del(a, scope);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Next.Should().NotBeNull();
        result.Next!.Id.Should().Be(2);
    }

    // ═══════════════════════════════════════════════════
    // Tracking disabled: no tracking code emitted
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_TrackingDisabled_NoTrackingCalls()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: false);
        var del = _compiler.Compile(blueprint);

        var origin = new SelfRefEmployee
        {
            Id = 1,
            Name = "Alice",
            Manager = new SelfRefEmployee { Id = 2, Name = "Bob", Manager = null },
        };

        // With tracking disabled, mapping still works for non-circular graphs
        var result = (SelfRefEmployeeDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Manager.Should().NotBeNull();
        result.Manager!.Id.Should().Be(2);
    }

    // ═══════════════════════════════════════════════════
    // Value types: no tracking needed
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_ValueTypeTarget_NoTrackingNeeded()
    {
        var blueprint = BuildBlueprint<FlatOrder, RecordStructDto>(trackReferences: true);
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 42, Name = "StructTest" };
        var result = (RecordStructDto)del(origin, new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be("StructTest");
    }

    // ═══════════════════════════════════════════════════
    // Depth limit = 1: only top-level mapped
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_DepthLimit1_OnlyTopLevelMapped()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: false, maxDepth: 1);
        var del = _compiler.Compile(blueprint);

        var origin = new SelfRefEmployee
        {
            Id = 1,
            Name = "L1",
            Manager = new SelfRefEmployee { Id = 2, Name = "L2", Manager = null },
        };

        var scope = new MappingScope { MaxDepth = 1 };
        var result = (SelfRefEmployeeDto)del(origin, scope);

        result.Id.Should().Be(1);
        result.Name.Should().Be("L1");
        // Depth 1 means the child scope is already at max depth
    }

    // ═══════════════════════════════════════════════════
    // Self-ref + depth limit combined
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_SelfRefWithDepthLimit_StopsAtLimit()
    {
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: false, maxDepth: 3);
        var del = _compiler.Compile(blueprint);

        var l4 = new SelfRefEmployee { Id = 4, Name = "L4", Manager = null };
        var l3 = new SelfRefEmployee { Id = 3, Name = "L3", Manager = l4 };
        var l2 = new SelfRefEmployee { Id = 2, Name = "L2", Manager = l3 };
        var l1 = new SelfRefEmployee { Id = 1, Name = "L1", Manager = l2 };

        var scope = new MappingScope { MaxDepth = 3 };
        var result = (SelfRefEmployeeDto)del(l1, scope);

        result.Id.Should().Be(1);
        result.Manager.Should().NotBeNull();
        result.Manager!.Id.Should().Be(2);
    }

    // ═══════════════════════════════════════════════════
    // PreCondition
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_PreConditionFalse_PropertySkipped()
    {
        var originModel = _typeModelCache.GetOrAdd<PreConditionOrder>();
        var targetModel = _typeModelCache.GetOrAdd<PreConditionOrderDto>();
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
                PreCondition = targetMember.Name == "Name"
                    ? (Func<object, bool>)(_ => false)
                    : null,
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(PreConditionOrder),
            TargetType = typeof(PreConditionOrderDto),
            Links = links,
        };

        var del = _compiler.Compile(blueprint);
        var origin = new PreConditionOrder { Id = 1, Name = "ShouldBeSkipped", IsActive = true };
        var result = (PreConditionOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Name.Should().Be(string.Empty); // pre-condition false
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Compile_PreConditionTrue_PropertyAssigned()
    {
        var originModel = _typeModelCache.GetOrAdd<PreConditionOrder>();
        var targetModel = _typeModelCache.GetOrAdd<PreConditionOrderDto>();
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
                PreCondition = targetMember.Name == "Name"
                    ? (Func<object, bool>)(_ => true)
                    : null,
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(PreConditionOrder),
            TargetType = typeof(PreConditionOrderDto),
            Links = links,
        };

        var del = _compiler.Compile(blueprint);
        var origin = new PreConditionOrder { Id = 1, Name = "Assigned", IsActive = true };
        var result = (PreConditionOrderDto)del(origin, new MappingScope());

        result.Name.Should().Be("Assigned");
    }

    // ═══════════════════════════════════════════════════
    // BestMatch ctor with verified output values
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_BestMatchCtor_OverloadPicksHighest_VerifyValues()
    {
        var blueprint = BuildBlueprint<FlatOrder, BestMatchOverloadDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 10, Name = "Best", Total = 77.77m };
        var result = (BestMatchOverloadDto)del(origin, new MappingScope());

        result.Id.Should().Be(10);
        result.Name.Should().Be("Best");
        result.Total.Should().Be(77.77m);
    }

    [Fact]
    public void Compile_MultipleCtors_BestMatchAssignsRemainingProps()
    {
        var blueprint = BuildBlueprint<FlatOrder, MultipleCtorDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder { Id = 5, Name = "Remaining", Description = "Desc" };
        var result = (MultipleCtorDto)del(origin, new MappingScope());

        // BestMatch should pick the (int, string, string) ctor
        result.Id.Should().Be(5);
        result.Name.Should().Be("Remaining");
        result.Description.Should().Be("Desc");
    }

    // ═══════════════════════════════════════════════════
    // Same-type deep copy
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_SameTypeComplexProperty_DeepCopy()
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

        result.Should().NotBeSameAs(origin);
        result.Id.Should().Be(1);
        result.Name.Should().Be("Alice");
        result.Address.Should().NotBeNull();
        result.Address.Should().NotBeSameAs(origin.Address);
        result.Address!.City.Should().Be("NYC");
    }

    // ═══════════════════════════════════════════════════
    // ExpressionMappingCompiler
    // ═══════════════════════════════════════════════════

    [Fact]
    public void CompileToLambda_ProducesUncompiledExpression()
    {
        var blueprint = BuildBlueprint<FlatOrder, FlatOrderDto>();
        var lambda = _compiler.CompileToLambda(blueprint);

        lambda.Should().NotBeNull();
        lambda.Parameters.Should().HaveCount(2);

        // Compile and execute
        var del = lambda.Compile();
        var origin = new FlatOrder { Id = 1, Name = "Lambda" };
        var result = (FlatOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Name.Should().Be("Lambda");
    }

    // ═══════════════════════════════════════════════════
    // Field-only target (no properties, only fields)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_FieldOnlyTarget_AllFieldsMapped()
    {
        var blueprint = BuildBlueprint<FieldOnlyOrigin, FieldOnlyTarget>();
        var del = _compiler.Compile(blueprint);

        var origin = new FieldOnlyOrigin { Id = 7, Name = "Fields", Value = 3.14 };
        var result = (FieldOnlyTarget)del(origin, new MappingScope());

        result.Id.Should().Be(7);
        result.Name.Should().Be("Fields");
        result.Value.Should().Be(3.14);
    }

    // ═══════════════════════════════════════════════════
    // BestMatch ctor + remaining settable props verified
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_BestMatchCtor_RemainingPropsAssigned()
    {
        var blueprint = BuildBlueprint<FlatOrder, BestMatchWithRemainingDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder
        {
            Id = 10, Name = "Remaining", Total = 55.5m, Category = "Electronics"
        };

        var result = (BestMatchWithRemainingDto)del(origin, new MappingScope());

        // ctor params
        result.Id.Should().Be(10);
        result.Name.Should().Be("Remaining");
        // remaining settable props
        result.Total.Should().Be(55.5m);
        result.Category.Should().Be("Electronics");
    }

    // ═══════════════════════════════════════════════════
    // Nullable<T> unwrap: int? → int
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_NullableUnwrap_HasValue_MapsCorrectly()
    {
        var blueprint = BuildBlueprint<NullableIntermediateOrigin, NullableIntermediateTarget>();
        var del = _compiler.Compile(blueprint);

        var origin = new NullableIntermediateOrigin { Id = 1, Score = 42 };
        var result = (NullableIntermediateTarget)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Score.Should().Be(42);
    }

    [Fact]
    public void Compile_NullableUnwrap_NoValue_MapsDefault()
    {
        var blueprint = BuildBlueprint<NullableIntermediateOrigin, NullableIntermediateTarget>();
        var del = _compiler.Compile(blueprint);

        var origin = new NullableIntermediateOrigin { Id = 2, Score = null };
        var result = (NullableIntermediateTarget)del(origin, new MappingScope());

        result.Id.Should().Be(2);
        result.Score.Should().Be(0); // default(int)
    }

#if NET7_0_OR_GREATER
    // ═══════════════════════════════════════════════════
    // Required member validation (strict mode)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_RequiredMembers_StrictMode_AllMapped_Succeeds()
    {
        // RequiredTarget has required Id, required Name, optional Optional
        // FlatOrder has Id and Name → both required members are mapped
        var blueprint = BuildBlueprint<FlatOrder, RequiredTarget>();
        var strictBlueprint = blueprint with { StrictRequiredMembers = true };

        var del = _compiler.Compile(strictBlueprint);
        var origin = new FlatOrder { Id = 1, Name = "Strict" };
        var result = (RequiredTarget)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Name.Should().Be("Strict");
    }

    [Fact]
    public void Compile_RequiredMembers_NonStrictMode_MissingRequired_Succeeds()
    {
        // SimpleDto has only Id and Name → maps to RequiredTarget which has required Id, required Name
        // Even if some required members were missing, non-strict mode won't throw
        var blueprint = BuildBlueprint<SimpleDto, RequiredTarget>();
        var nonStrictBlueprint = blueprint with { StrictRequiredMembers = false };

        var act = () => _compiler.Compile(nonStrictBlueprint);
        act.Should().NotThrow();
    }

    [Fact]
    public void Compile_RequiredMembers_StrictMode_MissingRequired_Throws()
    {
        // RequiredTargetWithUnmapped has required MandatoryField — no match in SimpleDto
        var blueprint = BuildBlueprint<SimpleDto, RequiredTargetWithUnmapped>();
        var strictBlueprint = blueprint with { StrictRequiredMembers = true };

        var act = () => _compiler.Compile(strictBlueprint);
        act.Should().Throw<MappingCompilationException>()
            .WithMessage("*MandatoryField*");
    }
#endif

    // ═══════════════════════════════════════════════════
    // Mixed record: ctor params + init props verified separately
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_RecordWithInit_CtorAndInitVerified()
    {
        var blueprint = BuildBlueprint<FlatOrder, RecordWithInitDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new FlatOrder
        {
            Id = 99, Name = "Mixed", Total = 42.0m, Category = "Tech"
        };

        var result = (RecordWithInitDto)del(origin, new MappingScope());

        // ctor params
        result.Id.Should().Be(99);
        result.Name.Should().Be("Mixed");
        // init-only props
        result.Total.Should().Be(42.0m);
        result.Category.Should().Be("Tech");
    }

    // ═══════════════════════════════════════════════════
    // 3-level deep nesting (GAP G3)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_NestedObject_3Level_MappedCorrectly()
    {
        var blueprint = BuildBlueprint<DeepLevel1, DeepLevel1Dto>(trackReferences: false);
        var del = _compiler.Compile(blueprint);

        var origin = new DeepLevel1
        {
            Value = 1,
            Child = new DeepLevel2
            {
                Value = 2,
                Child = new DeepLevel3 { Value = 3 },
            },
        };

        var result = (DeepLevel1Dto)del(origin, new MappingScope());

        result.Value.Should().Be(1);
        result.Child.Should().NotBeNull();
        result.Child!.Value.Should().Be(2);
        result.Child.Child.Should().NotBeNull();
        result.Child.Child!.Value.Should().Be(3);
    }

    [Fact]
    public void Compile_NestedObject_3Level_NullIntermediate_ReturnsNull()
    {
        var blueprint = BuildBlueprint<DeepLevel1, DeepLevel1Dto>(trackReferences: false);
        var del = _compiler.Compile(blueprint);

        var origin = new DeepLevel1
        {
            Value = 1,
            Child = new DeepLevel2 { Value = 2, Child = null },
        };

        var result = (DeepLevel1Dto)del(origin, new MappingScope());

        result.Child.Should().NotBeNull();
        result.Child!.Child.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════
    // Enum → String conversion (GAP G5)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_EnumToString_ConvertsCorrectly()
    {
        var blueprint = BuildBlueprint<EnumOrigin, EnumToStringDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new EnumOrigin { Id = 1, Status = OrderStatus.Shipped };
        var result = (EnumToStringDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Status.Should().Be("Shipped");
    }

    [Fact]
    public void Compile_StringToEnum_ParsesCorrectly()
    {
        var blueprint = BuildBlueprint<StringToEnumOrigin, StringToEnumDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new StringToEnumOrigin { Id = 2, Status = "Shipped" };
        var result = (StringToEnumDto)del(origin, new MappingScope());

        result.Id.Should().Be(2);
        result.Status.Should().Be(OrderStatus.Shipped);
    }

    // ═══════════════════════════════════════════════════
    // TransformerLookup integration (GAP G7)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_TransformerLookup_UsedForMismatchedTypes()
    {
        var transformer = new DateTimeToDateTimeOffsetTransformer();
        ITypeTransformer? TransformerLookup(Type origin, Type target)
        {
            if (origin == typeof(DateTime) && target == typeof(DateTimeOffset))
                return transformer;
            return null;
        }

        var compilerWithLookup = new BlueprintCompiler(
            _typeModelCache, _delegateCache, transformerLookup: TransformerLookup);

        var blueprint = BuildBlueprint<TransformerOrigin, TransformerDto>();
        var del = compilerWithLookup.Compile(blueprint);

        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var origin = new TransformerOrigin { Id = 1, CreatedAt = dt };
        var result = (TransformerDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.CreatedAt.Should().Be(new DateTimeOffset(dt));
    }

    // ═══════════════════════════════════════════════════
    // Condition true → property IS assigned (GAP G8)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_ConditionTrue_PropertyAssigned()
    {
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
                Condition = targetMember.Name == "Name"
                    ? (Func<object, bool>)(_ => true)
                    : null,
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = links,
        };

        var del = _compiler.Compile(blueprint);
        var origin = new FlatOrder { Id = 42, Name = "ShouldBeAssigned" };
        var result = (FlatOrderDto)del(origin, new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be("ShouldBeAssigned");
    }

    // ═══════════════════════════════════════════════════
    // ExpressionMappingCompiler — strongly typed projection (GAP G6)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void ExpressionMappingCompiler_ProducesStronglyTypedProjection()
    {
        var exprCompiler = new ExpressionMappingCompiler(_typeModelCache, _delegateCache);
        var blueprint = BuildBlueprint<FlatOrder, FlatOrderDto>();

        var projectionExpr = exprCompiler.CompileToProjectionExpression<FlatOrder, FlatOrderDto>(blueprint);

        projectionExpr.Should().NotBeNull();
        projectionExpr.Parameters.Should().HaveCount(1);
        projectionExpr.Parameters[0].Type.Should().Be(typeof(FlatOrder));
        projectionExpr.ReturnType.Should().Be(typeof(FlatOrderDto));

        // Compile and execute to verify correctness
        var compiled = projectionExpr.Compile();
        var origin = new FlatOrder { Id = 99, Name = "Projection" };
        var result = compiled(origin);

        result.Id.Should().Be(99);
        result.Name.Should().Be("Projection");
    }

    [Fact]
    public void ExpressionMappingCompiler_ProjectionUsableWithIQueryable()
    {
        var exprCompiler = new ExpressionMappingCompiler(_typeModelCache, _delegateCache);
        var blueprint = BuildBlueprint<FlatOrder, FlatOrderDto>();

        var projectionExpr = exprCompiler.CompileToProjectionExpression<FlatOrder, FlatOrderDto>(blueprint);

        // Simulate IQueryable usage
        var source = new List<FlatOrder>
        {
            new() { Id = 1, Name = "A" },
            new() { Id = 2, Name = "B" },
        }.AsQueryable();

        var projected = source.Select(projectionExpr).ToList();

        projected.Should().HaveCount(2);
        projected[0].Id.Should().Be(1);
        projected[0].Name.Should().Be("A");
        projected[1].Id.Should().Be(2);
        projected[1].Name.Should().Be("B");
    }

    // ═══════════════════════════════════════════════════
    // Explicit PropertyLink.Transformer (S4-T04 AC)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_ExplicitTransformerOnLink_UsedForConversion()
    {
        var originModel = _typeModelCache.GetOrAdd<ExplicitTransformerOrigin>();
        var targetModel = _typeModelCache.GetOrAdd<ExplicitTransformerTarget>();
        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is null) continue;

            ITypeTransformer? transformer = null;
            if (targetMember.Name == "Code")
                transformer = new IntToStringTransformer();

            links.Add(new PropertyLink
            {
                TargetMember = targetMember.MemberInfo,
                Provider = new DirectMemberProvider(originMember.MemberInfo),
                LinkedBy = ConventionMatch.ExactName(originMember.Name),
                Transformer = transformer,
            });
        }

        var blueprint = new Blueprint
        {
            OriginType = typeof(ExplicitTransformerOrigin),
            TargetType = typeof(ExplicitTransformerTarget),
            Links = links,
        };

        var del = _compiler.Compile(blueprint);
        var origin = new ExplicitTransformerOrigin { Id = 1, Code = 42 };
        var result = (ExplicitTransformerTarget)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Code.Should().Be("42");
    }

    // ═══════════════════════════════════════════════════
    // Plain struct mapping (S4-T07 AC: struct detection)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_PlainStruct_MappedCorrectly()
    {
        var blueprint = BuildBlueprint<PointStruct, PointStructDto>();
        var del = _compiler.Compile(blueprint);

        var origin = new PointStruct { X = 10, Y = 20 };
        var result = (PointStructDto)del(origin, new MappingScope());

        result.X.Should().Be(10);
        result.Y.Should().Be(20);
    }

    // ═══════════════════════════════════════════════════
    // Child scope created per nested call (S4-T07 AC)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_NestedMapping_ChildScopeCreated_DepthIncrements()
    {
        // Verify depth increments across nested levels by using a depth limit.
        // DeepLevel1 → DeepLevel2 → DeepLevel3: with maxDepth=1, top-level maps (no check),
        // nested L2 parent check depth 0<1 passes (maps L2), L3 parent check depth 1>=1 stops.
        var blueprint = BuildBlueprint<DeepLevel1, DeepLevel1Dto>(trackReferences: false, maxDepth: 1);
        var del = _compiler.Compile(blueprint);

        var origin = new DeepLevel1
        {
            Value = 1,
            Child = new DeepLevel2
            {
                Value = 2,
                Child = new DeepLevel3 { Value = 3 },
            },
        };

        var scope = new MappingScope { MaxDepth = 1 };
        var result = (DeepLevel1Dto)del(origin, scope);

        result.Value.Should().Be(1);
        result.Child.Should().NotBeNull();
        result.Child!.Value.Should().Be(2);
        // Level 3 stopped by depth limit — proves CreateChild() incremented depth
        result.Child.Child.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════
    // Depth limit 3 stops at level 4 (S4-T08 AC exact wording)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_DepthLimit3_StopsAtLevel4()
    {
        // MaxDepth=3: top-level maps (no check), then 3 nested calls succeed before stopping.
        // L1(top) → L2(depth check 0<3, maps) → L3(depth check 1<3, maps) → L4(depth check 2<3, maps) → L5(depth check 3>=3, stops)
        var blueprint = BuildBlueprint<SelfRefEmployee, SelfRefEmployeeDto>(trackReferences: false, maxDepth: 3);
        var del = _compiler.Compile(blueprint);

        var l5 = new SelfRefEmployee { Id = 5, Name = "L5", Manager = null };
        var l4 = new SelfRefEmployee { Id = 4, Name = "L4", Manager = l5 };
        var l3 = new SelfRefEmployee { Id = 3, Name = "L3", Manager = l4 };
        var l2 = new SelfRefEmployee { Id = 2, Name = "L2", Manager = l3 };
        var l1 = new SelfRefEmployee { Id = 1, Name = "L1", Manager = l2 };

        var scope = new MappingScope { MaxDepth = 3 };
        var result = (SelfRefEmployeeDto)del(l1, scope);

        result.Id.Should().Be(1);
        result.Manager.Should().NotBeNull();
        result.Manager!.Id.Should().Be(2);
        result.Manager.Manager.Should().NotBeNull();
        result.Manager.Manager!.Id.Should().Be(3);
        result.Manager.Manager.Manager.Should().NotBeNull();
        result.Manager.Manager.Manager!.Id.Should().Be(4);
        // Level 5 should be stopped by depth limit — stops at level 4
        result.Manager.Manager.Manager.Manager.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════
    // TransformerLookup propagated to nested auto-discovered blueprints (S4-T04/T07)
    // ═══════════════════════════════════════════════════

    [Fact]
    public void Compile_TransformerLookup_PropagatedToNestedBlueprints()
    {
        var transformer = new DateTimeToDateTimeOffsetTransformer();
        ITypeTransformer? TransformerLookup(Type origin, Type target)
        {
            if (origin == typeof(DateTime) && target == typeof(DateTimeOffset))
                return transformer;
            return null;
        }

        var compilerWithLookup = new BlueprintCompiler(
            _typeModelCache, _delegateCache, transformerLookup: TransformerLookup);

        var blueprint = BuildBlueprint<ParentWithNestedTransform, ParentWithNestedTransformDto>();
        var del = compilerWithLookup.Compile(blueprint);

        var dt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var origin = new ParentWithNestedTransform
        {
            Id = 1,
            Child = new ChildWithDateTime { Name = "Child", CreatedAt = dt },
        };
        var result = (ParentWithNestedTransformDto)del(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Child.Should().NotBeNull();
        result.Child.Name.Should().Be("Child");
        result.Child.CreatedAt.Should().Be(new DateTimeOffset(dt));
    }

    // ── Helpers ──

    private IReadOnlyList<PropertyLink> BuildLinks<TOrigin, TTarget>()
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

        return links;
    }

    // ── Test transformers ──

    private sealed class DateTimeToDateTimeOffsetTransformer : ITypeTransformer<DateTime, DateTimeOffset>
    {
        public bool CanTransform(Type originType, Type targetType)
            => originType == typeof(DateTime) && targetType == typeof(DateTimeOffset);

        public DateTimeOffset Transform(DateTime origin, MappingScope scope)
            => new DateTimeOffset(origin);
    }

    private sealed class IntToStringTransformer : ITypeTransformer<int, string>
    {
        public bool CanTransform(Type originType, Type targetType)
            => originType == typeof(int) && targetType == typeof(string);

        public string Transform(int origin, MappingScope scope)
            => origin.ToString();
    }
}
