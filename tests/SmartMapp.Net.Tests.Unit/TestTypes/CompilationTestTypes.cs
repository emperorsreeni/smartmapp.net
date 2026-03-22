namespace SmartMapp.Net.Tests.Unit.TestTypes;

// ── Flat DTO types for expression compiler tests ──

public class FlatOrder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double Weight { get; set; }
    public string Category { get; set; } = string.Empty;
    public long TrackingNumber { get; set; }
}

public class FlatOrderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double Weight { get; set; }
    public string Category { get; set; } = string.Empty;
    public long TrackingNumber { get; set; }
}

// ── Constructor-based DTOs ──

public class CtorOrderDto
{
    public int Id { get; }
    public string Name { get; }
    public decimal Total { get; set; }

    public CtorOrderDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

public class MultipleCtorDto
{
    public int Id { get; }
    public string Name { get; }
    public string? Description { get; }

    public MultipleCtorDto(int id)
    {
        Id = id;
        Name = string.Empty;
    }

    public MultipleCtorDto(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public MultipleCtorDto(int id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }
}

public class NoMatchCtorDto
{
    public int Foo { get; }
    public string Bar { get; }

    public NoMatchCtorDto(int zzz, string yyy)
    {
        Foo = zzz;
        Bar = yyy;
    }
}

// ── Record types ──

public record RecordOrderDto(int Id, string Name, decimal Total);

public record RecordWithInitDto(int Id, string Name)
{
    public decimal Total { get; init; }
    public string Category { get; init; } = string.Empty;
}

public record struct RecordStructDto(int Id, string Name);

// ── Init-only types ──

public class InitOnlyOrderDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Total { get; init; }
}

// ── Nested types ──

public class NestedOrder
{
    public int Id { get; set; }
    public NestedCustomer Customer { get; set; } = new();
}

public class NestedCustomer
{
    public string Name { get; set; } = string.Empty;
    public NestedAddress Address { get; set; } = new();
}

public class NestedAddress
{
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
}

public class NestedOrderDto
{
    public int Id { get; set; }
    public NestedCustomerDto Customer { get; set; } = new();
}

public class NestedCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public NestedAddressDto Address { get; set; } = new();
}

public class NestedAddressDto
{
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
}

// ── Self-referencing type ──

public class SelfRefEmployee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SelfRefEmployee? Manager { get; set; }
}

public class SelfRefEmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SelfRefEmployeeDto? Manager { get; set; }
}

// ── Mutual circular reference ──

public class MutualParent
{
    public int Id { get; set; }
    public MutualChild? Child { get; set; }
}

public class MutualChild
{
    public int Id { get; set; }
    public MutualParent? Parent { get; set; }
}

public class MutualParentDto
{
    public int Id { get; set; }
    public MutualChildDto? Child { get; set; }
}

public class MutualChildDto
{
    public int Id { get; set; }
    public MutualParentDto? Parent { get; set; }
}

// ── Type mismatch types (transformer needed) ──

public class TypeMismatchOrder
{
    public int Id { get; set; }
    public int Quantity { get; set; }
    public long TrackingNumber { get; set; }
    public float Weight { get; set; }
}

public class TypeMismatchOrderDto
{
    public int Id { get; set; }
    public long Quantity { get; set; } // int -> long widening
    public int TrackingNumber { get; set; } // long -> int narrowing
    public double Weight { get; set; } // float -> double widening
}

// ── Field-based types ──

public class FieldBasedClass
{
    public int Id;
    public string Name = string.Empty;
}

public class FieldBasedDto
{
    public int Id;
    public string Name = string.Empty;
}

// ── Nullable types ──

public class NullableOrigin
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class NullableTargetDto
{
    public int? Value { get; set; }
    public string? Name { get; set; }
}

public class NullableOriginReverse
{
    public int? Value { get; set; }
    public string? Name { get; set; }
}

public class NonNullableTargetDto
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ── Deeply nested for depth limit tests ──

