namespace SmartMapp.Net.Tests.Unit.TestTypes;

// ============================================================
// Types for ExactNameConvention tests
// ============================================================

public class ExactSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ExactTarget
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

// Case-insensitive match target
public class CaseInsensitiveTarget
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string EMAIL { get; set; } = string.Empty;
}

// Class with both field and property of same name (different case)
public class FieldAndPropertySource
{
    public int Id;
    public string Name { get; set; } = string.Empty;
    public int Code { get; set; }
}

// ============================================================
// Types for CaseConvention / NameNormalizer tests
// ============================================================

public class SnakeCaseSource
{
    public string first_name { get; set; } = string.Empty;
    public string last_name { get; set; } = string.Empty;
    public int user_age { get; set; }
}

public class PascalCaseTarget
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int UserAge { get; set; }
}

public class CamelCaseSource
{
    public string firstName { get; set; } = string.Empty;
    public string lastName { get; set; } = string.Empty;
}

public class ScreamingSnakeSource
{
    public string FIRST_NAME { get; set; } = string.Empty;
    public string LAST_NAME { get; set; } = string.Empty;
}

public class KebabStyleSource
{
    // Note: C# doesn't allow hyphens in identifiers, so we use underscores
    // and rely on NameNormalizer for actual kebab tests
}

// ============================================================
// Types for FlatteningConvention tests
// ============================================================

public class FlatteningOrder
{
    public int Id { get; set; }
    public FlatteningCustomer Customer { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}

public class FlatteningCustomer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public FlatteningAddress Address { get; set; } = new();
}

public class FlatteningAddress
{
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public FlatteningStreetDetail StreetDetail { get; set; } = new();
}

public class FlatteningStreetDetail
{
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
}

// 1-level flat
public class FlatOrderDto1
{
    public int Id { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// 2-level flat
public class FlatOrderDto2
{
    public int Id { get; set; }
    public string CustomerAddressCity { get; set; } = string.Empty;
    public string CustomerAddressStreet { get; set; } = string.Empty;
}

// 3-level flat
public class FlatOrderDto3
{
    public int Id { get; set; }
    public string CustomerAddressStreetDetailLine1 { get; set; } = string.Empty;
}

// Non-decomposable target
public class NonFlatDto
{
    public int Id { get; set; }
    public string FooBarBaz { get; set; } = string.Empty;
}

// ============================================================
// Types for UnflatteningConvention tests
// ============================================================

public class UnflatSource
{
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
    public int Id { get; set; }
}

public class UnflatTarget
{
    public int Id { get; set; }
    public FlatteningCustomer Customer { get; set; } = new();
}

// ============================================================
// Types for PrefixDroppingConvention tests
// ============================================================

public class PrefixSource
{
    public string GetName { get; set; } = string.Empty;
    public int m_id { get; set; }
    public string _description { get; set; } = string.Empty;
    public string StrCustomerName { get; set; } = string.Empty;
}

public class PrefixTarget
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
}

public class SuffixTarget
{
    public string NameField { get; set; } = string.Empty;
    public string DescriptionProperty { get; set; } = string.Empty;
    public string StatusProp { get; set; } = string.Empty;
}

public class SuffixSource
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

// ============================================================
// Types for MethodToPropertyConvention tests
// ============================================================

public class MethodSource
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    private decimal _total = 100m;

    public string GetFullName() => $"{FirstName} {LastName}";
    public decimal GetTotal() => _total;
    public int ComputedAge() => 25; // no "Get" prefix, but matches by exact name

    // These should NOT be matched:
    public void DoSomething() { }                    // void
    public string GetValue(int id) => id.ToString(); // has parameter
}

public class MethodTarget
{
    public string FullName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int ComputedAge { get; set; }
}

// ============================================================
// Types for AbbreviationConvention tests
// ============================================================

