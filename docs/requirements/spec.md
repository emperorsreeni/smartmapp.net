# SmartMapp.Net - Comprehensive Library Specification

> **Version:** 1.0.0-spec  
> **Status:** Draft  
> **Target Framework:** .NET 8+ (with .NET Standard 2.1 compatibility package)  
> **License:** MIT  
> **Tagline:** *Less code. More features. Better performance.*

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Design Philosophy](#2-design-philosophy)
3. [Architecture Overview](#3-architecture-overview)
4. [Core Engine](#4-core-engine)
5. [Auto-Discovery and Convention-Based Mapping](#5-auto-discovery-and-convention-based-mapping)
6. [Fluent Configuration API](#6-fluent-configuration-api)
7. [Type Mapping Strategies](#7-type-mapping-strategies)
8. [Complex Scenario Support](#8-complex-scenario-support)
9. [Performance Engine](#9-performance-engine)
10. [Extensibility Model](#10-extensibility-model)
11. [Dependency Injection Integration](#11-dependency-injection-integration)
12. [Diagnostics and Debugging](#12-diagnostics-and-debugging)
13. [Thread Safety and Concurrency](#13-thread-safety-and-concurrency)
14. [Public API Surface](#14-public-api-surface)
15. [Comparison with AutoMapper](#15-comparison-with-automapper)
16. [Working Samples](#16-working-samples)
17. [Test Plan](#17-test-plan)
18. [Package and Distribution](#18-package-and-distribution)
19. [Roadmap](#19-roadmap)
20. [Glossary](#20-glossary)

---

## 1. Executive Summary

**SmartMapp.Net** is a high-performance, zero-configuration object-to-object mapping library for .NET that automatically discovers and transforms entities without requiring explicit configuration. It introduces a fundamentally different approach from AutoMapper, built around **Sculptors**, **Blueprints**, and **Property Links** rather than Profiles, CreateMap calls, and ForMember chains.

### AutoMapper vs SmartMapp.Net At A Glance

| Dimension | AutoMapper | SmartMapp.Net |
|---|---|---|
| **Approach** | Profile + CreateMap + ForMember ceremony | Zero-config auto-discovery; opt-in Blueprint overrides |
| **Core Concept** | Map types via configuration profiles | Sculpt types via intelligent auto-linking |
| **Performance** | Reflection + expression tree compilation | IL Emit + source generators + SIMD-accelerated collection copy |
| **Memory** | Allocates intermediate expression trees | Near-zero allocation hot path via pooled buffers |
| **Boilerplate** | CreateMap, ForMember, Profile, AddAutoMapper | Single `services.AddSculptor()` - done |
| **Complex Scenarios** | Manual config for inheritance, polymorphism | Automatic polymorphic mapping, deep graph tracking |
| **Extensibility** | IValueResolver, ITypeConverter | Addon system, mapping filters, hooks, custom conventions |
| **Parallelism** | None | Automatic parallel collection mapping with configurable thresholds |
| **Diagnostics** | Limited AssertConfigurationIsValid | Real-time mapping telemetry, OpenTelemetry, mapping atlas visualizer |
| **AOT/Trimming** | Not trim-safe | Source generator mode is fully AOT and trimmer safe |

### Key Differentiators

- **Zero-Config Mapping** - Scans assemblies at startup; links properties by name, type, and structural similarity.
- **Compile-Time Safety** - Optional source generator produces mapping code at build time; errors surface as compiler warnings.
- **Mapping Filter Pipeline** - Every transformation flows through a filter pipeline (pre-map, resolve, map, post-map) that users can intercept.
- **Parallel Collections** - Collections above a configurable threshold are mapped in parallel using `Parallel.ForEachAsync` with work-stealing.
- **Adaptive Caching** - Hot mappings are promoted to IL-emitted delegates; cold mappings fall back to compiled expressions.
- **Sub-100ns Simple Mappings** - Target: mapping a flat 10-property DTO completes in under 100 nanoseconds after warm-up.

---

## 2. Design Philosophy

### 2.1 Principles

| # | Principle | Implication |
|---|---|---|
| P1 | **Convention over Configuration** | Mapping "just works" for 95% of cases with zero setup. |
| P2 | **Pit of Success** | The easiest API to use is also the most correct and performant. |
| P3 | **Pay for What You Use** | Features like parallel mapping, diagnostics, and filters add zero overhead when unused. |
| P4 | **Transparency** | Every mapping decision is traceable. Users can ask "why did property X get value Y?" and get an answer. |
| P5 | **Immutable Configuration** | Once the sculptor is forged, its configuration is frozen. This enables aggressive caching and thread safety. |
| P6 | **Modern C# First** | Leverages `init` properties, records, `required` members, `Span<T>`, generic math, and interceptors. |
| P7 | **Minimal API Surface** | Small public API that is hard to misuse. Internal complexity hidden behind clean abstractions. |

### 2.2 Non-Goals

- SmartMapp.Net is **not** an ORM or serialization library.
- SmartMapp.Net does **not** perform database queries or HTTP calls during mapping (services can be injected for value provision, but I/O is the caller's responsibility).
- SmartMapp.Net does **not** map `dynamic` or `ExpandoObject` in v1.0 (planned for a future release).

---

## 3. Architecture Overview

### 3.1 High-Level Architecture

```
+------------------------------------------------------------------+
|                        Public API Layer                           |
|  ISculptor . IMapper<S,D> . Fluent Bindings . Extensions         |
+------------------------------------------------------------------+
|                     Mapping Filter Pipeline                       |
|  Pre-Map -> Convention Linking -> Mapping Execution -> Post    |
+------------------------------------------------------------------+
|                       Mapping Engine                              |
|  +------------+ +----------------+ +---------------------------+ |
|  | IL Emitter  | | Source Gen     | | Expression Compiler      | |
|  | (hot path)  | | (build-time)   | | (fallback / cold path)   | |
|  +------------+ +----------------+ +---------------------------+ |
+------------------------------------------------------------------+
|                   Convention Engine                               |
|  Name Link . Flattening . Unflattening . Type Coercion . Custom  |
+------------------------------------------------------------------+
|                 Discovery & Metadata Layer                        |
|  Assembly Scanner . Type Analyzer . Blueprint Builder             |
+------------------------------------------------------------------+
|                    Infrastructure                                |
|  Object Pool . Parallel Scheduler . Cache . Diagnostics . DI    |
+------------------------------------------------------------------+
```

### 3.2 Package Structure

| Package | Description |
|---|---|
| `SmartMapp.Net` | Core library: mapping engine, conventions, fluent API |
| `SmartMapp.Net.Codegen` | Roslyn source generator for compile-time mapping code emission |
| `SmartMapp.Net.DependencyInjection` | `IServiceCollection` extensions for DI registration |
| `SmartMapp.Net.AspNetCore` | Model-binding integration, request/response mapping filters |
| `SmartMapp.Net.Insights` | OpenTelemetry, mapping atlas visualizer, health checks |
| `SmartMapp.Net.Validation` | Pre/post-map validation integration with FluentValidation |

### 3.3 Dependency Graph

```
SmartMapp.Net.AspNetCore --------> SmartMapp.Net.DependencyInjection
SmartMapp.Net.Insights ------+           |
SmartMapp.Net.Validation --+  |          |
                        |  |          v
                        +--+----> SmartMapp.Net (core)
                                      ^
SmartMapp.Net.Codegen -------------------+  (analyzer/generator reference)
```

---

## 4. Core Engine

### 4.1 Blueprint

Every `(TOrigin, TTarget)` pair produces a **Blueprint** — an immutable, pre-computed instruction set describing how each target member is populated.

```csharp
public sealed record Blueprint
{
    public Type OriginType { get; init; }
    public Type TargetType { get; init; }
    public IReadOnlyList<PropertyLink> Links { get; init; }
    public MappingStrategy Strategy { get; init; }          // Emit | Compiled | Interpreted
    public bool IsParallelEligible { get; init; }
    public IReadOnlyList<IMappingFilter> Filters { get; init; }
}

public sealed record PropertyLink
{
    public MemberInfo TargetMember { get; init; }
    public IValueProvider Provider { get; init; }           // How the value is obtained
    public ITypeTransformer? Transformer { get; init; }     // Optional type conversion
    public ConventionMatch LinkedBy { get; init; }          // Traceability
    public bool IsSkipped { get; init; }
    public object? Fallback { get; init; }
}
```

### 4.2 Mapping Execution Modes

| Mode | When Used | Characteristics |
|---|---|---|
| **IL Emit** | Hot paths (>10 invocations, auto-promoted) | Fastest; near-manual-code speed via `DynamicMethod` |
| **Source Generated** | Build-time opt-in via `[MappedBy<T>]` attribute | Zero runtime overhead; AOT friendly; trimmer safe |
| **Expression Compiled** | First-call fallback | Fast compilation; moderate speed; serves as warm-up |
| **Interpreted** | Debugging / diagnostics mode | Slowest; full traceability of every step |

### 4.3 Mapping Lifecycle

```
Origin Instance
    |
    v
[Pre-Map Filters]          <-- validation, logging, transformation
    |
    v
[Resolve Target]              <-- construct or reuse existing instance
    |
    v
[For Each PropertyLink]
    |-- Resolve origin value (property, method, flattening, custom provider)
    |-- Transform if types differ (built-in transformers -> custom transformers)
    |-- Recurse if complex type (with circular reference tracking)
    |-- Assign to target member
    |
    v
[Post-Map Filters]         <-- auditing, caching, side-effects
    |
    v
Target Instance
```

### 4.4 Object Construction

SmartMapp.Net supports multiple strategies for constructing target objects:

| Strategy | Description |
|---|---|
| **Parameterless Constructor** | Default. Uses `new T()` or `Activator.CreateInstance<T>()`. |
| **Best-Match Constructor** | Selects the constructor whose parameters best match origin properties by name and type. |
| **Record/Primary Constructor** | Automatically maps to `record` positional parameters. |
| **Factory Function** | User-supplied `Func<TOrigin, TTarget>` via `.BuildWith()`. |
| **Service Provider** | Resolves target from DI container via `.BuildWith(sp => ...)`. |
| **Existing Instance** | Maps onto a pre-existing object via `sculptor.Map(origin, existingTarget)`. |
| **Interface Proxy** | Generates runtime proxy for interface targets via `DispatchProxy`. |

```csharp
// Best-match constructor: SmartMapp.Net matches origin props to ctor params
public record OrderDto(int Id, string CustomerName, decimal Total);

// No configuration needed - SmartMapp.Net detects the primary constructor,
// matches Id, CustomerName (flattened from Customer.Name), and Total.
var dto = sculptor.Map<Order, OrderDto>(order);
```

---

## 5. Auto-Discovery and Convention-Based Mapping

### 5.1 Assembly Scanning

At startup, SmartMapp.Net scans loaded assemblies (or a user-specified subset) to discover:

1. **Mappable type pairs** — types whose names follow configurable patterns (e.g., `Order` to `OrderDto`).
2. **Mapping blueprints** — classes extending `MappingBlueprint`.
3. **Custom providers and transformers** — classes implementing `IValueProvider<,>` or `ITypeTransformer<,>`.
4. **Attributed mappings** — types decorated with `[MapsInto<T>]` or `[MappedBy<T>]`.

```csharp
// Zero-config: just register and go
services.AddSculptor();  // scans calling assembly

// Explicit assembly list
services.AddSculptor(options =>
{
    options.ScanAssemblies(typeof(Order).Assembly, typeof(OrderDto).Assembly);
    options.ScanAssembliesContaining<Startup>();
});
```

### 5.2 Naming Conventions

SmartMapp.Net ships with a rich set of built-in naming conventions and allows custom ones:

| Convention | Example Origin | Example Target | Match |
|---|---|---|---|
| **Exact Name** | `FirstName` | `FirstName` | Yes |
| **Case-Insensitive** | `firstName` | `FirstName` | Yes |
| **Flattening** | `Address.City` | `AddressCity` | Yes |
| **Unflattening** | `AddressCity` | `Address.City` | Yes |
| **Prefix Stripping** | `StrCustomerName` | `CustomerName` | Yes (configurable prefixes) |
| **Suffix Stripping** | `NameField` | `Name` | Yes (configurable suffixes) |
| **Snake to Pascal** | `first_name` | `FirstName` | Yes |
| **Camel to Pascal** | `firstName` | `FirstName` | Yes |
| **Abbreviation Expansion** | `Addr` | `Address` | Yes (via alias dictionary) |
| **Attributed** | `[LinksTo("FullName")]` | `FullName` | Yes |
| **Method to Property** | `GetFullName()` | `FullName` | Yes |

### 5.3 Type Suffix Auto-Pairing

SmartMapp.Net auto-discovers type pairs by matching base names with known suffixes:

```csharp
// Default suffix pairs (configurable)
options.TypePairing.Suffixes = new[]
{
    ("", "Dto"),
    ("", "ViewModel"),
    ("", "Vm"),
    ("", "Model"),
    ("", "Response"),
    ("", "Request"),
    ("", "Command"),
    ("Entity", "Dto"),
    ("Entity", "ViewModel"),
};

// Custom suffix pairs
options.TypePairing.AddSuffixPair("Record", "View");
```

### 5.4 Structural Similarity Matching

When naming conventions do not produce a match, SmartMapp.Net falls back to structural similarity scoring:

```
Score = (MatchedProperties / TotalTargetProperties) x 100
```

If the score exceeds a configurable threshold (default: 70%), the pair is registered as a candidate mapping with a warning in diagnostics.

### 5.5 Custom Conventions

```csharp
public class PrefixDroppingConvention : IPropertyConvention
{
    public bool TryLink(
        MemberInfo targetMember,
        TypeModel originType,
        out IValueProvider? provider)
    {
        // Strip "Dst" prefix and look for matching origin member
        var name = targetMember.Name;
        if (name.StartsWith("Dst"))
        {
            var originName = name[3..];
            var originMember = originType.GetMember(originName);
            if (originMember != null)
            {
                provider = new PropertyAccessProvider(originMember);
                return true;
            }
        }
        provider = null;
        return false;
    }
}

// Register globally
options.Conventions.Add<PrefixDroppingConvention>();
```

---

## 6. Fluent Configuration API

SmartMapp.Net's fluent API is built around **Bindings** — concise, chainable rules that override auto-discovered conventions. This is a completely different mental model from AutoMapper's Profile/CreateMap/ForMember approach.

### 6.1 Blueprint-Based Configuration

```csharp
public class OrderBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<Order, OrderDto>()
            .Property(d => d.CustomerFullName,
                      p => p.From(s => $"{s.Customer.FirstName} {s.Customer.LastName}"))
            .Property(d => d.Total,
                      p => p.From(s => s.Lines.Sum(l => l.Quantity * l.UnitPrice)))
            .Property(d => d.InternalCode,
                      p => p.Skip())
            .OnMapped((origin, target) => target.MappedAt = DateTime.UtcNow);
    }
}
```

### 6.2 Inline Configuration (No Blueprint Required)

```csharp
services.AddSculptor(options =>
{
    options.Bind<Order, OrderDto>(rule => rule
        .Property(d => d.CustomerFullName, p => p.From(s => s.Customer.FullName))
        .Property(d => d.Total, p => p.When(s => s.Status != OrderStatus.Cancelled))
        .DepthLimit(3)
    );
});
```

### 6.3 Attribute-Based Configuration (Zero Boilerplate)

```csharp
[MappedBy<Order>]
public record OrderDto
{
    public int Id { get; init; }
    public string CustomerFullName { get; init; }          // Auto-flattened from Customer.FullName

    [LinkedFrom(nameof(Order.Lines), Transform = "Sum(Quantity * UnitPrice)")]
    public decimal Total { get; init; }

    [Unmapped]
    public string InternalCode { get; init; }
}
```

### 6.4 Global Configuration

```csharp
services.AddSculptor(options =>
{
    // Global conventions
    options.Conventions.OriginPrefixes("Get", "Str", "m_");
    options.Conventions.TargetSuffixes("Field", "Property");
    options.Conventions.EnableSnakeCaseMatching();
    options.Conventions.EnableAbbreviationExpansion(aliases =>
    {
        aliases.Add("Addr", "Address");
        aliases.Add("Qty", "Quantity");
    });

    // Global null handling
    options.Nulls.FallbackForStrings = string.Empty;
    options.Nulls.ThrowOnNullOrigin = false;
    options.Nulls.UseDefaultForNullTarget = true;

    // Global performance tuning
    options.Throughput.ParallelCollectionThreshold = 1000;
    options.Throughput.MaxDegreeOfParallelism = Environment.ProcessorCount;
    options.Throughput.EnableILEmit = true;
    options.Throughput.EnableAdaptivePromotion = true;
    options.Throughput.AdaptivePromotionThreshold = 10;

    // Global depth limit (prevents infinite recursion)
    options.MaxRecursionDepth = 10;
});
```

### 6.5 Fluent API Complete Reference

```csharp
IBindingRule<TOrigin, TTarget>
    // Property configuration
    .Property(targetExpr, propertyRule)
    .AllProperties(propertyRule)
    .RemainingProperties(propertyRule)

    // Origin member configuration
    .OriginProperty(originExpr, originRule)

    // Construction
    .BuildWith(factoryExpr)
    .BuildWith(serviceProvider)

    // Lifecycle hooks
    .OnMapping(action)             // before mapping starts
    .OnMapped(action)              // after mapping completes

    // Conditions
    .When(predicate)               // type-level: only map when true
    .Property(d => d.X, p => p.When(s => s.Y != null))

    // Null handling
    .FallbackTo(value)

    // Inheritance
    .InheritFrom<TBaseOrigin, TBaseTarget>()
    .ExtendWith<TDerivedOrigin, TDerivedTarget>()
    .AsProxy()                     // map to runtime-generated proxy for interfaces

    // Depth control
    .DepthLimit(n)
    .TrackReferences()

    // Bidirectional mapping
    .Bidirectional()

    // Custom transformation
    .TransformWith(transformer)
    .TransformWith<TTransformer>()

    // Validation
    .ValidateAfterMapping(validator)

    // Performance hints
    .PreferILEmit()
    .PreferCodegen()
    .DisableParallelCollections()

    // Mapping filters
    .UseFilters(pipeline => pipeline
        .Add<LoggingFilter>()
        .Add<CachingFilter>()
    );

IPropertyRule<TOrigin, TTarget, TMember>
    .From(originExpr)
    .From<TProvider>()             // DI-resolved provider
    .FromService<TService>(serviceExpr)
    .Skip()                        // ignore this property
    .FallbackTo(value)
    .When(predicate)
    .OnlyIf(predicate)             // pre-condition: skip resolution entirely if false
    .TransformWith(transformer)
    .SetOrder(int)
    .PostProcess(transformExpr)    // post-assignment transform (e.g., Trim())
```

---

## 7. Type Mapping Strategies

### 7.1 Built-in Type Transformers

SmartMapp.Net ships with a comprehensive set of built-in transformers that handle common type conversions without configuration:

| Origin | Target | Strategy |
|---|---|---|
| `string` | `int`, `long`, `decimal`, etc. | `IParsable<T>.TryParse` (generic math) |
| `int`, `long`, etc. | `string` | `ToString()` with culture support |
| `DateTime` | `DateTimeOffset` | Direct conversion with `DateTimeKind` awareness |
| `DateTime` | `DateOnly` / `TimeOnly` | Property extraction |
| `string` | `Enum` | `Enum.TryParse` (case-insensitive) |
| `Enum` | `string` | `ToString()` or `[Description]` attribute |
| `Enum` | `Enum` | By name (default) or by value (configurable) |
| `Guid` | `string` | `ToString("D")` |
| `string` | `Guid` | `Guid.TryParse` |
| `string` | `Uri` | `new Uri()` |
| `int` | `bool` | `!= 0` |
| `bool` | `int` | Ternary `? 1 : 0` |
| `T` | `Nullable<T>` | Wrap |
| `Nullable<T>` | `T` | Unwrap (with null handling) |
| `T` | `string` | Fallback via `ToString()` |
| `byte[]` | `string` | Base64 encode |
| `string` | `byte[]` | Base64 decode |
| `JsonElement` | `T` | `JsonSerializer.Deserialize<T>` |
| `T` | `JsonElement` | `JsonSerializer.SerializeToElement` |

### 7.2 Implicit and Explicit Operator Support

SmartMapp.Net detects and uses `implicit`/`explicit` cast operators defined on either origin or target types:

```csharp
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }

    public static implicit operator decimal(Money m) => m.Amount;
    public static explicit operator Money(decimal d) => new() { Amount = d, Currency = "USD" };
}

// SmartMapp.Net auto-detects: Money -> decimal uses implicit operator
// decimal -> Money uses explicit operator (opt-in via config)
```

### 7.3 Enum Mapping

```csharp
// By name (default) - OrderStatus.Pending -> OrderStatusDto.Pending
// By value - OrderStatus.Pending (0) -> OrderStatusDto (0)
// By attribute - [MapsIntoEnum(OrderStatusDto.InProgress)] on origin enum member
// With fallback - unmatched values use a configurable default

options.Enums.Strategy = EnumMappingStrategy.ByName;  // default
options.Enums.CaseInsensitive = true;                  // default
options.Enums.FallbackValue<OrderStatusDto>(OrderStatusDto.Unknown);
```

### 7.4 String Transformations

```csharp
options.Strings.TrimAll = true;
options.Strings.NullToEmpty = true;
options.Strings.Apply(s => s.Normalize(NormalizationForm.FormC));
```

### 7.5 Custom Type Transformers

```csharp
public class MoneyToStringTransformer : ITypeTransformer<Money, string>
{
    public string Transform(Money origin, MappingScope scope)
        => $"{origin.Currency} {origin.Amount:N2}";
}

// Register globally
options.AddTransformer<MoneyToStringTransformer>();

// Or per-property
plan.Bind<Order, OrderDto>()
    .Property(d => d.TotalDisplay, p => p.TransformWith<MoneyToStringTransformer>());
```

### 7.6 Custom Value Providers

```csharp
public class FullNameProvider : IValueProvider<Customer, CustomerDto, string>
{
    public string Provide(Customer origin, CustomerDto target,
                          string targetMember, MappingScope scope)
        => $"{origin.Title} {origin.FirstName} {origin.LastName}".Trim();
}

// Usage
plan.Bind<Customer, CustomerDto>()
    .Property(d => d.FullName, p => p.From<FullNameProvider>());
```

### 7.7 Conditional Mapping

```csharp
plan.Bind<Order, OrderDto>()
    // Map only when condition is met
    .Property(d => d.ShippingAddress, p => p.When(s => s.IsShipped))
    // Pre-condition: skip entire property resolution if false
    .Property(d => d.Discount, p => p.OnlyIf(s => s.HasDiscount))
    // Type-level condition: skip entire mapping
    .When(s => s.Status != OrderStatus.Deleted);
```

---

## 8. Complex Scenario Support

### 8.1 Inheritance and Polymorphism

SmartMapp.Net automatically detects inheritance hierarchies and maps polymorphic types without explicit configuration.

#### Automatic Polymorphic Mapping

```csharp
// Domain model
public abstract class Map { public double X { get; set; } public double Y { get; set; } }
public class Circle : Map { public double Radius { get; set; } }
public class Rectangle : Map { public double Width { get; set; } public double Height { get; set; } }

// DTOs
public abstract class ShapeDto { public double X { get; set; } public double Y { get; set; } }
public class CircleDto : ShapeDto { public double Radius { get; set; } }
public class RectangleDto : ShapeDto { public double Width { get; set; } public double Height { get; set; } }

// SmartMapp.Net auto-discovers the hierarchy. No configuration needed.
Map map = new Circle { X = 1, Y = 2, Radius = 5 };
ShapeDto dto = sculptor.Map<Shape, ShapeDto>(shape);
// dto is CircleDto { X = 1, Y = 2, Radius = 5 }
```

#### Explicit Inheritance Configuration (when auto-discovery is insufficient)

```csharp
plan.Bind<Shape, ShapeDto>()
    .ExtendWith<Circle, CircleDto>()
    .ExtendWith<Rectangle, RectangleDto>();
```

#### Discriminator-Based Mapping

```csharp
plan.Bind<Shape, ShapeDto>()
    .DiscriminateBy(s => s.GetType().Name)
    .When("Circle", rule => rule.MapAs<CircleDto>())
    .When("Rectangle", rule => rule.MapAs<RectangleDto>())
    .Otherwise(rule => rule.MapAs<ShapeDto>());
```

### 8.2 Nested Objects

Nested objects are mapped recursively by default. SmartMapp.Net detects nested complex types and applies the appropriate blueprint.

```csharp
public class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; }           // nested object
    public Address ShippingAddress { get; set; }     // nested object
}

public class OrderDto
{
    public int Id { get; set; }
    public CustomerDto Customer { get; set; }        // auto-mapped recursively
    public AddressDto ShippingAddress { get; set; }  // auto-mapped recursively
}

// Just works - no configuration needed
var dto = sculptor.Map<Order, OrderDto>(order);
```

### 8.3 Collections

SmartMapp.Net handles all standard .NET collection types:

| Origin Type | Target Type | Strategy |
|---|---|---|
| `T[]` | `T[]` | Array.Copy + element mapping |
| `List<T>` | `List<T>` | Pre-sized list + element mapping |
| `IEnumerable<T>` | `List<T>` | Materialized to list |
| `IEnumerable<T>` | `T[]` | Materialized to array |
| `ICollection<T>` | `ICollection<T>` | Preserve interface |
| `IReadOnlyList<T>` | `IReadOnlyList<T>` | Immutable wrapping |
| `HashSet<T>` | `HashSet<T>` | Set semantics preserved |
| `Dictionary<K,V>` | `Dictionary<K,V>` | Key+value mapping |
| `ImmutableList<T>` | `ImmutableList<T>` | Builder pattern |
| `ImmutableArray<T>` | `ImmutableArray<T>` | Builder pattern |
| `ObservableCollection<T>` | `ObservableCollection<T>` | WPF/MAUI support |
| `ReadOnlyCollection<T>` | `ReadOnlyCollection<T>` | Wrapping |

#### Parallel Collection Mapping

```csharp
// Collections with > threshold items are mapped in parallel automatically
options.Throughput.ParallelCollectionThreshold = 1000;  // default
options.Throughput.MaxDegreeOfParallelism = Environment.ProcessorCount;

// Disable per-binding
plan.Bind<Order, OrderDto>()
    .DisableParallelCollections();

// Force parallel regardless of threshold
plan.Bind<Order, OrderDto>()
    .ForceParallelCollections();
```

#### Collection Merging (Update Existing Collections)

```csharp
// Instead of replacing, merge origin into existing target collection
plan.Bind<Order, OrderDto>()
    .Property(d => d.Lines, p => p.Merge(
        matchBy: (origin, target) => origin.LineId == target.LineId,
        onAdd: line => { /* new item */ },
        onRemove: line => { /* removed item */ },
        onUpdate: (origin, target) => { /* updated item */ }
    ));
```

### 8.4 Flattening and Unflattening

#### Automatic Flattening

```csharp
public class Order
{
    public Customer Customer { get; set; }
}
public class Customer
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public Address Address { get; set; }
}
public class Address { public string City { get; set; } }

public class OrderFlatDto
{
    public string CustomerFirstName { get; set; }    // Order.Customer.FirstName
    public string CustomerLastName { get; set; }     // Order.Customer.LastName
    public string CustomerAddressCity { get; set; }  // Order.Customer.Address.City
}

// All three properties are auto-flattened. No configuration.
var dto = sculptor.Map<Order, OrderFlatDto>(order);
```

#### Automatic Unflattening

```csharp
// Reverse: OrderFlatDto -> Order also works automatically
var order = sculptor.Map<OrderFlatDto, Order>(dto);
// order.Customer.Address.City is populated correctly
```

### 8.5 Circular References and Self-Referencing Types

```csharp
public class Employee
{
    public string Name { get; set; }
    public Employee Manager { get; set; }             // self-reference
    public List<Employee> DirectReports { get; set; } // circular collection
}

// SmartMapp.Net tracks visited objects via identity map.
// Circular references produce the same target instance (no infinite loop).
options.TrackReferences = true;  // default: true

// Or limit depth
plan.Bind<Employee, EmployeeDto>()
    .DepthLimit(5);
```

### 8.6 Dictionary and Dynamic Mapping

```csharp
// Dictionary to object
var dict = new Dictionary<string, object>
{
    ["Id"] = 1,
    ["Name"] = "Alice",
    ["Address"] = new Dictionary<string, object>
    {
        ["City"] = "Seattle",
        ["Zip"] = "98101"
    }
};

var customer = sculptor.Map<Dictionary<string, object>, CustomerDto>(dict);

// Object to dictionary
var dict2 = sculptor.Map<CustomerDto, Dictionary<string, object>>(customer);
```

### 8.7 Tuple Mapping

```csharp
// ValueTuple to object
var tuple = (Id: 1, Name: "Alice", Age: 30);
var dto = sculptor.Map<(int Id, string Name, int Age), PersonDto>(tuple);

// Object to ValueTuple
var tuple2 = sculptor.Map<PersonDto, (int Id, string Name, int Age)>(dto);
```

### 8.8 Record and Init-Only Property Support

```csharp
// Records with positional parameters
public record PersonRecord(string FirstName, string LastName, int Age);

// Records with init-only properties
public record PersonDto
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public int Age { get; init; }
}

// SmartMapp.Net uses the constructor for records with positional params
// and uses init-only setters for records with init properties.
// Both work without configuration.
```

### 8.9 Interface and Abstract Type Mapping

```csharp
// Map to an interface - SmartMapp.Net generates a runtime proxy
ICustomerDto dto = sculptor.Map<Customer, ICustomerDto>(customer);

// Or configure a concrete type
plan.Bind<Customer, ICustomerDto>()
    .Materialize<CustomerDto>();  // use CustomerDto as the concrete implementation
```

### 8.10 Projection (IQueryable Support)

```csharp
// Project to DTOs at the database level
var dtos = dbContext.Orders
    .SelectAs<OrderDto>(sculptor)
    .Where(d => d.Total > 100)
    .ToListAsync();

// This generates an Expression<Func<Order, OrderDto>> that translates to SQL.
// Only linked columns are selected - no over-fetching.
```

### 8.11 Multi-Origin Mapping (Compose from Multiple Objects)

```csharp
// Combine multiple origin objects into a single target
var dto = sculptor.Compose<OrderDto>(order, customer, shippingAddress);

// Fluent configuration for multi-origin
plan.Compose<OrderDto>()
    .FromOrigin<Order>(r => r
        .Property(d => d.OrderId, p => p.From(s => s.Id)))
    .FromOrigin<Customer>(r => r
        .Property(d => d.CustomerName, p => p.From(s => s.FullName)))
    .FromOrigin<Address>(r => r
        .Property(d => d.City, p => p.From(s => s.City)));
```

### 8.12 Bi-directional Mapping

```csharp
// .Bidirectional() creates the inverse mapping automatically
plan.Bind<Order, OrderDto>()
    .Property(d => d.CustomerName, p => p.From(s => s.Customer.FullName))
    .Bidirectional()
    .PropertyPath(s => s.Customer.FullName, p => p.From(d => d.CustomerName));
```

---

## 9. Performance Engine

### 9.1 Performance Targets

| Scenario | Target | AutoMapper Baseline |
|---|---|---|
| Flat 10-prop DTO (warm) | < 100 ns | ~300 ns |
| Nested 3-level object (warm) | < 500 ns | ~1500 ns |
| Collection of 1000 flat DTOs | < 100 us | ~300 us |
| Collection of 10000 flat DTOs | < 800 us (parallel) | ~3000 us |
| Memory per flat mapping | 0 bytes (pooled) | ~120 bytes |
| Startup scan (100 types) | < 50 ms | N/A (manual config) |
| First-map compilation | < 5 ms | ~15 ms |

### 9.2 IL Emit Engine

The IL Emit engine generates `DynamicMethod` delegates that perform direct property access without reflection:

```csharp
// Conceptually, the emitted code looks like:
static OrderDto Map(Order origin)
{
    var target = new OrderDto();
    target.Id = origin.Id;
    target.CustomerName = origin.Customer?.Name;
    target.Total = origin.Lines.Sum(l => l.Quantity * l.UnitPrice);
    return target;
}
```

**Adaptive Promotion:** Mappings start as compiled expressions. After exceeding a configurable invocation threshold (default: 10), they are promoted to IL-emitted delegates on a background thread. The switchover is atomic and lock-free via `Interlocked.CompareExchange`.

### 9.3 Source Generator (Build-Time)

The `SmartMapp.Net.Codegen` package uses Roslyn incremental generators to emit mapping code at compile time:

```csharp
// User writes:
[MappedBy<Order>]
public partial record OrderDto
{
    public int Id { get; init; }
    public string CustomerName { get; init; }
}

// Source generator emits (in a partial class):
public partial record OrderDto
{
    [GeneratedMapper]
    public static OrderDto MapFromOrder(Order origin)
    {
        return new OrderDto
        {
            Id = origin.Id,
            CustomerName = origin.Customer?.Name ?? string.Empty,
        };
    }
}
```

**Benefits:**
- Zero runtime reflection or compilation overhead.
- Fully AOT-compatible and trimmer-safe.
- Compile-time errors for unlinked required members.
- IntelliSense support in the generated code.

### 9.4 Object Pooling

```csharp
// SmartMapp.Net pools intermediate objects to reduce GC pressure
// ArrayPool<T> for collection buffers
// ObjectPool<MappingScope> for mapping scope objects
// StringBuilderPool for string concatenation in transformers

options.Throughput.EnableObjectPooling = true;     // default
options.Throughput.PoolMaxRetained = 256;           // per type
```

### 9.5 SIMD-Accelerated Primitive Collection Copy

For collections of primitive types (`int[]`, `double[]`, `byte[]`, etc.), SmartMapp.Net uses `Vector<T>` SIMD operations or `Buffer.BlockCopy` for maximum throughput:

```csharp
// Internally, for int[] -> int[] with same element type:
Buffer.BlockCopy(origin, 0, target, 0, origin.Length * sizeof(int));

// For int[] -> long[] (widening):
// Uses SIMD Vector.Widen where hardware supports it
```

### 9.6 Parallel Collection Mapping

```csharp
// When collection.Count > ParallelCollectionThreshold (default: 1000):
// 1. Partition the collection into chunks
// 2. Map each chunk in parallel using Parallel.ForEachAsync
// 3. Combine results preserving original order

// Configuration
options.Throughput.ParallelCollectionThreshold = 1000;
options.Throughput.MaxDegreeOfParallelism = Environment.ProcessorCount;
options.Throughput.ParallelPartitionSize = 256;  // items per work unit
```

### 9.7 Lazy and Streaming Mapping

```csharp
// Lazy mapping - maps items on demand (IEnumerable)
IEnumerable<OrderDto> dtos = sculptor.MapLazy<Order, OrderDto>(orders);

// Streaming mapping - for IAsyncEnumerable
IAsyncEnumerable<OrderDto> dtos = sculptor.MapStream<Order, OrderDto>(ordersAsync);

// Useful for large datasets where materializing all items is expensive
await foreach (var dto in sculptor.MapStream<Order, OrderDto>(GetOrdersAsync()))
{
    await ProcessAsync(dto);
}
```

### 9.8 Caching Strategy

| Cache Level | What | Lifetime | Eviction |
|---|---|---|---|
| **L1: Blueprint Cache** | `Blueprint` per `(TOrigin, TTarget)` | App lifetime | Never (immutable config) |
| **L2: Delegate Cache** | Compiled/emitted mapping delegates | App lifetime | Never (promoted only) |
| **L3: Type Metadata Cache** | Reflected member info, constructors | App lifetime | Never |
| **L4: Convention Link Cache** | Name matching results | App lifetime | Never |
| **Hot Path Counter** | Invocation count per mapping pair | App lifetime | Reset on recompilation |

All caches use `ConcurrentDictionary<TKey, TValue>` or `FrozenDictionary<TKey, TValue>` (.NET 8+) for lock-free reads.

### 9.9 Benchmarking Suite

SmartMapp.Net ships with a BenchmarkDotNet project comparing against AutoMapper and Mapster:

```
BenchmarkDotNet Results (example):

|              Method |       Mean |    Error |  StdDev | Ratio |   Gen0 | Allocated |
|-------------------- |-----------:|---------:|--------:|------:|-------:|----------:|
|   SmartMappNet_Flat |    62.3 ns |  0.8 ns  | 0.7 ns  |  1.00 | 0.0000 |         - |
|     AutoMapper_Flat |   287.4 ns |  3.2 ns  | 2.8 ns  |  4.61 | 0.0191 |     120 B |
|        Mapster_Flat |   148.7 ns |  1.5 ns  | 1.3 ns  |  2.39 | 0.0095 |      60 B |
| SmartMappNet_Nested |   312.1 ns |  4.1 ns  | 3.6 ns  |  1.00 | 0.0000 |         - |
|   AutoMapper_Nested |  1421.6 ns | 12.3 ns  | 10.9 ns |  4.56 | 0.0572 |     360 B |
| SmartMappNet_1K_List  |    89.2 us |  1.1 us  | 0.9 us  |  1.00 | 0.0000 |      48 B |
| AutoMapper_1K_List  |   312.8 us |  3.8 us  | 3.4 us  |  3.51 | 19.531 |  120048 B |
```

---

## 10. Extensibility Model

### 10.1 Addon Architecture

SmartMapp.Net uses an addon-based architecture where all features (including built-in ones) are implemented as addons:

```csharp
public interface ISculptorAddon
{
    string Name { get; }
    int Order { get; }                     // execution order
    void Install(IAddonContext context);
}

// Register addons
options.Addons.Install<MyCustomAddon>();
options.Addons.Install<OpenTelemetryAddon>();
```

### 10.2 Mapping Filters

Every mapping flows through a filter pipeline — a chain-of-responsibility pattern unique to SmartMapp.Net:

```csharp
public interface IMappingFilter
{
    Task<object?> ApplyAsync(
        MappingContext context,
        MappingDelegate next);
}

// Example: Logging filter
public class LoggingFilter : IMappingFilter
{
    private readonly ILogger _logger;

    public LoggingFilter(ILogger<LoggingFilter> logger) => _logger = logger;

    public async Task<object?> ApplyAsync(MappingContext context, MappingDelegate next)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("Mapping {Origin} -> {Target}", context.OriginType, context.TargetType);

        var result = await next(context);

        sw.Stop();
        _logger.LogDebug("Mapped in {Elapsed}ms", sw.ElapsedMilliseconds);
        return result;
    }
}

// Register globally
options.Filters.Add<LoggingFilter>();

// Or per-binding
plan.Bind<Order, OrderDto>()
    .UseFilters(f => f.Add<LoggingFilter>());
```

### 10.3 Mapping Hooks

Hooks provide a simpler alternative to filters for common pre/post-map scenarios:

```csharp
public interface IMappingHook<TOrigin, TTarget>
{
    void OnMapping(TOrigin origin, TTarget target, MappingScope scope);
    void OnMapped(TOrigin origin, TTarget target, MappingScope scope);
}

// Example: Audit trail hook
public class AuditHook : IMappingHook<object, IAuditable>
{
    public void OnMapping(object origin, IAuditable target, MappingScope scope) { }
    public void OnMapped(object origin, IAuditable target, MappingScope scope)
    {
        target.MappedAt = DateTime.UtcNow;
        target.MappedBy = scope.GetService<ICurrentUser>()?.Name;
    }
}

// Auto-discovered and applied to all mappings where TTarget implements IAuditable
```

### 10.4 Custom Conventions

```csharp
public interface IPropertyConvention
{
    int Priority { get; }
    bool TryLink(MemberInfo target, TypeModel origin, out IValueProvider? provider);
}

public interface ITypeConvention
{
    bool TryBind(Type origin, Type target, out Blueprint? blueprint);
}

// Register
options.Conventions.Add<MyPropertyConvention>();
options.Conventions.AddTypeConvention<MyTypeConvention>();
```

### 10.5 Custom Value Provider Pipeline

```csharp
// Providers are tried in order until one succeeds:
// 1. Explicit configuration (.Property().From())
// 2. Attribute-based ([LinkedFrom])
// 3. Custom conventions (user-registered)
// 4. Built-in conventions (name linking, flattening)
// 5. Type transformer (if types differ but conversion exists)
// 6. Fallback value / null substitute
// 7. Skip (if no match found and strict mode is off)

// Users can insert providers at any priority level
options.Providers.InsertBefore<BuiltInNameConvention, MyCustomProvider>();
```

### 10.6 Mapping Events

```csharp
// Global events for cross-cutting concerns
options.Events.OnBlueprintCreated += (sender, args) =>
{
    Console.WriteLine($"New blueprint: {args.OriginType} -> {args.TargetType}");
};

options.Events.OnMappingError += (sender, args) =>
{
    Console.WriteLine($"Mapping error: {args.Exception.Message}");
    args.Handled = true;  // suppress exception
    args.Result = default; // use default value
};

options.Events.OnPropertyLinked += (sender, args) =>
{
    Console.WriteLine($"  {args.TargetMember.Name} = {args.Value}");
};
```

### 10.7 Custom Mapping Strategy Provider

```csharp
// Override how mapping delegates are generated
public interface IMappingStrategyProvider
{
    MappingStrategy Strategy { get; }
    Delegate? TryCreateMapper(Blueprint blueprint);
}

// Example: use a third-party expression library
public class FastExpressionStrategy : IMappingStrategyProvider
{
    public MappingStrategy Strategy => MappingStrategy.Custom;

    public Delegate? TryCreateMapper(Blueprint blueprint)
    {
        // Build and compile using FastExpressionCompiler
        var expr = BuildExpression(blueprint);
        return expr.CompileFast();
    }
}

options.Strategies.Add<FastExpressionStrategy>();
```

---

## 11. Dependency Injection Integration

### 11.1 Registration

```csharp
// Minimal registration - scans calling assembly
services.AddSculptor();

// With options
services.AddSculptor(options =>
{
    options.ScanAssembliesContaining<Order, OrderDto>();
    options.UseBlueprint<OrderBlueprint>();
    options.Bind<Order, OrderDto>();
});

// Register as specific lifetime
services.AddSculptor(ServiceLifetime.Singleton);  // default: Singleton
```

### 11.2 Service Registration Details

| Service | Lifetime | Description |
|---|---|---|
| `ISculptor` | Singleton | Main mapping interface (thread-safe, immutable config) |
| `IMapper<TOrigin, TTarget>` | Singleton | Strongly-typed mapper for a specific pair |
| `ISculptorConfiguration` | Singleton | Read-only access to mapping configuration |
| `IMappingEngine` | Singleton | Low-level mapping engine |
| `IValueProvider<,,>` | Transient | Custom providers (DI-resolved per invocation) |
| `ITypeTransformer<,>` | Transient | Custom transformers |
| `IMappingFilter` | Scoped | Filter instances (scoped for request context) |
| `IMappingHook<,>` | Scoped | Hook instances |

### 11.3 Injecting the Sculptor

```csharp
// Constructor injection (recommended)
public class OrderService
{
    private readonly ISculptor _sculptor;

    public OrderService(ISculptor sculptor) => _sculptor = sculptor;

    public OrderDto GetOrder(int id)
    {
        var order = _repository.GetById(id);
        return _sculptor.Map<Order, OrderDto>(order);
    }
}

// Strongly-typed injection (for performance-critical paths)
public class OrderService
{
    private readonly IMapper<Order, OrderDto> _mapper;

    public OrderService(IMapper<Order, OrderDto> mapper) => _mapper = mapper;

    public OrderDto GetOrder(int id)
    {
        var order = _repository.GetById(id);
        return _mapper.Map(order);
    }
}
```

### 11.4 Providers with DI

```csharp
public class TaxCalculatorProvider : IValueProvider<Order, OrderDto, decimal>
{
    private readonly ITaxService _taxService;

    public TaxCalculatorProvider(ITaxService taxService) => _taxService = taxService;

    public decimal Provide(Order origin, OrderDto target, decimal member, MappingScope scope)
        => _taxService.CalculateTax(origin.Total, origin.ShippingAddress.State);
}

// Used in mapping - provider is created via DI
plan.Bind<Order, OrderDto>()
    .Property(d => d.Tax, p => p.From<TaxCalculatorProvider>());
```

### 11.5 Keyed Services (.NET 8+)

```csharp
// Register multiple sculptor configurations
services.AddSculptor("admin", options => { /* admin mappings */ });
services.AddSculptor("public", options => { /* public mappings */ });

// Inject by key
public class AdminService([FromKeyedServices("admin")] ISculptor sculptor) { }
```

### 11.6 Health Checks

```csharp
services.AddHealthChecks()
    .AddSculptorHealthCheck();  // validates all registered blueprints at health check time
```

---

## 12. Diagnostics and Debugging

### 12.1 Configuration Validation

```csharp
// Validate all mappings at startup (fail-fast)
services.AddSculptor(options =>
{
    options.ValidateOnStartup = true;         // default: true in Development
    options.StrictMode = true;                // all target members must be linked
    options.ThrowOnUnlinkedMembers = true;    // throw if any target member has no origin
});

// Validate programmatically
var config = serviceProvider.GetRequiredService<ISculptorConfiguration>();
var result = config.Validate();

foreach (var error in result.Errors)
{
    Console.WriteLine($"{error.OriginType} -> {error.TargetType}: {error.Message}");
}
```

### 12.2 Mapping Inspection

```csharp
// Ask "why" for any mapping decision
var inspection = sculptor.Inspect<Order, OrderDto>();

Console.WriteLine(inspection.ToString());
// Output:
// Order -> OrderDto (Strategy: ILEmit, 8 links)
//   Id -> Id (ExactNameLink)
//   Customer.FirstName + Customer.LastName -> CustomerFullName (CustomProvider: FullNameProvider)
//   Lines.Sum(Quantity * UnitPrice) -> Total (ExplicitBinding)
//   InternalCode -> [SKIPPED]
//   Status -> Status (ExactNameLink, EnumByName)
//   CreatedAt -> CreatedAt (ExactNameLink, DateTimeToDateTimeOffset)
//   ShippingAddress -> ShippingAddress (NestedMapping: Address -> AddressDto)
//   Customer -> Customer (NestedMapping: Customer -> CustomerDto)
```

### 12.3 Mapping Atlas Visualizer

```csharp
// Generate a DOT graph of all registered mappings
var atlas = sculptor.GetMappingAtlas();
var dot = atlas.ToDotFormat();
File.WriteAllText("mapping-atlas.dot", dot);

// Or serve as an endpoint in ASP.NET Core
app.MapSculptorInsights("/insights/mappings");
// Provides:
//   GET /insights/mappings         -> JSON list of all mappings
//   GET /insights/mappings/atlas   -> DOT/SVG graph
//   GET /insights/mappings/{type}  -> details for a specific type
```

### 12.4 OpenTelemetry Integration

```csharp
// Add SmartMapp.Net instrumentation
services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddSculptorInstrumentation())
    .WithMetrics(builder => builder.AddSculptorInstrumentation());

// Metrics emitted:
// smartmappnet.mappings.total          - counter of total mappings performed
// smartmappnet.mappings.duration       - histogram of mapping durations
// smartmappnet.mappings.errors         - counter of mapping errors
// smartmappnet.collections.parallel    - counter of parallel collection mappings
// smartmappnet.cache.hits              - counter of cache hits
// smartmappnet.cache.promotions        - counter of IL emit promotions

// Traces emitted:
// Each mapping creates a span with:
//   smartmappnet.origin_type, smartmappnet.target_type,
//   smartmappnet.strategy, smartmappnet.link_count
```

### 12.5 Logging

```csharp
// SmartMapp.Net uses ILogger<SmartMapp.Net> for structured logging
// Log levels:
//   Debug   - individual property links, cache hits
//   Info    - blueprint creation, IL promotion
//   Warning - unlinked members (non-strict mode), structural similarity matches
//   Error   - mapping failures, invalid configuration

options.Logging.MinimumLevel = LogLevel.Warning;  // default
options.Logging.LogBlueprints = true;              // log full blueprint on creation
```

### 12.6 Debug View

```csharp
// In the debugger, Blueprint has a DebugView property
// that shows a human-readable representation of the mapping:

[DebuggerDisplay("{DebugView}")]
public sealed record Blueprint
{
    internal string DebugView => $"{OriginType.Name} -> {TargetType.Name} [{Strategy}] ({Links.Count} links)";
}
```

---

## 13. Thread Safety and Concurrency

### 13.1 Thread Safety Guarantees

| Component | Thread Safe | Notes |
|---|---|---|
| `ISculptor` | Yes | Immutable after forging. All methods are safe for concurrent use. |
| `Blueprint` | Yes | Immutable record. |
| `PropertyLink` | Yes | Immutable record. |
| `MappingScope` | No | Created per-mapping, not shared across threads. |
| Cache stores | Yes | `ConcurrentDictionary` / `FrozenDictionary`. |
| IL Emit promotion | Yes | Atomic via `Interlocked.CompareExchange`. |
| Parallel collections | Yes | Each thread gets its own `MappingScope`. |

### 13.2 Immutable Configuration Pattern

```csharp
// Configuration is built once and frozen
var builder = new SculptorBuilder();
builder.UseBlueprint<OrderBlueprint>();
builder.Bind<Customer, CustomerDto>();

// Forge() freezes the configuration - no further modifications allowed
ISculptor sculptor = builder.Forge();

// Attempting to modify after Forge() throws InvalidOperationException
```

### 13.3 Concurrent Collection Mapping

```csharp
// When parallel mapping is enabled, SmartMapp.Net ensures:
// 1. Each parallel task gets its own MappingScope (no sharing)
// 2. Circular reference tracking is per-scope (thread-local)
// 3. Results array is pre-allocated (no concurrent writes to shared list)
// 4. Exceptions are aggregated and re-thrown as AggregateException

// The user can safely call sculptor.Map from multiple threads simultaneously
Parallel.ForEach(orders, order =>
{
    var dto = sculptor.Map<Order, OrderDto>(order);  // safe
    Process(dto);
});
```

---

## 14. Public API Surface

### 14.1 Core Interfaces

```csharp
// Primary sculptor interface
public interface ISculptor
{
    TTarget Map<TOrigin, TTarget>(TOrigin origin);
    TTarget Map<TOrigin, TTarget>(TOrigin origin, TTarget existingTarget);
    object Map(object origin, Type originType, Type targetType);
    object Map(object origin, object target, Type originType, Type targetType);

    // Collection mapping
    IReadOnlyList<TTarget> MapAll<TOrigin, TTarget>(IEnumerable<TOrigin> origins);
    TTarget[] MapToArray<TOrigin, TTarget>(IEnumerable<TOrigin> origins);

    // Lazy / streaming
    IEnumerable<TTarget> MapLazy<TOrigin, TTarget>(IEnumerable<TOrigin> origins);
    IAsyncEnumerable<TTarget> MapStream<TOrigin, TTarget>(
        IAsyncEnumerable<TOrigin> origins, CancellationToken ct = default);

    // Multi-origin
    TTarget Compose<TTarget>(params object[] origins);

    // Projection
    IQueryable<TTarget> SelectAs<TTarget>(IQueryable source);
    Expression<Func<TOrigin, TTarget>> GetProjection<TOrigin, TTarget>();

    // Diagnostics
    MappingInspection Inspect<TOrigin, TTarget>();
    MappingAtlas GetMappingAtlas();
}

// Strongly-typed mapper for a specific pair (faster, no dictionary lookup)
public interface IMapper<TOrigin, TTarget>
{
    TTarget Map(TOrigin origin);
    TTarget Map(TOrigin origin, TTarget existingTarget);
    IReadOnlyList<TTarget> MapAll(IEnumerable<TOrigin> origins);
}
```

### 14.2 Configuration Interfaces

```csharp
public interface ISculptorConfiguration
{
    IReadOnlyDictionary<TypePair, Blueprint> GetAllBlueprints();
    Blueprint? GetBlueprint<TOrigin, TTarget>();
    Blueprint? GetBlueprint(Type originType, Type targetType);
    ValidationResult Validate();
    bool HasBinding<TOrigin, TTarget>();
    bool HasBinding(Type originType, Type targetType);
}
```

### 14.3 Builder API

```csharp
public interface ISculptorBuilder
{
    IBindingRule<TOrigin, TTarget> Bind<TOrigin, TTarget>();
    ICompositionRule<TTarget> Compose<TTarget>();
    ISculptorBuilder UseBlueprint<TBlueprint>() where TBlueprint : MappingBlueprint;
    ISculptorBuilder UseBlueprint(MappingBlueprint blueprint);
    ISculptorBuilder AddTransformer<TTransformer>() where TTransformer : class;
    ISculptorBuilder ScanAssemblies(params Assembly[] assemblies);
    ISculptorBuilder ScanAssembliesContaining<T>();
    ISculptor Forge();
}
```

### 14.4 Attributes

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MappedByAttribute<TOrigin> : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MapsIntoAttribute<TTarget> : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class LinkedFromAttribute : Attribute
{
    public LinkedFromAttribute(string originMemberName);
    public string? Transform { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class LinksToAttribute : Attribute
{
    public LinksToAttribute(string targetMemberName);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class UnmappedAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class TransformWithAttribute<TTransformer> : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ProvideWithAttribute<TProvider> : Attribute { }

[AttributeUsage(AttributeTargets.Enum)]
public sealed class MapsIntoEnumAttribute : Attribute
{
    public MapsIntoEnumAttribute(object targetValue);
}
```

### 14.5 Extension Methods

```csharp
// IServiceCollection extensions
public static class SculptorServiceCollectionExtensions
{
    public static IServiceCollection AddSculptor(this IServiceCollection services);
    public static IServiceCollection AddSculptor(this IServiceCollection services,
        Action<SculptorOptions> configure);
    public static IServiceCollection AddSculptor(this IServiceCollection services,
        ServiceLifetime lifetime);
    public static IServiceCollection AddSculptor(this IServiceCollection services,
        string key, Action<SculptorOptions> configure);
}

// IQueryable extensions
public static class SculptorQueryableExtensions
{
    public static IQueryable<TTarget> SelectAs<TTarget>(this IQueryable source, ISculptor sculptor);
}

// Object extensions (opt-in via using SmartMapp.Net.Extensions)
public static class SculptorObjectExtensions
{
    public static TTarget MapTo<TTarget>(this object source);
    public static TTarget MapTo<TTarget>(this object source, ISculptor sculptor);
}

// Health check extensions
public static class SculptorHealthCheckExtensions
{
    public static IHealthChecksBuilder AddSculptorHealthCheck(this IHealthChecksBuilder builder);
}

// ASP.NET Core extensions
public static class SculptorEndpointExtensions
{
    public static IEndpointRouteBuilder MapSculptorInsights(
        this IEndpointRouteBuilder endpoints, string prefix = "/insights/mappings");
}
```

---

## 15. Comparison with AutoMapper

### 15.1 Conceptual Differences

| Concept | AutoMapper | SmartMapp.Net |
|---|---|---|
| **Core metaphor** | "Mapping" — cartography | "Sculpting" — mapping data into new forms |
| **Configuration unit** | `Profile` (class with CreateMap calls) | `MappingBlueprint` (class with Bind rules) |
| **Registration call** | `CreateMap<S,D>()` | `Bind<S,D>()` (or fully auto-discovered) |
| **Property override** | `ForMember(d => ..., o => o.MapFrom(...))` | `Property(d => ..., p => p.From(...))` |
| **Ignore a property** | `ForMember(d => ..., o => o.Ignore())` | `Property(d => ..., p => p.Skip())` or `[Unmapped]` |
| **Reverse mapping** | `.ReverseMap()` | `.Bidirectional()` |
| **Value extraction** | `IValueResolver` | `IValueProvider` |
| **Type conversion** | `ITypeConverter` | `ITypeTransformer` |
| **Execution context** | `ResolutionContext` | `MappingScope` |
| **Builder/entry point** | `MapperConfiguration` + `CreateMapper()` | `SculptorBuilder` + `Forge()` |
| **DI registration** | `AddAutoMapper()` | `AddSculptor()` |
| **Primary interface** | `IMapper` | `ISculptor` |
| **IQueryable projection** | `ProjectTo<D>()` | `SelectAs<D>()` |
| **Condition** | `.Condition()` | `.When()` |
| **Pre-condition** | `.PreCondition()` | `.OnlyIf()` |
| **Null substitute** | `.NullSubstitute()` | `.FallbackTo()` |
| **Construction** | `.ConstructUsing()` | `.BuildWith()` |
| **Max depth** | `.MaxDepth()` | `.DepthLimit()` |
| **Preserve references** | `.PreserveReferences()` | `.TrackReferences()` |
| **After map** | `.AfterMap()` | `.OnMapped()` |
| **Before map** | `.BeforeMap()` | `.OnMapping()` |
| **Middleware** | N/A | `IMappingFilter` |
| **Interceptors** | N/A | `IMappingHook` |
| **Plugins** | N/A | `ISculptorAddon` |
| **Diagnostics** | `AssertConfigurationIsValid` | `Inspect<S,D>()`, `GetMappingAtlas()` |
| **Concrete type for interfaces** | `.As<T>()` | `.Materialize<T>()` |
| **Include derived** | `.Include<S,D>()` | `.ExtendWith<S,D>()` |

### 15.2 Feature Comparison Matrix

| Feature | AutoMapper | SmartMapp.Net | Notes |
|---|---|---|---|
| Zero-config mapping | No | **Yes** | SmartMapp.Net auto-discovers type pairs |
| Fluent API | Yes | **Yes** | SmartMapp.Net uses a distinct Bind/Property/From pattern |
| Attribute-based config | No | **Yes** | `[MappedBy<T>]`, `[MapsInto<T>]`, `[Unmapped]` |
| Blueprint-based config | Yes (Profile) | **Yes** | `MappingBlueprint` with `Design()` method |
| Inline config (no blueprint) | No | **Yes** | Configure directly in DI registration |
| Source generators | No | **Yes** | AOT-safe, zero-reflection mapping |
| IL Emit | No | **Yes** | Adaptive hot-path promotion |
| Parallel collections | No | **Yes** | Auto-parallel above threshold |
| SIMD collection copy | No | **Yes** | Primitive array fast-path |
| Streaming (IAsyncEnumerable) | No | **Yes** | `MapStream` |
| Multi-origin composition | No | **Yes** | `Compose` N origins into 1 target |
| Flattening | Yes | **Yes** | Both support automatic flattening |
| Unflattening | Partial | **Yes** | SmartMapp.Net auto-unflattens bidirectionally |
| Inheritance/polymorphism | Manual config | **Auto** | Auto-detected from type hierarchy |
| Discriminator mapping | No | **Yes** | `DiscriminateBy()` |
| Dictionary-to-object | No | **Yes** | Bidirectional |
| Tuple mapping | No | **Yes** | Named ValueTuple support |
| Record/init support | Partial | **Full** | Primary ctors, init-only, required members |
| Interface proxy mapping | No | **Yes** | Runtime DispatchProxy generation |
| Collection merging | No | **Yes** | Update-in-place with match/add/remove |
| IQueryable projection | Yes (ProjectTo) | **Yes** (`SelectAs`) | Different name, same power |
| Custom providers (DI) | Yes | **Yes** | Distinct `IValueProvider` interface |
| Custom transformers | Yes | **Yes** | Distinct `ITypeTransformer` interface |
| Mapping filter pipeline | No | **Yes** | Chain-of-responsibility filters |
| Mapping hooks | No | **Yes** | Typed before/after hooks |
| Addon system | No | **Yes** | Modular architecture |
| Events | No | **Yes** | OnBlueprintCreated, OnError, OnPropertyLinked |
| Mapping inspection | No | **Yes** | `.Inspect<S,D>()` traces every decision |
| Atlas visualizer | No | **Yes** | DOT/SVG output, diagnostic endpoint |
| OpenTelemetry | No | **Yes** | Metrics + traces |
| Health checks | No | **Yes** | ASP.NET Core health check |
| Keyed DI services | No | **Yes** | .NET 8 keyed services |
| Strict mode | Partial | **Yes** | All-or-nothing validation |
| Configuration validation | `AssertConfigurationIsValid` | **Yes** | Richer error messages, startup validation |
| Lazy mapping | No | **Yes** | Deferred `IEnumerable` mapping |
| Object pooling | No | **Yes** | Reduces GC pressure |
| Snake/camel/pascal conv. | No | **Yes** | Built-in naming convention converters |
| Abbreviation aliases | No | **Yes** | `Addr` -> `Address` dictionary |

### 15.3 Code Comparison

#### AutoMapper: Setting Up a Simple Mapping

```csharp
// 1. Create a profile
public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer.FullName))
            .ForMember(d => d.OrderTotal, o => o.MapFrom(s => s.Lines.Sum(l => l.Total)));

        CreateMap<Customer, CustomerDto>();
        CreateMap<Address, AddressDto>();
        CreateMap<OrderLine, OrderLineDto>();
    }
}

// 2. Register in DI
services.AddAutoMapper(typeof(OrderProfile).Assembly);

// 3. Use
var dto = mapper.Map<OrderDto>(order);
```

**Lines of code: 15**

#### SmartMapp.Net: Same Mapping

```csharp
// 1. Register in DI (auto-discovers all mappings)
services.AddSculptor();

// 2. Use
var dto = sculptor.Map<Order, OrderDto>(order);
```

**Lines of code: 2** (Customer, Address, OrderLine mappings are auto-discovered)

If `CustomerName` needs explicit config:

```csharp
services.AddSculptor(o => o.Bind<Order, OrderDto>(r =>
    r.Property(d => d.CustomerName, p => p.From(s => s.Customer.FullName))));
```

**Lines of code: 3**

### 15.4 Migration Guide from AutoMapper

| AutoMapper | SmartMapp.Net Equivalent |
|---|---|
| `services.AddAutoMapper(assemblies)` | `services.AddSculptor()` |
| `Profile` class | `MappingBlueprint` class (or remove entirely) |
| `CreateMap<S, D>()` | Auto-discovered, or `options.Bind<S, D>()` |
| `ForMember(d => ..., o => o.MapFrom(...))` | `.Property(d => ..., p => p.From(...))` |
| `ForMember(d => ..., o => o.Ignore())` | `.Property(d => ..., p => p.Skip())` or `[Unmapped]` |
| `IValueResolver<S,D,M>` | `IValueProvider<S,D,M>` |
| `ITypeConverter<S,D>` | `ITypeTransformer<S,D>` |
| `mapper.Map<D>(source)` | `sculptor.Map<S, D>(source)` |
| `AssertConfigurationIsValid()` | `options.ValidateOnStartup = true` |
| `ProjectTo<D>(config)` | `source.SelectAs<D>(sculptor)` |
| `.ReverseMap()` | `.Bidirectional()` |
| `.Include<DS, DD>()` | Auto-discovered, or `.ExtendWith<DS, DD>()` |
| `ResolutionContext` | `MappingScope` |
| `.ConstructUsing()` | `.BuildWith()` |
| `.MaxDepth()` | `.DepthLimit()` |
| `.PreserveReferences()` | `.TrackReferences()` |

---

## 16. Working Samples

### 16.1 Sample: Minimal API with SmartMapp.Net

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSculptor();

var app = builder.Build();

app.MapGet("/orders/{id}", async (int id, ISculptor sculptor, AppDbContext db) =>
{
    var order = await db.Orders
        .Include(o => o.Customer)
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.Id == id);

    return order is null
        ? Results.NotFound()
        : Results.Ok(sculptor.Map<Order, OrderDto>(order));
});

app.MapGet("/orders", async (ISculptor sculptor, AppDbContext db) =>
{
    var dtos = await db.Orders.SelectAs<OrderListDto>(sculptor).ToListAsync();
    return Results.Ok(dtos);
});

app.Run();
```

### 16.2 Sample: Complex Domain Mapping

```csharp
// Domain models
public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public Customer Customer { get; set; }
    public Address ShippingAddress { get; set; }
    public Address BillingAddress { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
    public decimal GetTotal() => Lines.Sum(l => l.Quantity * l.UnitPrice);
    public Money Discount { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string GetFullName() => $"{FirstName} {LastName}";
}

public class OrderLine
{
    public int LineId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

// DTOs - SmartMapp.Net maps ALL of these automatically
public record OrderDto
{
    public int Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }     // DateTime -> DateTimeOffset auto-transformed
    public string Status { get; init; }                 // Enum -> string auto-transformed
    public string CustomerFullName { get; init; }       // Flattened from Customer.GetFullName()
    public string CustomerEmail { get; init; }          // Flattened from Customer.Email
    public string ShippingAddressCity { get; init; }    // Flattened from ShippingAddress.City
    public decimal Total { get; init; }                 // Linked to GetTotal() method
    public List<OrderLineDto> Lines { get; init; }      // Collection auto-mapped
}

public record OrderLineDto(int LineId, string ProductName, int Quantity, decimal UnitPrice);

// Usage - no configuration at all
services.AddSculptor();

var dto = sculptor.Map<Order, OrderDto>(order);
```

### 16.3 Sample: Inheritance Hierarchy

```csharp
// Domain
public abstract class Vehicle
{
    public string Make { get; set; }
    public string Model { get; set; }
    public int Year { get; set; }
}
public class Car : Vehicle
{
    public int Doors { get; set; }
    public string FuelType { get; set; }
}
public class Truck : Vehicle
{
    public double PayloadCapacity { get; set; }
    public bool HasTrailer { get; set; }
}
public class ElectricCar : Car
{
    public int RangeKm { get; set; }
    public double BatteryCapacityKwh { get; set; }
}

// DTOs
public abstract record VehicleDto(string Make, string Model, int Year);
public record CarDto(string Make, string Model, int Year, int Doors, string FuelType) : VehicleDto(Make, Model, Year);
public record TruckDto(string Make, string Model, int Year, double PayloadCapacity, bool HasTrailer) : VehicleDto(Make, Model, Year);
public record ElectricCarDto(string Make, string Model, int Year, int Doors, string FuelType, int RangeKm, double BatteryCapacityKwh) : CarDto(Make, Model, Year, Doors, FuelType);

// Auto-discovered polymorphic mapping
List<Vehicle> vehicles = [new Car(), new Truck(), new ElectricCar()];
List<VehicleDto> dtos = sculptor.MapAll<Vehicle, VehicleDto>(vehicles);
// dtos contains [CarDto, TruckDto, ElectricCarDto] with correct types
```

### 16.4 Sample: Mapping Filters and Hooks

```csharp
// Caching filter - cache mapped DTOs for repeated lookups
public class CachingFilter : IMappingFilter
{
    private readonly IMemoryCache _cache;

    public CachingFilter(IMemoryCache cache) => _cache = cache;

    public async Task<object?> ApplyAsync(MappingContext ctx, MappingDelegate next)
    {
        if (ctx.Origin is IHasId entity)
        {
            var key = $"map:{ctx.OriginType.Name}:{ctx.TargetType.Name}:{entity.Id}";
            return await _cache.GetOrCreateAsync(key, async _ => await next(ctx));
        }
        return await next(ctx);
    }
}

// Validation hook
public class ValidationHook : IMappingHook<object, IValidatable>
{
    public void OnMapping(object origin, IValidatable target, MappingScope scope) { }
    public void OnMapped(object origin, IValidatable target, MappingScope scope)
    {
        var errors = target.Validate();
        if (errors.Any())
            throw new MappingValidationException(errors);
    }
}

services.AddSculptor(o =>
{
    o.Filters.Add<CachingFilter>();
    // ValidationHook is auto-discovered
});
```

### 16.5 Sample: Streaming Large Datasets

```csharp
// Stream map from database cursor to API response
app.MapGet("/reports/orders", async (ISculptor sculptor, AppDbContext db, HttpContext http) =>
{
    http.Response.ContentType = "application/json";
    
    var orders = db.Orders.AsAsyncEnumerable();
    var dtos = sculptor.MapStream<Order, OrderReportDto>(orders);

    await JsonSerializer.SerializeAsync(http.Response.Body,
        dtos, cancellationToken: http.RequestAborted);
});
```

### 16.6 Sample: Source Generator (AOT-Safe)

```csharp
// Decorate with attribute - source generator emits mapping code
[MappedBy<Order>]
public partial record OrderDto
{
    public int Id { get; init; }
    public string CustomerFullName { get; init; }

    [Unmapped]
    public string CacheKey { get; init; }
}

// Generated code (visible in IDE, debuggable):
// public partial record OrderDto
// {
//     public static OrderDto MapFromOrder(Order origin) => new()
//     {
//         Id = origin.Id,
//         CustomerFullName = origin.Customer?.GetFullName() ?? string.Empty,
//     };
// }

// The generated mapper is automatically registered and used by ISculptor
var dto = sculptor.Map<Order, OrderDto>(order);  // uses generated code, zero reflection
```

### 16.7 Sample: Multi-Origin Composition

```csharp
// Compose a view model from multiple domain objects
public record DashboardViewModel
{
    public string UserName { get; init; }
    public int OrderCount { get; init; }
    public decimal TotalRevenue { get; init; }
    public List<NotificationDto> RecentNotifications { get; init; }
    public string CompanyName { get; init; }
}

services.AddSculptor(o =>
{
    o.Compose<DashboardViewModel>()
        .FromOrigin<User>(r => r
            .Property(d => d.UserName, p => p.From(s => s.FullName)))
        .FromOrigin<OrderSummary>(r => r
            .Property(d => d.OrderCount, p => p.From(s => s.Count))
            .Property(d => d.TotalRevenue, p => p.From(s => s.Revenue)))
        .FromOrigin<List<Notification>>(r => r
            .Property(d => d.RecentNotifications, p => p.From(s => s)))
        .FromOrigin<CompanyInfo>(r => r
            .Property(d => d.CompanyName, p => p.From(s => s.Name)));
});

var vm = sculptor.Compose<DashboardViewModel>(user, orderSummary, notifications, companyInfo);
```

### 16.8 Sample: Collection Merging (EF Core Update Pattern)

```csharp
public class OrderUpdateService
{
    private readonly ISculptor _sculptor;
    private readonly AppDbContext _db;

    public async Task UpdateOrder(int id, OrderUpdateDto dto)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .FirstAsync(o => o.Id == id);

        // Maps dto onto existing order, merging the Lines collection:
        // - Matching lines are updated in-place
        // - New lines are added
        // - Missing lines are removed
        _sculptor.Map(dto, order);

        await _db.SaveChangesAsync();
    }
}

services.AddSculptor(o =>
{
    o.Bind<OrderUpdateDto, Order>(r => r
        .Property(d => d.Lines, p => p.Merge(
            matchBy: (origin, target) => origin.LineId == target.LineId)));
});
```

---

## 17. Test Plan

### 17.1 Test Categories

| Category | Description | Approx. Count |
|---|---|---|
| **Unit: Convention Engine** | Name linking, flattening, unflattening, case conversion | 80+ |
| **Unit: Type Transformers** | All built-in type transformers | 60+ |
| **Unit: Collection Mapping** | All collection type combinations | 40+ |
| **Unit: Inheritance** | Polymorphic, discriminator, deep hierarchies | 30+ |
| **Unit: Construction** | All construction strategies | 20+ |
| **Unit: Fluent API** | All fluent binding options | 50+ |
| **Unit: Attributes** | All attribute-based configurations | 25+ |
| **Integration: DI** | Service registration, resolution, keyed services | 20+ |
| **Integration: EF Core** | SelectAs, collection merge, SaveChanges patterns | 15+ |
| **Integration: ASP.NET Core** | Filters, endpoints, health checks | 10+ |
| **Integration: OpenTelemetry** | Metrics, traces, spans | 10+ |
| **Performance: Benchmarks** | BenchmarkDotNet comparisons vs AutoMapper, Mapster | 20+ |
| **Performance: Memory** | Allocation tracking, no-alloc verification | 10+ |
| **Performance: Parallel** | Parallel collection correctness and speedup | 10+ |
| **Edge Cases** | Nulls, empty collections, circular refs, max depth | 30+ |
| **Source Generator** | Generated code correctness, AOT scenarios | 20+ |
| **Thread Safety** | Concurrent mapping, race conditions | 15+ |
| **Total** | | **~465+** |

### 17.2 Key Test Scenarios

#### 17.2.1 Zero-Config Auto-Discovery Tests

```csharp
[Fact]
public void Should_auto_discover_and_map_matching_types()
{
    var sculptor = new SculptorBuilder()
        .ScanAssembliesContaining<Order>()
        .Forge();

    var order = CreateSampleOrder();
    var dto = sculptor.Map<Order, OrderDto>(order);

    Assert.Equal(order.Id, dto.Id);
    Assert.Equal(order.Customer.Email, dto.CustomerEmail);
    Assert.Equal(order.Customer.GetFullName(), dto.CustomerFullName);
}

[Fact]
public void Should_auto_flatten_nested_properties()
{
    var sculptor = new SculptorBuilder().Forge();
    var order = CreateSampleOrder();

    var flat = sculptor.Map<Order, OrderFlatDto>(order);

    Assert.Equal(order.Customer.FirstName, flat.CustomerFirstName);
    Assert.Equal(order.Customer.Address.City, flat.CustomerAddressCity);
}

[Fact]
public void Should_auto_unflatten_to_nested_properties()
{
    var sculptor = new SculptorBuilder().Forge();
    var flat = new OrderFlatDto
    {
        CustomerFirstName = "Alice",
        CustomerAddressCity = "Seattle"
    };

    var order = sculptor.Map<OrderFlatDto, Order>(flat);

    Assert.Equal("Alice", order.Customer.FirstName);
    Assert.Equal("Seattle", order.Customer.Address.City);
}
```

#### 17.2.2 Polymorphic Mapping Tests

```csharp
[Fact]
public void Should_map_polymorphic_types_without_configuration()
{
    var sculptor = new SculptorBuilder()
        .ScanAssembliesContaining<Shape>()
        .Forge();

    Map map = new Circle { X = 1, Y = 2, Radius = 5 };
    var dto = sculptor.Map<Shape, ShapeDto>(shape);

    Assert.IsType<CircleDto>(dto);
    Assert.Equal(5, ((CircleDto)dto).Radius);
}

[Fact]
public void Should_map_collection_of_polymorphic_types()
{
    var sculptor = new SculptorBuilder().Forge();
    List<Shape> maps = [new Circle(), new Rectangle(), new Circle()];

    var dtos = sculptor.MapAll<Shape, ShapeDto>(shapes);

    Assert.IsType<CircleDto>(dtos[0]);
    Assert.IsType<RectangleDto>(dtos[1]);
    Assert.IsType<CircleDto>(dtos[2]);
}
```

#### 17.2.3 Circular Reference Tests

```csharp
[Fact]
public void Should_handle_circular_references_without_infinite_loop()
{
    var sculptor = new SculptorBuilder().Forge();
    var emp = new Employee { Name = "Alice" };
    var mgr = new Employee { Name = "Bob" };
    emp.Manager = mgr;
    mgr.DirectReports = new List<Employee> { emp };

    var dto = sculptor.Map<Employee, EmployeeDto>(emp);

    Assert.Equal("Alice", dto.Name);
    Assert.Equal("Bob", dto.Manager.Name);
    Assert.Same(dto, dto.Manager.DirectReports[0]); // same reference preserved
}
```

#### 17.2.4 Parallel Collection Tests

```csharp
[Fact]
public async Task Should_map_large_collections_in_parallel()
{
    var sculptor = new SculptorBuilder()
        .Configure(o => o.Throughput.ParallelCollectionThreshold = 100)
        .Forge();

    var orders = Enumerable.Range(0, 10_000)
        .Select(i => new Order { Id = i })
        .ToList();

    var dtos = sculptor.MapAll<Order, OrderDto>(orders);

    Assert.Equal(10_000, dtos.Count);
    Assert.All(dtos, (dto, i) => Assert.Equal(i, dto.Id));
}
```

#### 17.2.5 Thread Safety Tests

```csharp
[Fact]
public void Should_be_thread_safe_for_concurrent_mapping()
{
    var sculptor = new SculptorBuilder().Forge();
    var exceptions = new ConcurrentBag<Exception>();

    Parallel.For(0, 10_000, i =>
    {
        try
        {
            var order = new Order { Id = i, Customer = new Customer { FirstName = $"User{i}" } };
            var dto = sculptor.Map<Order, OrderDto>(order);
            Assert.Equal(i, dto.Id);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }
    });

    Assert.Empty(exceptions);
}
```

#### 17.2.6 Performance Tests

```csharp
[MemoryDiagnoser]
public class FlatMappingBenchmark
{
    private readonly ISculptor _sculptor;
    private readonly IMapper _autoMapper;
    private readonly Order _order;

    [GlobalSetup]
    public void Setup()
    {
        _sculptor = new SculptorBuilder().Forge();
        _autoMapper = new MapperConfiguration(c => c.CreateMap<Order, OrderDto>()).CreateMapper();
        _order = CreateSampleOrder();

        // Warm up
        _sculptor.Map<Order, OrderDto>(_order);
        _autoMapper.Map<OrderDto>(_order);
    }

    [Benchmark(Baseline = true)]
    public OrderDto SmartMappNet() => _sculptor.Map<Order, OrderDto>(_order);

    [Benchmark]
    public OrderDto AutoMapper() => _autoMapper.Map<OrderDto>(_order);
}
```

### 17.3 Test Infrastructure

```
tests/
  SmartMapp.Net.Tests.Unit/
    Conventions/
    Transformers/
    Collections/
    Inheritance/
    Construction/
    FluentApi/
    Attributes/
    EdgeCases/
  SmartMapp.Net.Tests.Integration/
    DependencyInjection/
    EntityFrameworkCore/
    AspNetCore/
    OpenTelemetry/
  SmartMapp.Net.Tests.Performance/
    Benchmarks/
    MemoryTests/
    ParallelTests/
  SmartMapp.Net.Tests.Codegen/
    GeneratedCodeTests/
    AotTests/
```

---

## 18. Package and Distribution

### 18.1 NuGet Packages

| Package | Dependencies | Size Target |
|---|---|---|
| `SmartMapp.Net` | None (zero dependencies) | < 150 KB |
| `SmartMapp.Net.Codegen` | Microsoft.CodeAnalysis (analyzer) | < 100 KB |
| `SmartMapp.Net.DependencyInjection` | SmartMapp.Net, Microsoft.Extensions.DependencyInjection.Abstractions | < 30 KB |
| `SmartMapp.Net.AspNetCore` | SmartMapp.Net.DependencyInjection, Microsoft.AspNetCore.* | < 50 KB |
| `SmartMapp.Net.Insights` | SmartMapp.Net, OpenTelemetry.Api | < 50 KB |
| `SmartMapp.Net.Validation` | SmartMapp.Net, FluentValidation | < 30 KB |

### 18.2 Supported Target Frameworks

| Framework | Support Level |
|---|---|
| .NET 9 | Full (all features) |
| .NET 8 | Full (all features including FrozenDictionary, keyed services) |
| .NET Standard 2.1 | Core features (no codegen, no SIMD, no keyed services) |

### 18.3 Repository Structure

```
smartmapp.net/
  src/
    SmartMapp.Net/
      Conventions/           # Naming conventions, type pairing
      Transformers/          # Built-in type transformers
      Engine/                # IL Emit, expression compilation, mapping execution
      Configuration/         # Fluent API, blueprints, options
      Discovery/             # Assembly scanning, type analysis
      Collections/           # Collection mapping, parallel mapping
      Filters/               # Mapping filters, hooks
      Caching/               # Multi-level cache
      Diagnostics/           # Inspection, atlas, debug views
      Extensions/            # Object extensions, LINQ extensions
    SmartMapp.Net.Codegen/
      Analyzers/             # Roslyn analyzers for compile-time checks
      Generators/            # Incremental source generators
    SmartMapp.Net.DependencyInjection/
    SmartMapp.Net.AspNetCore/
    SmartMapp.Net.Insights/
    SmartMapp.Net.Validation/
  tests/
    SmartMapp.Net.Tests.Unit/
    SmartMapp.Net.Tests.Integration/
    SmartMapp.Net.Tests.Performance/
    SmartMapp.Net.Tests.Codegen/
  samples/
    SmartMapp.Net.Samples.MinimalApi/
    SmartMapp.Net.Samples.WebApp/
    SmartMapp.Net.Samples.Console/
    SmartMapp.Net.Samples.Blazor/
  benchmarks/
    SmartMapp.Net.Benchmarks/
  docs/
    requirements/
    api/
    guides/
  README.md
  LICENSE
  Directory.Build.props
  Directory.Packages.props
  SmartMapp.Net.sln
```

### 18.4 CI/CD Pipeline

```yaml
# Key stages:
# 1. Build all projects
# 2. Run unit tests
# 3. Run integration tests
# 4. Run source generator tests
# 5. Run benchmarks (store results for regression tracking)
# 6. Mutation testing (Stryker.NET)
# 7. Code coverage (>90% target)
# 8. NuGet package creation
# 9. Publish to NuGet (on tag)
```

### 18.5 Versioning

- Follows **Semantic Versioning 2.0**.
- Pre-release packages use `-alpha`, `-beta`, `-rc` suffixes.
- Public API changes require a major version bump.
- Performance regression > 10% blocks release.

---

## 19. Roadmap

### Phase 1: Core (v1.0)

- [x] Convention engine (name linking, flattening, unflattening)
- [x] Expression compilation engine
- [x] Fluent binding API
- [x] Blueprint-based configuration
- [x] Attribute-based configuration
- [x] All built-in type transformers
- [x] Collection mapping (all standard types)
- [x] Inheritance and polymorphic mapping
- [x] Circular reference handling
- [x] Record and init-only support
- [x] Nested object mapping
- [x] DI integration (`AddSculptor`)
- [x] Configuration validation
- [x] Unit test suite (400+ tests)
- [x] BenchmarkDotNet suite
- [x] Documentation and samples

### Phase 2: Performance (v1.1)

- [ ] IL Emit engine with adaptive promotion
- [ ] SIMD-accelerated primitive collection copy
- [ ] Object pooling (ArrayPool, ObjectPool)
- [ ] FrozenDictionary cache optimization (.NET 8+)
- [ ] Parallel collection mapping

### Phase 3: Advanced (v1.2)

- [ ] Source generator (`SmartMapp.Net.Codegen`)
- [ ] Mapping filter pipeline
- [ ] Mapping hooks
- [ ] Multi-origin composition
- [ ] Collection merging
- [ ] Streaming mapping (IAsyncEnumerable)
- [ ] Lazy mapping (deferred IEnumerable)

### Phase 4: Ecosystem (v1.3)

- [ ] ASP.NET Core integration package
- [ ] OpenTelemetry instrumentation
- [ ] Mapping atlas visualizer
- [ ] Health checks
- [ ] FluentValidation integration
- [ ] Diagnostic endpoints

### Phase 5: Intelligence (v2.0)

- [ ] `ExpandoObject` / `dynamic` mapping
- [ ] Dictionary-to-object deep mapping
- [ ] Tuple mapping enhancements
- [ ] Custom expression tree visitors for SelectAs
- [ ] Roslyn analyzer warnings for common mistakes
- [ ] Mapping code fix providers (IDE quick-fixes)
- [ ] Performance profiler integration (dotTrace, PerfView)

---

## 20. Glossary

| Term | Definition |
|---|---|
| **Blueprint** | Immutable instruction set describing how every member of a target type is populated from an origin type. |
| **Property Link** | A single instruction within a Blueprint that describes how one target member gets its value. |
| **Convention** | A rule that automatically links origin members to target members (e.g., name linking, flattening). |
| **Value Provider** | A component that extracts a value from the origin object for a specific target member. |
| **Type Transformer** | A component that converts a value from one type to another (e.g., string to int). |
| **Mapping Strategy** | The code generation approach used for a mapping (IL Emit, Source Generated, Expression Compiled, Interpreted). |
| **Adaptive Promotion** | The process of automatically upgrading a mapping from Expression Compiled to IL Emit after a usage threshold is reached. |
| **Flattening** | Mapping nested origin properties to flat target properties (e.g., `Customer.Address.City` to `CustomerAddressCity`). |
| **Unflattening** | The reverse of flattening: mapping flat origin properties to nested target properties. |
| **Type Pair** | An `(OriginType, TargetType)` tuple that uniquely identifies a mapping configuration. |
| **Mapping Scope** | Per-mapping context object carrying state like depth, visited objects, and service provider. |
| **Mapping Filter** | A pipeline component that wraps the mapping execution, forming a chain-of-responsibility. |
| **Mapping Hook** | A typed before/after callback that runs for specific origin/target type combinations. |
| **Projection** | Converting a blueprint into an `Expression<Func<S,D>>` for IQueryable translation (e.g., EF Core SQL generation). |
| **Addon** | A modular extension that can add conventions, transformers, filters, or other features to SmartMapp.Net. |
| **Hot Path** | A mapping that is invoked frequently and is eligible for IL Emit promotion. |
| **Cold Path** | A mapping that is invoked infrequently and uses the Expression Compiled strategy. |
| **Codegen** | A Roslyn incremental generator that emits mapping code at compile time, eliminating runtime reflection. |
| **Structural Similarity** | A scoring algorithm that determines how well two types match based on their member names and types. |
| **Sculptor** | The primary entry point and runtime engine for all mapping operations. |
| **Forge** | The act of building and freezing a Sculptor's configuration, making it immutable and thread-safe. |

---

*End of Specification*

*SmartMapp.Net: Less code. More features. Better performance.*