public class DeepLevel1
{
    public int Value { get; set; }
    public DeepLevel2? Child { get; set; }
}

public class DeepLevel2
{
    public int Value { get; set; }
    public DeepLevel3? Child { get; set; }
}

public class DeepLevel3
{
    public int Value { get; set; }
    public DeepLevel4? Child { get; set; }
}

public class DeepLevel4
{
    public int Value { get; set; }
}

public class DeepLevel1Dto
{
    public int Value { get; set; }
    public DeepLevel2Dto? Child { get; set; }
}

public class DeepLevel2Dto
{
    public int Value { get; set; }
    public DeepLevel3Dto? Child { get; set; }
}

public class DeepLevel3Dto
{
    public int Value { get; set; }
    public DeepLevel4Dto? Child { get; set; }
}

public class DeepLevel4Dto
{
    public int Value { get; set; }
}

// ── Required member types ──

#if NET7_0_OR_GREATER
public class RequiredTarget
{
    public required int Id { get; set; }
    public required string Name { get; set; }
    public string? Optional { get; set; }
}
#endif

// ── Deep cycle A→B→C→A types ──

public class CycleNodeA
{
    public int Id { get; set; }
    public CycleNodeB? Next { get; set; }
}

public class CycleNodeB
{
    public int Id { get; set; }
    public CycleNodeC? Next { get; set; }
}

public class CycleNodeC
{
    public int Id { get; set; }
    public CycleNodeA? Next { get; set; }
}

public class CycleNodeADto
{
    public int Id { get; set; }
    public CycleNodeBDto? Next { get; set; }
}

public class CycleNodeBDto
{
    public int Id { get; set; }
    public CycleNodeCDto? Next { get; set; }
}

public class CycleNodeCDto
{
    public int Id { get; set; }
    public CycleNodeADto? Next { get; set; }
}

// ── Same-type mapping (deep copy) ──

public class SameTypePerson
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SameTypeAddress? Address { get; set; }
}

public class SameTypeAddress
{
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
}

// ── Best-match ctor with overloads and type-scoring ──

public class BestMatchOverloadDto
{
    public int Id { get; }
    public string Name { get; }
    public decimal Total { get; }

    public BestMatchOverloadDto(int id)
    {
        Id = id;
        Name = string.Empty;
    }

    public BestMatchOverloadDto(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public BestMatchOverloadDto(int id, string name, decimal total)
    {
        Id = id;
        Name = name;
        Total = total;
    }
}

// ── PreCondition test type ──

public class PreConditionOrder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class PreConditionOrderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// ── BestMatch zero-match with multiple ctors (no parameterless) ──

public class ZeroMatchMultiCtorDto
{
    public int Aaa { get; }
    public string Bbb { get; }

    public ZeroMatchMultiCtorDto(int xxx)
    {
        Aaa = xxx;
        Bbb = string.Empty;
    }

    public ZeroMatchMultiCtorDto(int xxx, string yyy)
    {
        Aaa = xxx;
        Bbb = yyy;
    }
}

// ── BestMatch ctor + remaining settable props ──

public class BestMatchWithRemainingDto
{
    public int Id { get; }
    public string Name { get; }
    public decimal Total { get; set; }
    public string Category { get; set; } = string.Empty;

    public BestMatchWithRemainingDto(int id, string name)
    {
        Id = id;
        Name = name;
    }
}

// ── Field-only target (no properties) ──

public class FieldOnlyOrigin
{
    public int Id;
    public string Name = string.Empty;
    public double Value;
}

public class FieldOnlyTarget
{
    public int Id;
    public string Name = string.Empty;
    public double Value;
}

// ── Nullable<T> origin for NullSafe HasValue guard ──

public class NullableIntermediateOrigin
{
    public int Id { get; set; }
    public int? Score { get; set; }
}

public class NullableIntermediateTarget
{
    public int Id { get; set; }
    public int Score { get; set; }
}