public class AbbreviatedSource
{
    public string Addr { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal Amt { get; set; }
}

public class ExpandedTarget
{
    public string Address { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}

public class ExpandedSource
{
    public string Address { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class AbbreviatedTarget
{
    public string Addr { get; set; } = string.Empty;
    public int Qty { get; set; }
}

// ============================================================
// Types for StructuralSimilarityScorer tests
// ============================================================

public class ScoreSourceFull
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ScoreTargetFull
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ScoreTargetPartial
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unknown1 { get; set; } = string.Empty;
    public string Unknown2 { get; set; } = string.Empty;
}

public class ScoreTargetNone
{
    public string Foo { get; set; } = string.Empty;
    public string Bar { get; set; } = string.Empty;
    public int Baz { get; set; }
}

public class EmptyClass { }

// ============================================================
// Types for NameSuffixTypeConvention tests
// ============================================================

public class Product
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ProductEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Completely different — should NOT pair with Product
public class InvoiceDto
{
    public int InvoiceNumber { get; set; }
    public DateTime DueDate { get; set; }
}

// ============================================================
// Types for ConventionPipeline integration tests
// ============================================================

public class PipelineOrder
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PipelineCustomer Customer { get; set; } = new();
    public decimal GetTotal() => 99.99m;
}

public class PipelineCustomer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class PipelineOrderDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string NonExistent { get; set; } = string.Empty;
}

// ============================================================
// Types for backtracking tests (FlatteningConvention)
// ============================================================

// Ambiguous: "StatusCode" could be "Status" + "Code" or "StatusCode"
public class BacktrackSource
{
    public int Id { get; set; }
    public BacktrackStatus Status { get; set; } = new();
}

public class BacktrackStatus
{
    public int Code { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class BacktrackFlatDto
{
    public int Id { get; set; }
    public int StatusCode { get; set; }
    public string StatusText { get; set; } = string.Empty;
}

// ============================================================
// Types for 3-level unflattening tests
// ============================================================

public class Unflatten3LevelSource
{
    public string CustomerAddressCity { get; set; } = string.Empty;
    public string CustomerAddressStreet { get; set; } = string.Empty;
    public string CustomerFirstName { get; set; } = string.Empty;
    public int Id { get; set; }
}

public class Unflatten3LevelTarget
{
    public int Id { get; set; }
    public FlatteningCustomer Customer { get; set; } = new();
}

// ============================================================
// Types for read-only intermediate tests
// ============================================================

public class ReadOnlyIntermediateSource
{
    public string DataValue { get; set; } = string.Empty;
}

public class ReadOnlyIntermediate
{
    public string Value { get; } = string.Empty; // read-only — no setter
}

public class ReadOnlyIntermediateTarget
{
    public ReadOnlyIntermediate Data { get; set; } = new();
}

// ============================================================
// Types for multi-segment abbreviation tests
// ============================================================

public class MultiSegmentAbbrSource
{
    public string CustAddr { get; set; } = string.Empty;
}

public class MultiSegmentAbbrTarget
{
    public string CustomerAddress { get; set; } = string.Empty;
}

// ============================================================
// Types for StrictMode tests
// ============================================================

public class StrictTarget
{
    public required string Name { get; set; }
    public required int Code { get; set; }
    public string? Optional { get; set; }
}

public class StrictSource
{
    public string Name { get; set; } = string.Empty;
    // Code is NOT present — should cause strict mode failure
}

// ============================================================
// Types for IgnoreUnlinked tests
// ============================================================

public class PartialSource
{
    public int Id { get; set; }
}

public class PartialTarget
{
    public int Id { get; set; }
    public string Missing1 { get; set; } = string.Empty;
    public string Missing2 { get; set; } = string.Empty;
}

// ============================================================
// Types for ExactNameConvention — DateTime match test
// ============================================================

public class DateTimeTarget
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================================
// Types for max-depth flattening test (6 levels deep — exceeds MaxDepth=5)
// ============================================================

public class DepthLevel1
{
    public DepthLevel2 L2 { get; set; } = new();
}

public class DepthLevel2
{
    public DepthLevel3 L3 { get; set; } = new();
}

public class DepthLevel3
{
    public DepthLevel4 L4 { get; set; } = new();
}

public class DepthLevel4
{
    public DepthLevel5 L5 { get; set; } = new();
}

public class DepthLevel5
{
    public DepthLevel6 L6 { get; set; } = new();
}

public class DepthLevel6
{
    public DepthLevel7 L7 { get; set; } = new();
}

public class DepthLevel7
{
    public string Value { get; set; } = string.Empty;
}

public class DepthFlatDto
{
    public string L2L3L4L5L6L7Value { get; set; } = string.Empty;
}

// ============================================================
// Types for case-insensitive flattening prefix match test
// ============================================================

public class CaseInsensitiveFlatSource
{
    public int Id { get; set; }
    public CaseInsensitiveFlatNested customer { get; set; } = new(); // lowercase "customer"
}

public class CaseInsensitiveFlatNested
{
    public string Name { get; set; } = string.Empty;
}

public class CaseInsensitiveFlatTarget
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty; // PascalCase "Customer"
}

// ============================================================
// Types for mixed convention pipeline test
// ============================================================

public class MixedConventionSource
{
    public int Id { get; set; }
    public string first_name { get; set; } = string.Empty;   // snake_case → CaseConvention
    public string GetAge() => "25";                            // method → MethodToPropertyConvention
    public string Addr { get; set; } = string.Empty;          // abbreviation → AbbreviationConvention
    public MixedConventionNested Info { get; set; } = new();   // flattening → FlatteningConvention
}

public class MixedConventionNested
{
    public string City { get; set; } = string.Empty;
}

public class MixedConventionTarget
{
    public int Id { get; set; }          // ExactNameConvention
    public string FirstName { get; set; } = string.Empty;     // CaseConvention
    public string Age { get; set; } = string.Empty;           // MethodToPropertyConvention
    public string Address { get; set; } = string.Empty;       // AbbreviationConvention
    public string InfoCity { get; set; } = string.Empty;      // FlatteningConvention
}
