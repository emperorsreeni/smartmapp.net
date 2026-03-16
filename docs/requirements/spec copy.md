# LiteMapper - Comprehensive Library Specification

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

**LiteMapper** is a high-performance, zero-configuration object-to-object mapping library for .NET that automatically discovers and maps entities without requiring explicit configuration. It is designed to be a drop-in replacement for AutoMapper while offering superior performance, reduced boilerplate, and greater extensibility.

### AutoMapper vs LiteMapper At A Glance

| Dimension | AutoMapper | LiteMapper |
|---|---|---|
| **Configuration** | Requires Profile classes, CreateMap calls | Zero-config auto-discovery; opt-in fluent overrides |
| **Performance** | Reflection + expression tree compilation | IL Emit + source generators + SIMD-accelerated collection copy |
| **Memory** | Allocates intermediate expression trees | Near-zero allocation hot path via pooled buffers |
| **Boilerplate** | CreateMap, ForMember, Profile, AddAutoMapper | Single `services.AddLiteMapper()` - done |
| **Complex Scenarios** | Manual config for inheritance, polymorphism | Automatic polymorphic mapping, deep graph tracking |
| **Extensibility** | IValueResolver, ITypeConverter | Plugin pipeline, middleware, interceptors, custom conventions |
| **Parallelism** | None | Automatic parallel collection mapping with configurable thresholds |
| **Diagnostics** | Limited AssertConfigurationIsValid | Real-time mapping telemetry, OpenTelemetry, mapping graph visualizer |
| **AOT/Trimming** | Not trim-safe | Source generator mode is fully AOT and trimmer safe |

### Key Differentiators

- **Zero-Config Mapping** - Scans assemblies at startup; matches by name, type, and structural similarity.
- **Compile-Time Safety** - Optional source generator produces mapping code at build time; errors surface as compiler warnings.
- **Pluggable Pipeline** - Every mapping flows through a middleware pipeline (pre-map, resolve, map, post-map) that users can intercept.
- **Parallel Collections** - Collections above a configurable threshold are mapped in parallel using `Parallel.ForEachAsync` with work-stealing.
- **Adaptive Caching** - Hot mappings are promoted to IL-emitted delegates; cold mappings fall back to compiled expressions.
- **Sub-100ns Simple Maps** - Target: mapping a flat 10-property DTO completes in under 100 nanoseconds after warm-up.

---

## 2. Design Philosophy

### 2.1 Principles

| # | Principle | Implication |
|---|---|---|
| P1 | **Convention over Configuration** | Mapping "just works" for 95% of cases with zero setup. |
| P2 | **Pit of Success** | The easiest API to use is also the most correct and performant. |
| P3 | **Pay for What You Use** | Features like parallel mapping, diagnostics, and middleware add zero overhead when unused. |
| P4 | **Transparency** | Every mapping decision is traceable. Users can ask "why did property X get value Y?" and get an answer. |
| P5 | **Immutable Configuration** | Once the mapper is built, its configuration is frozen. This enables aggressive caching and thread safety. |
| P6 | **Modern C# First** | Leverages `init` properties, records, `required` members, `Span<T>`, generic math, and interceptors. |
| P7 | **Minimal API Surface** | Small public API that is hard to misuse. Internal complexity hidden behind clean abstractions. |

### 2.2 Non-Goals

- LiteMapper is **not** an ORM or serialization library.
- LiteMapper does **not** perform database queries or HTTP calls during mapping (services can be injected for resolution, but I/O is the caller's responsibility).
- LiteMapper does **not** map `dynamic` or `ExpandoObject` in v1.0 (planned for a future release).

---

## 3. Architecture Overview

### 3.1 High-Level Architecture

```
+------------------------------------------------------------------+
|                        Public API Layer                           |
|  ILiteMapper . IMapper<TSrc,TDst> . Fluent Config . Extensions   |
+------------------------------------------------------------------+
|                      Middleware Pipeline                          |
|  Pre-Map -> Convention Resolution -> Mapping Execution -> Post   |
+------------------------------------------------------------------+
|                       Mapping Engine                              |
|  +------------+ +----------------+ +---------------------------+ |
|  | IL Emitter  | | Source Gen     | | Expression Compiler      | |
|  | (hot path)  | | (build-time)   | | (fallback / cold path)   | |
|  +------------+ +----------------+ +---------------------------+ |
+------------------------------------------------------------------+
|                   Convention Engine                               |
|  Name Match . Flattening . Unflattening . Type Coercion . Custom |
+------------------------------------------------------------------+
|                 Discovery & Metadata Layer                        |
|  Assembly Scanner . Type Analyzer . Mapping Plan Builder         |
+------------------------------------------------------------------+
|                    Infrastructure                                |
|  Object Pool . Parallel Scheduler . Cache . Diagnostics . DI    |
+------------------------------------------------------------------+
```

### 3.2 Package Structure

| Package | Description |
|---|---|
| `LiteMapper` | Core library: mapping engine, conventions, fluent API |
| `LiteMapper.SourceGen` | Roslyn source generator for compile-time mapping code emission |
| `LiteMapper.DependencyInjection` | `IServiceCollection` extensions for DI registration |
| `LiteMapper.AspNetCore` | Model-binding integration, request/response mapping middleware |
| `LiteMapper.Diagnostics` | OpenTelemetry, mapping graph visualizer, health checks |
| `LiteMapper.FluentValidation` | Pre/post-map validation integration with FluentValidation |

### 3.3 Dependency Graph

```
LiteMapper.AspNetCore -----> LiteMapper.DependencyInjection
LiteMapper.Diagnostics --+        |
LiteMapper.FluentValidation -+    |
                              |    v
                              +--> LiteMapper (core)
                                       ^
LiteMapper.SourceGen ------------------+  (analyzer/generator reference)
```

---

## 4. Core Engine

### 4.1 Mapping Plan

Every `(TSource, TDestination)` pair produces a **MappingPlan** - an immutable, pre-computed instruction set describing how each destination member is populated.

```csharp
public sealed record MappingPlan
{
    public Type SourceType { get; init; }
    public Type DestinationType { get; init; }
    public IReadOnlyList<MemberBinding> Bindings { get; init; }
    public MappingStrategy Strategy { get; init; }          // Emit | Compiled | Interpreted
    public bool IsParallelEligible { get; init; }
    public IReadOnlyList<IMappingMiddleware> Pipeline { get; init; }
}

public sealed record MemberBinding
{
    public MemberInfo DestinationMember { get; init; }
    public IValueResolver Resolver { get; init; }           // How the value is obtained
    public IValueConverter? Converter { get; init; }        // Optional type conversion
    public ConventionMatch MatchedBy { get; init; }         // Traceability
    public bool IsIgnored { get; init; }
    public object? DefaultValue { get; init; }
}
```

### 4.2 Mapping Execution Modes

| Mode | When Used | Characteristics |
|---|---|---|
| **IL Emit** | Hot paths (>10 invocations, auto-promoted) | Fastest; near-manual-code speed via `DynamicMethod` |
| **Source Generated** | Build-time opt-in via `[MapFrom<T>]` attribute | Zero runtime overhead; AOT friendly; trimmer safe |
| **Expression Compiled** | First-call fallback | Fast compilation; moderate speed; serves as warm-up |
| **Interpreted** | Debugging / diagnostics mode | Slowest; full traceability of every step |

### 4.3 Mapping Lifecycle

```
Source Instance
    |
    v
[Pre-Map Middleware]          <-- validation, logging, transformation
    |
    v
[Resolve Destination]         <-- construct or reuse existing instance
    |
    v
[For Each MemberBinding]
    |-- Resolve source value (property, method, flattening, custom resolver)
    |-- Convert if types differ (built-in converters -> custom converters)
    |-- Recurse if complex type (with circular reference tracking)
    |-- Assign to destination member
    |
    v
[Post-Map Middleware]         <-- auditing, caching, side-effects
    |
    v
Destination Instance
```

### 4.4 Object Construction

LiteMapper supports multiple strategies for constructing destination objects:

| Strategy | Description |
|---|---|
| **Parameterless Constructor** | Default. Uses `new T()` or `Activator.CreateInstance<T>()`. |
| **Best-Match Constructor** | Selects the constructor whose parameters best match source properties by name and type. |
| **Record/Primary Constructor** | Automatically maps to `record` positional parameters. |
| **Factory Function** | User-supplied `Func<TSource, TDestination>` via `.ConstructUsing()`. |
| **Service Provider** | Resolves destination from DI container via `.ConstructUsing(sp => ...)`. |
| **Existing Instance** | Maps onto a pre-existing object via `mapper.Map(source, existingDest)`. |
| **Interface Proxy** | Generates runtime proxy for interface destinations via `DispatchProxy`. |

```csharp
// Best-match constructor: LiteMapper matches source props to ctor params
public record OrderDto(int Id, string CustomerName, decimal Total);

// No configuration needed - LiteMapper detects the primary constructor,
// matches Id, CustomerName (flattened from Customer.Name), and Total.
var dto = mapper.Map<Order, OrderDto>(order);
```

---

## 5. Auto-Discovery and Convention-Based Mapping

### 5.1 Assembly Scanning

At startup, LiteMapper scans loaded assemblies (or a user-specified subset) to discover:

1. **Mappable type pairs** - types whose names follow configurable patterns (e.g., `Order` to `OrderDto`).
2. **Mapping profiles** - classes implementing `ILiteMapperProfile`.
3. **Custom resolvers and converters** - classes implementing `IValueResolver<,>` or `IValueConverter<,>`.
4. **Attributed mappings** - types decorated with `[MapTo<T>]` or `[MapFrom<T>]`.

```csharp
// Zero-config: just register and go
services.AddLiteMapper();  // scans calling assembly

// Explicit assembly list
services.AddLiteMapper(options =>
{
    options.ScanAssemblies(typeof(Order).Assembly, typeof(OrderDto).Assembly);
    options.ScanAssembliesContaining<Startup>();
});
```

### 5.2 Naming Conventions

LiteMapper ships with a rich set of built-in naming conventions and allows custom ones:

| Convention | Example Source | Example Destination | Match |
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
| **Attributed** | `[MapTo("FullName")]` | `FullName` | Yes |
| **Method to Property** | `GetFullName()` | `FullName` | Yes |

### 5.3 Type Suffix Auto-Pairing

LiteMapper auto-discovers type pairs by matching base names with known suffixes:

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

When naming conventions do not produce a match, LiteMapper falls back to structural similarity scoring:

```
Score = (MatchedProperties / TotalDestinationProperties) x 100
```

If the score exceeds a configurable threshold (default: 70%), the pair is registered as a candidate mapping with a warning in diagnostics.

### 5.5 Custom Conventions

```csharp
public class PrefixDroppingConvention : IMemberConvention
{
    public bool TryResolve(
        MemberInfo destinationMember,
        TypeModel sourceType,
        out IValueResolver? resolver)
    {
        // Strip "Dst" prefix and look for matching source member
        var name = destinationMember.Name;
        if (name.StartsWith("Dst"))
        {
            var sourceName = name[3..];
            var sourceMember = sourceType.GetMember(sourceName);
            if (sourceMember != null)
            {
                resolver = new PropertyResolver(sourceMember);
                return true;
            }
        }
        resolver = null;
        return false;
    }
}

// Register globally
options.Conventions.Add<PrefixDroppingConvention>();
```

---

## 6. Fluent Configuration API

### 6.1 Profile-Based Configuration

```csharp
public class OrderMappingProfile : LiteMapperProfile
{
    public override void Configure(IProfileExpression config)
    {
        config.CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerFullName,
                       o => o.MapFrom(s => $"{s.Customer.FirstName} {s.Customer.LastName}"))
            .ForMember(d => d.Total,
                       o => o.MapFrom(s => s.Lines.Sum(l => l.Quantity * l.UnitPrice)))
            .ForMember(d => d.InternalCode,
                       o => o.Ignore())
            .AfterMap((src, dst) => dst.MappedAt = DateTime.UtcNow);
    }
}
```

### 6.2 Inline Configuration (No Profile Required)

```csharp
services.AddLiteMapper(options =>
{
    options.CreateMap<Order, OrderDto>(map => map
        .ForMember(d => d.CustomerFullName, o => o.MapFrom(s => s.Customer.FullName))
        .ForMember(d => d.Total, o => o.Condition(s => s.Status != OrderStatus.Cancelled))
        .MaxDepth(3)
    );
});
```

### 6.3 Attribute-Based Configuration (Zero Boilerplate)

```csharp
[MapFrom<Order>]
public record OrderDto
{
    public int Id { get; init; }
    public string CustomerFullName { get; init; }          // Auto-flattened from Customer.FullName

    [MapFrom(nameof(Order.Lines), Transform = "Sum(Quantity * UnitPrice)")]
    public decimal Total { get; init; }

    [IgnoreMap]
    public string InternalCode { get; init; }
}
```

### 6.4 Global Configuration

```csharp
services.AddLiteMapper(options =>
{
    // Global conventions
    options.Conventions.SourcePrefixes("Get", "Str", "m_");
    options.Conventions.DestinationSuffixes("Field", "Property");
    options.Conventions.EnableSnakeCaseMapping();
    options.Conventions.EnableAbbreviationExpansion(aliases =>
    {
        aliases.Add("Addr", "Address");
        aliases.Add("Qty", "Quantity");
    });

    // Global null handling
    options.NullHandling.SourceMemberNullSubstitute = string.Empty;  // for strings
    options.NullHandling.ThrowOnNullSource = false;
    options.NullHandling.UseDefaultForNullDestination = true;

    // Global performance tuning
    options.Performance.ParallelCollectionThreshold = 1000;
    options.Performance.MaxDegreeOfParallelism = Environment.ProcessorCount;
    options.Performance.EnableILEmit = true;
    options.Performance.EnableAdaptivePromotion = true;
    options.Performance.AdaptivePromotionThreshold = 10;

    // Global depth limit (prevents infinite recursion)
    options.MaxRecursionDepth = 10;
});
```

### 6.5 Fluent API Complete Reference

```csharp
IMapExpression<TSource, TDestination>
    // Member configuration
    .ForMember(destExpr, memberOptions)
    .ForAllMembers(memberOptions)
    .ForAllOtherMembers(memberOptions)

    // Source member configuration
    .ForSourceMember(srcExpr, sourceOptions)

    // Construction
    .ConstructUsing(factoryExpr)
    .ConstructUsing(serviceProvider)

    // Lifecycle hooks
    .BeforeMap(action)
    .AfterMap(action)

    // Conditions
    .Condition(predicate)
    .ForMember(d => d.X, o => o.Condition(s => s.Y != null))

    // Null handling
    .NullSubstitute(value)

    // Inheritance
    .IncludeBase<TBaseSource, TBaseDestination>()
    .IncludeDerived<TDerivedSource, TDerivedDestination>()
    .AsProxy()   // map to runtime-generated proxy for interfaces

    // Depth control
    .MaxDepth(n)
    .PreserveReferences()

    // Reverse mapping
    .ReverseMap()

    // Conversion
    .ConvertUsing(converter)
    .ConvertUsing<TConverter>()

    // Validation
    .ValidateOnMap(validator)

    // Performance hints
    .PreferILEmit()
    .PreferSourceGenerated()
    .DisableParallelCollections()

    // Middleware
    .UsePipeline(pipeline => pipeline
        .Use<LoggingMiddleware>()
        .Use<CachingMiddleware>()
    );

IMemberOptions<TSource, TDestination, TMember>
    .MapFrom(sourceExpr)
    .MapFrom<TResolver>()          // DI-resolved resolver
    .MapFromServiceProvider<TService>(serviceExpr)
    .Ignore()
    .UseDefaultValue(value)
    .Condition(predicate)
    .ConvertUsing(converter)
    .NullSubstitute(value)
    .SetMappingOrder(int)
    .AddTransform(transformExpr)   // post-assignment transform (e.g., Trim())
```

---

## 7. Type Mapping Strategies

### 7.1 Built-in Type Converters

LiteMapper ships with a comprehensive set of built-in converters that handle common type transformations without configuration:

| Source | Destination | Strategy |
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

LiteMapper detects and uses `implicit`/`explicit` cast operators defined on either source or destination types:

```csharp
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }

    public static implicit operator decimal(Money m) => m.Amount;
    public static explicit operator Money(decimal d) => new() { Amount = d, Currency = "USD" };
}

// LiteMapper auto-detects: Money -> decimal uses implicit operator
// decimal -> Money uses explicit operator (opt-in via config)
```

### 7.3 Enum Mapping

```csharp
// By name (default) - OrderStatus.Pending -> OrderStatusDto.Pending
// By value - OrderStatus.Pending (0) -> OrderStatusDto (0)
// By attribute - [MapToEnum(OrderStatusDto.InProgress)] on source enum member
// With fallback - unmapped values use a configurable default

options.EnumMapping.Strategy = EnumMappingStrategy.ByName;  // default
options.EnumMapping.CaseInsensitive = true;                 // default
options.EnumMapping.FallbackValue<OrderStatusDto>(OrderStatusDto.Unknown);
```

### 7.4 String Transformations

```csharp
options.StringTransforms.TrimAll = true;
options.StringTransforms.NullToEmpty = true;
options.StringTransforms.Apply(s => s.Normalize(NormalizationForm.FormC));
```

### 7.5 Custom Value Converters

```csharp
public class MoneyToStringConverter : IValueConverter<Money, string>
{
    public string Convert(Money source, ResolutionContext context)
        => $"{source.Currency} {source.Amount:N2}";
}

// Register globally
options.AddConverter<MoneyToStringConverter>();

// Or per-member
config.CreateMap<Order, OrderDto>()
    .ForMember(d => d.TotalDisplay, o => o.ConvertUsing<MoneyToStringConverter>());
```

### 7.6 Custom Value Resolvers

```csharp
public class FullNameResolver : IValueResolver<Customer, CustomerDto, string>
{
    public string Resolve(Customer source, CustomerDto destination,
                          string destMember, ResolutionContext context)
        => $"{source.Title} {source.FirstName} {source.LastName}".Trim();
}

// Usage
config.CreateMap<Customer, CustomerDto>()
    .ForMember(d => d.FullName, o => o.MapFrom<FullNameResolver>());
```

### 7.7 Conditional Mapping

```csharp
config.CreateMap<Order, OrderDto>()
    // Map only when condition is met
    .ForMember(d => d.ShippingAddress, o => o.Condition(s => s.IsShipped))
    // Pre-condition: skip entire member resolution if false
    .ForMember(d => d.Discount, o => o.PreCondition(s => s.HasDiscount))
    // Type-level condition: skip entire mapping
    .Condition(s => s.Status != OrderStatus.Deleted);
```

---

## 8. Complex Scenario Support

### 8.1 Inheritance and Polymorphism

LiteMapper automatically detects inheritance hierarchies and maps polymorphic types without explicit configuration.

#### Automatic Polymorphic Mapping

```csharp
// Domain model
public abstract class Shape { public double X { get; set; } public double Y { get; set; } }
public class Circle : Shape { public double Radius { get; set; } }
public class Rectangle : Shape { public double Width { get; set; } public double Height { get; set; } }

// DTOs
public abstract class ShapeDto { public double X { get; set; } public double Y { get; set; } }
public class CircleDto : ShapeDto { public double Radius { get; set; } }
public class RectangleDto : ShapeDto { public double Width { get; set; } public double Height { get; set; } }

// LiteMapper auto-discovers the hierarchy. No configuration needed.
Shape shape = new Circle { X = 1, Y = 2, Radius = 5 };
ShapeDto dto = mapper.Map<Shape, ShapeDto>(shape);
// dto is CircleDto { X = 1, Y = 2, Radius = 5 }
```

#### Explicit Inheritance Configuration (when auto-discovery is insufficient)

```csharp
config.CreateMap<Shape, ShapeDto>()
    .IncludeDerived<Circle, CircleDto>()
    .IncludeDerived<Rectangle, RectangleDto>();
```

#### Discriminator-Based Mapping

```csharp
config.CreateMap<Shape, ShapeDto>()
    .DiscriminateBy(s => s.GetType().Name)
    .When("Circle", map => map.MapTo<CircleDto>())
    .When("Rectangle", map => map.MapTo<RectangleDto>())
    .Default(map => map.MapTo<ShapeDto>());
```

### 8.2 Nested Objects

Nested objects are mapped recursively by default. LiteMapper detects nested complex types and applies the appropriate mapping plan.

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
var dto = mapper.Map<Order, OrderDto>(order);
```

### 8.3 Collections

LiteMapper handles all standard .NET collection types:

| Source Type | Destination Type | Strategy |
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
options.Performance.ParallelCollectionThreshold = 1000;  // default
options.Performance.MaxDegreeOfParallelism = Environment.ProcessorCount;

// Disable per-map
config.CreateMap<Order, OrderDto>()
    .DisableParallelCollections();

// Force parallel regardless of threshold
config.CreateMap<Order, OrderDto>()
    .ForceParallelCollections();
```

#### Collection Merging (Update Existing Collections)

```csharp
// Instead of replacing, merge source into existing destination collection
config.CreateMap<Order, OrderDto>()
    .ForMember(d => d.Lines, o => o.MergeCollection(
        matchBy: (src, dst) => src.LineId == dst.LineId,
        onAdd: line => { /* new item */ },
        onRemove: line => { /* removed item */ },
        onUpdate: (src, dst) => { /* updated item */ }
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
var dto = mapper.Map<Order, OrderFlatDto>(order);
```

#### Automatic Unflattening

```csharp
// Reverse: OrderFlatDto -> Order also works automatically
var order = mapper.Map<OrderFlatDto, Order>(dto);
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

// LiteMapper tracks visited objects via identity map.
// Circular references produce the same destination instance (no infinite loop).
options.PreserveReferences = true;  // default: true

// Or limit depth
config.CreateMap<Employee, EmployeeDto>()
    .MaxDepth(5);
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

var customer = mapper.Map<Dictionary<string, object>, CustomerDto>(dict);

// Object to dictionary
var dict2 = mapper.Map<CustomerDto, Dictionary<string, object>>(customer);
```

### 8.7 Tuple Mapping

```csharp
// ValueTuple to object
var tuple = (Id: 1, Name: "Alice", Age: 30);
var dto = mapper.Map<(int Id, string Name, int Age), PersonDto>(tuple);

// Object to ValueTuple
var tuple2 = mapper.Map<PersonDto, (int Id, string Name, int Age)>(dto);
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

// LiteMapper uses the constructor for records with positional params
// and uses init-only setters for records with init properties.
// Both work without configuration.
```

### 8.9 Interface and Abstract Type Mapping

```csharp
// Map to an interface - LiteMapper generates a runtime proxy
ICustomerDto dto = mapper.Map<Customer, ICustomerDto>(customer);

// Or configure a concrete type
config.CreateMap<Customer, ICustomerDto>()
    .As<CustomerDto>();  // use CustomerDto as the concrete implementation
```

### 8.10 Projection (IQueryable Support)

```csharp
// Project to DTOs at the database level (like AutoMapper ProjectTo)
var dtos = dbContext.Orders
    .ProjectTo<OrderDto>(mapper)
    .Where(d => d.Total > 100)
    .ToListAsync();

// This generates an Expression<Func<Order, OrderDto>> that translates to SQL.
// Only mapped columns are selected - no over-fetching.
```

### 8.11 Multi-Source Mapping (Merge Multiple Objects)

```csharp
// Combine multiple source objects into a single destination
var dto = mapper.Map<OrderDto>(order, customer, shippingAddress);

// Fluent configuration for multi-source
config.CreateMultiMap<OrderDto>()
    .FromSource<Order>(m => m
        .ForMember(d => d.OrderId, o => o.MapFrom(s => s.Id)))
    .FromSource<Customer>(m => m
        .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.FullName)))
    .FromSource<Address>(m => m
        .ForMember(d => d.City, o => o.MapFrom(s => s.City)));
```

### 8.12 Bi-directional Mapping

```csharp
// .ReverseMap() creates the inverse mapping automatically
config.CreateMap<Order, OrderDto>()
    .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Customer.FullName))
    .ReverseMap()
    .ForPath(s => s.Customer.FullName, o => o.MapFrom(d => d.CustomerName));
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
static OrderDto Map(Order source)
{
    var dest = new OrderDto();
    dest.Id = source.Id;
    dest.CustomerName = source.Customer?.Name;
    dest.Total = source.Lines.Sum(l => l.Quantity * l.UnitPrice);
    return dest;
}
```

**Adaptive Promotion:** Mappings start as compiled expressions. After exceeding a configurable invocation threshold (default: 10), they are promoted to IL-emitted delegates on a background thread. The switchover is atomic and lock-free via `Interlocked.CompareExchange`.

### 9.3 Source Generator (Build-Time)

The `LiteMapper.SourceGen` package uses Roslyn incremental generators to emit mapping code at compile time:

```csharp
// User writes:
[MapFrom<Order>]
public partial record OrderDto
{
    public int Id { get; init; }
    public string CustomerName { get; init; }
}

// Source generator emits (in a partial class):
public partial record OrderDto
{
    [GeneratedMapper]
    public static OrderDto MapFromOrder(Order source)
    {
        return new OrderDto
        {
            Id = source.Id,
            CustomerName = source.Customer?.Name ?? string.Empty,
        };
    }
}
```

**Benefits:**
- Zero runtime reflection or compilation overhead.
- Fully AOT-compatible and trimmer-safe.
- Compile-time errors for unmapped required members.
- IntelliSense support in the generated code.

### 9.4 Object Pooling

```csharp
// LiteMapper pools intermediate objects to reduce GC pressure
// ArrayPool<T> for collection buffers
// ObjectPool<ResolutionContext> for mapping context objects
// StringBuilderPool for string concatenation in converters

options.Performance.EnableObjectPooling = true;     // default
options.Performance.PoolMaxRetained = 256;          // per type
```

### 9.5 SIMD-Accelerated Primitive Collection Copy

For collections of primitive types (`int[]`, `double[]`, `byte[]`, etc.), LiteMapper uses `Vector<T>` SIMD operations or `Buffer.BlockCopy` for maximum throughput:

```csharp
// Internally, for int[] -> int[] with same element type:
Buffer.BlockCopy(source, 0, destination, 0, source.Length * sizeof(int));

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
options.Performance.ParallelCollectionThreshold = 1000;
options.Performance.MaxDegreeOfParallelism = Environment.ProcessorCount;
options.Performance.ParallelPartitionSize = 256;  // items per work unit
```

### 9.7 Lazy and Streaming Mapping

```csharp
// Lazy mapping - maps items on demand (IEnumerable)
IEnumerable<OrderDto> dtos = mapper.MapLazy<Order, OrderDto>(orders);

// Streaming mapping - for IAsyncEnumerable
IAsyncEnumerable<OrderDto> dtos = mapper.MapStreamAsync<Order, OrderDto>(ordersAsync);

// Useful for large datasets where materializing all items is expensive
await foreach (var dto in mapper.MapStreamAsync<Order, OrderDto>(GetOrdersAsync()))
{
    await ProcessAsync(dto);
}
```

### 9.8 Caching Strategy

| Cache Level | What | Lifetime | Eviction |
|---|---|---|---|
| **L1: Plan Cache** | `MappingPlan` per `(TSource, TDest)` | App lifetime | Never (immutable config) |
| **L2: Delegate Cache** | Compiled/emitted mapping delegates | App lifetime | Never (promoted only) |
| **L3: Type Metadata Cache** | Reflected member info, constructors | App lifetime | Never |
| **L4: Convention Match Cache** | Name matching results | App lifetime | Never |
| **Hot Path Counter** | Invocation count per mapping pair | App lifetime | Reset on recompilation |

All caches use `ConcurrentDictionary<TKey, TValue>` or `FrozenDictionary<TKey, TValue>` (.NET 8+) for lock-free reads.

### 9.9 Benchmarking Suite

LiteMapper ships with a BenchmarkDotNet project comparing against AutoMapper and Mapster:

```
BenchmarkDotNet Results (example):

|              Method |       Mean |    Error |  StdDev | Ratio |   Gen0 | Allocated |
|-------------------- |-----------:|---------:|--------:|------:|-------:|----------:|
|      LiteMapper_Flat|    62.3 ns |  0.8 ns  | 0.7 ns  |  1.00 | 0.0000 |         - |
|      AutoMapper_Flat|   287.4 ns |  3.2 ns  | 2.8 ns  |  4.61 | 0.0191 |     120 B |
|         Mapster_Flat|   148.7 ns |  1.5 ns  | 1.3 ns  |  2.39 | 0.0095 |      60 B |
|    LiteMapper_Nested|   312.1 ns |  4.1 ns  | 3.6 ns  |  1.00 | 0.0000 |         - |
|    AutoMapper_Nested|  1421.6 ns | 12.3 ns  | 10.9 ns |  4.56 | 0.0572 |     360 B |
|  LiteMapper_1K_List|    89.2 us |  1.1 us  | 0.9 us  |  1.00 | 0.0000 |      48 B |
|  AutoMapper_1K_List|   312.8 us |  3.8 us  | 3.4 us  |  3.51 | 19.531 |  120048 B |
```

---

## 10. Extensibility Model

### 10.1 Plugin Architecture

LiteMapper uses a plugin-based architecture where all features (including built-in ones) are implemented as plugins:

```csharp
public interface ILiteMapperPlugin
{
    string Name { get; }
    int Order { get; }                     // execution order
    void Configure(IPluginContext context);
}

// Register plugins
options.Plugins.Add<MyCustomPlugin>();
options.Plugins.Add<OpenTelemetryPlugin>();
```

### 10.2 Mapping Middleware

Every mapping flows through a middleware pipeline, similar to ASP.NET Core middleware:

```csharp
public interface IMappingMiddleware
{
    Task<object?> InvokeAsync(
        MappingContext context,
        MappingDelegate next);
}

// Example: Logging middleware
public class LoggingMiddleware : IMappingMiddleware
{
    private readonly ILogger _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger) => _logger = logger;

    public async Task<object?> InvokeAsync(MappingContext context, MappingDelegate next)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogDebug("Mapping {Source} -> {Dest}", context.SourceType, context.DestType);

        var result = await next(context);

        sw.Stop();
        _logger.LogDebug("Mapped in {Elapsed}ms", sw.ElapsedMilliseconds);
        return result;
    }
}

// Register globally
options.Pipeline.Use<LoggingMiddleware>();

// Or per-map
config.CreateMap<Order, OrderDto>()
    .UsePipeline(p => p.Use<LoggingMiddleware>());
```

### 10.3 Mapping Interceptors

Interceptors provide a simpler alternative to middleware for common pre/post-map scenarios:

```csharp
public interface IMappingInterceptor<TSource, TDestination>
{
    void BeforeMap(TSource source, TDestination destination, ResolutionContext context);
    void AfterMap(TSource source, TDestination destination, ResolutionContext context);
}

// Example: Audit trail interceptor
public class AuditInterceptor : IMappingInterceptor<object, IAuditable>
{
    public void BeforeMap(object source, IAuditable dest, ResolutionContext ctx) { }
    public void AfterMap(object source, IAuditable dest, ResolutionContext ctx)
    {
        dest.MappedAt = DateTime.UtcNow;
        dest.MappedBy = ctx.GetService<ICurrentUser>()?.Name;
    }
}

// Auto-discovered and applied to all mappings where TDest implements IAuditable
```

### 10.4 Custom Conventions

```csharp
public interface IMemberConvention
{
    int Priority { get; }
    bool TryResolve(MemberInfo destination, TypeModel source, out IValueResolver? resolver);
}

public interface ITypeConvention
{
    bool TryCreateMap(Type source, Type destination, out MappingPlan? plan);
}

// Register
options.Conventions.Add<MyMemberConvention>();
options.Conventions.AddTypeConvention<MyTypeConvention>();
```

### 10.5 Custom Value Resolver Pipeline

```csharp
// Resolvers are tried in order until one succeeds:
// 1. Explicit configuration (.ForMember().MapFrom())
// 2. Attribute-based ([MapFrom])
// 3. Custom conventions (user-registered)
// 4. Built-in conventions (name matching, flattening)
// 5. Type converter (if types differ but conversion exists)
// 6. Default value / null substitute
// 7. Ignore (if no match found and strict mode is off)

// Users can insert resolvers at any priority level
options.Resolvers.InsertBefore<BuiltInNameConvention, MyCustomResolver>();
```

### 10.6 Mapping Events

```csharp
// Global events for cross-cutting concerns
options.Events.OnMappingCreated += (sender, args) =>
{
    Console.WriteLine($"New mapping: {args.SourceType} -> {args.DestType}");
};

options.Events.OnMappingError += (sender, args) =>
{
    Console.WriteLine($"Mapping error: {args.Exception.Message}");
    args.Handled = true;  // suppress exception
    args.Result = default; // use default value
};

options.Events.OnMemberMapped += (sender, args) =>
{
    Console.WriteLine($"  {args.DestMember.Name} = {args.Value}");
};
```

### 10.7 Custom Mapping Strategy Provider

```csharp
// Override how mapping delegates are generated
public interface IMappingStrategyProvider
{
    MappingStrategy Strategy { get; }
    Delegate? TryCreateMapper(MappingPlan plan);
}

// Example: use a third-party expression library
public class FastExpressionStrategy : IMappingStrategyProvider
{
    public MappingStrategy Strategy => MappingStrategy.Custom;

    public Delegate? TryCreateMapper(MappingPlan plan)
    {
        // Build and compile using FastExpressionCompiler
        var expr = BuildExpression(plan);
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
services.AddLiteMapper();

// With options
services.AddLiteMapper(options =>
{
    options.ScanAssembliesContaining<Order, OrderDto>();
    options.AddProfile<OrderMappingProfile>();
    options.CreateMap<Order, OrderDto>();
});

// Register as specific lifetime
services.AddLiteMapper(ServiceLifetime.Singleton);  // default: Singleton
```

### 11.2 Service Registration Details

| Service | Lifetime | Description |
|---|---|---|
| `ILiteMapper` | Singleton | Main mapper interface (thread-safe, immutable config) |
| `IMapper<TSource, TDest>` | Singleton | Strongly-typed mapper for a specific pair |
| `ILiteMapperConfiguration` | Singleton | Read-only access to mapping configuration |
| `IMappingEngine` | Singleton | Low-level mapping engine |
| `IValueResolver<,,>` | Transient | Custom resolvers (DI-resolved per invocation) |
| `IValueConverter<,>` | Transient | Custom converters |
| `IMappingMiddleware` | Scoped | Middleware instances (scoped for request context) |
| `IMappingInterceptor<,>` | Scoped | Interceptor instances |

### 11.3 Injecting the Mapper

```csharp
// Constructor injection (recommended)
public class OrderService
{
    private readonly ILiteMapper _mapper;

    public OrderService(ILiteMapper mapper) => _mapper = mapper;

    public OrderDto GetOrder(int id)
    {
        var order = _repository.GetById(id);
        return _mapper.Map<Order, OrderDto>(order);
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

### 11.4 Resolvers with DI

```csharp
public class TaxCalculatorResolver : IValueResolver<Order, OrderDto, decimal>
{
    private readonly ITaxService _taxService;

    public TaxCalculatorResolver(ITaxService taxService) => _taxService = taxService;

    public decimal Resolve(Order source, OrderDto dest, decimal member, ResolutionContext ctx)
        => _taxService.CalculateTax(source.Total, source.ShippingAddress.State);
}

// Used in mapping - resolver is created via DI
config.CreateMap<Order, OrderDto>()
    .ForMember(d => d.Tax, o => o.MapFrom<TaxCalculatorResolver>());
```

### 11.5 Keyed Services (.NET 8+)

```csharp
// Register multiple mapper configurations
services.AddLiteMapper("admin", options => { /* admin mappings */ });
services.AddLiteMapper("public", options => { /* public mappings */ });

// Inject by key
public class AdminService([FromKeyedServices("admin")] ILiteMapper mapper) { }
```

### 11.6 Health Checks

```csharp
services.AddHealthChecks()
    .AddLiteMapperHealthCheck();  // validates all registered mappings at health check time
```

---

## 12. Diagnostics and Debugging

### 12.1 Configuration Validation

```csharp
// Validate all mappings at startup (fail-fast)
services.AddLiteMapper(options =>
{
    options.ValidateOnStartup = true;         // default: true in Development
    options.StrictMode = true;                // all destination members must be mapped
    options.ThrowOnUnmappedMembers = true;    // throw if any dest member has no source
});

// Validate programmatically
var config = serviceProvider.GetRequiredService<ILiteMapperConfiguration>();
var result = config.Validate();

foreach (var error in result.Errors)
{
    Console.WriteLine($"{error.SourceType} -> {error.DestType}: {error.Message}");
}
```

### 12.2 Mapping Explanation

```csharp
// Ask "why" for any mapping decision
var explanation = mapper.Explain<Order, OrderDto>();

Console.WriteLine(explanation.ToString());
// Output:
// Order -> OrderDto (Strategy: ILEmit, 8 bindings)
//   Id -> Id (ExactNameMatch)
//   Customer.FirstName + Customer.LastName -> CustomerFullName (CustomResolver: FullNameResolver)
//   Lines.Sum(Quantity * UnitPrice) -> Total (ExplicitConfiguration)
//   InternalCode -> [IGNORED]
//   Status -> Status (ExactNameMatch, EnumByName)
//   CreatedAt -> CreatedAt (ExactNameMatch, DateTimeToDateTimeOffset)
//   ShippingAddress -> ShippingAddress (NestedMapping: Address -> AddressDto)
//   Customer -> Customer (NestedMapping: Customer -> CustomerDto)
```

### 12.3 Mapping Graph Visualizer

```csharp
// Generate a DOT graph of all registered mappings
var graph = mapper.GetMappingGraph();
var dot = graph.ToDotFormat();
File.WriteAllText("mapping-graph.dot", dot);

// Or serve as an endpoint in ASP.NET Core
app.MapLiteMapperDiagnostics("/diagnostics/mappings");
// Provides:
//   GET /diagnostics/mappings         -> JSON list of all mappings
//   GET /diagnostics/mappings/graph   -> DOT/SVG graph
//   GET /diagnostics/mappings/{type}  -> details for a specific type
```

### 12.4 OpenTelemetry Integration

```csharp
// Add LiteMapper instrumentation
services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddLiteMapperInstrumentation())
    .WithMetrics(builder => builder.AddLiteMapperInstrumentation());

// Metrics emitted:
// litemapper.mappings.total          - counter of total mappings performed
// litemapper.mappings.duration       - histogram of mapping durations
// litemapper.mappings.errors         - counter of mapping errors
// litemapper.collections.parallel    - counter of parallel collection mappings
// litemapper.cache.hits              - counter of cache hits
// litemapper.cache.promotions        - counter of IL emit promotions

// Traces emitted:
// Each mapping creates a span with:
//   litemapper.source_type, litemapper.dest_type,
//   litemapper.strategy, litemapper.member_count
```

### 12.5 Logging

```csharp
// LiteMapper uses ILogger<LiteMapper> for structured logging
// Log levels:
//   Debug   - individual member mappings, cache hits
//   Info    - mapping plan creation, IL promotion
//   Warning - unmapped members (non-strict mode), structural similarity matches
//   Error   - mapping failures, invalid configuration

options.Logging.MinimumLevel = LogLevel.Warning;  // default
options.Logging.LogMappingPlans = true;            // log full plan on creation
```

### 12.6 Debug View

```csharp
// In the debugger, MappingPlan has a DebugView property
// that shows a human-readable representation of the mapping:

[DebuggerDisplay("{DebugView}")]
public sealed record MappingPlan
{
    internal string DebugView => $"{SourceType.Name} -> {DestType.Name} [{Strategy}] ({Bindings.Count} members)";
}
```

---

## 13. Thread Safety and Concurrency

### 13.1 Thread Safety Guarantees

| Component | Thread Safe | Notes |
|---|---|---|
| `ILiteMapper` | Yes | Immutable after build. All methods are safe for concurrent use. |
| `MappingPlan` | Yes | Immutable record. |
| `MemberBinding` | Yes | Immutable record. |
| `ResolutionContext` | No | Created per-mapping, not shared across threads. |
| Cache stores | Yes | `ConcurrentDictionary` / `FrozenDictionary`. |
| IL Emit promotion | Yes | Atomic via `Interlocked.CompareExchange`. |
| Parallel collections | Yes | Each thread gets its own `ResolutionContext`. |

### 13.2 Immutable Configuration Pattern

```csharp
// Configuration is built once and frozen
var builder = new LiteMapperBuilder();
builder.AddProfile<OrderProfile>();
builder.CreateMap<Customer, CustomerDto>();

// Build() freezes the configuration - no further modifications allowed
ILiteMapper mapper = builder.Build();

// Attempting to modify after Build() throws InvalidOperationException
```

### 13.3 Concurrent Collection Mapping

```csharp
// When parallel mapping is enabled, LiteMapper ensures:
// 1. Each parallel task gets its own ResolutionContext (no sharing)
// 2. Circular reference tracking is per-context (thread-local)
// 3. Results array is pre-allocated (no concurrent writes to shared list)
// 4. Exceptions are aggregated and re-thrown as AggregateException

// The user can safely call mapper.Map from multiple threads simultaneously
Parallel.ForEach(orders, order =>
{
    var dto = mapper.Map<Order, OrderDto>(order);  // safe
    Process(dto);
});
```

---

## 14. Public API Surface

### 14.1 Core Interfaces

```csharp
// Primary mapper interface
public interface ILiteMapper
{
    TDestination Map<TSource, TDestination>(TSource source);
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    object Map(object source, Type sourceType, Type destinationType);
    object Map(object source, object destination, Type sourceType, Type destinationType);

    // Collection mapping
    IReadOnlyList<TDestination> MapList<TSource, TDestination>(IEnumerable<TSource> source);
    TDestination[] MapArray<TSource, TDestination>(IEnumerable<TSource> source);

    // Lazy / streaming
    IEnumerable<TDestination> MapLazy<TSource, TDestination>(IEnumerable<TSource> source);
    IAsyncEnumerable<TDestination> MapStreamAsync<TSource, TDestination>(
        IAsyncEnumerable<TSource> source, CancellationToken ct = default);

    // Multi-source
    TDestination Map<TDestination>(params object[] sources);

    // Projection
    IQueryable<TDestination> ProjectTo<TDestination>(IQueryable source);
    Expression<Func<TSource, TDestination>> GetProjection<TSource, TDestination>();

    // Diagnostics
    MappingExplanation Explain<TSource, TDestination>();
    MappingGraph GetMappingGraph();
}

// Strongly-typed mapper for a specific pair (faster, no dictionary lookup)
public interface IMapper<TSource, TDestination>
{
    TDestination Map(TSource source);
    TDestination Map(TSource source, TDestination destination);
    IReadOnlyList<TDestination> MapList(IEnumerable<TSource> source);
}
```

### 14.2 Configuration Interfaces

```csharp
public interface ILiteMapperConfiguration
{
    IReadOnlyDictionary<TypePair, MappingPlan> GetAllMappingPlans();
    MappingPlan? GetMappingPlan<TSource, TDestination>();
    MappingPlan? GetMappingPlan(Type sourceType, Type destinationType);
    ValidationResult Validate();
    bool HasMapping<TSource, TDestination>();
    bool HasMapping(Type sourceType, Type destinationType);
}
```

### 14.3 Builder API

```csharp
public interface ILiteMapperBuilder
{
    IMapExpression<TSource, TDestination> CreateMap<TSource, TDestination>();
    IMultiMapExpression<TDestination> CreateMultiMap<TDestination>();
    ILiteMapperBuilder AddProfile<TProfile>() where TProfile : LiteMapperProfile;
    ILiteMapperBuilder AddProfile(LiteMapperProfile profile);
    ILiteMapperBuilder AddConverter<TConverter>() where TConverter : class;
    ILiteMapperBuilder ScanAssemblies(params Assembly[] assemblies);
    ILiteMapperBuilder ScanAssembliesContaining<T>();
    ILiteMapper Build();
}
```

### 14.4 Attributes

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MapFromAttribute<TSource> : Attribute { }

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MapToAttribute<TDestination> : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MapFromAttribute : Attribute
{
    public MapFromAttribute(string sourceMemberName);
    public string? Transform { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MapToAttribute : Attribute
{
    public MapToAttribute(string destinationMemberName);
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IgnoreMapAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MapWithConverterAttribute<TConverter> : Attribute { }

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MapWithResolverAttribute<TResolver> : Attribute { }

[AttributeUsage(AttributeTargets.Enum)]
public sealed class MapToEnumAttribute : Attribute
{
    public MapToEnumAttribute(object targetValue);
}
```

### 14.5 Extension Methods

```csharp
// IServiceCollection extensions
public static class LiteMapperServiceCollectionExtensions
{
    public static IServiceCollection AddLiteMapper(this IServiceCollection services);
    public static IServiceCollection AddLiteMapper(this IServiceCollection services,
        Action<LiteMapperOptions> configure);
    public static IServiceCollection AddLiteMapper(this IServiceCollection services,
        ServiceLifetime lifetime);
    public static IServiceCollection AddLiteMapper(this IServiceCollection services,
        string key, Action<LiteMapperOptions> configure);
}

// IQueryable extensions
public static class LiteMapperQueryableExtensions
{
    public static IQueryable<TDest> ProjectTo<TDest>(this IQueryable source, ILiteMapper mapper);
}

// Object extensions (opt-in via using LiteMapper.Extensions)
public static class LiteMapperObjectExtensions
{
    public static TDest MapTo<TDest>(this object source);
    public static TDest MapTo<TDest>(this object source, ILiteMapper mapper);
}

// Health check extensions
public static class LiteMapperHealthCheckExtensions
{
    public static IHealthChecksBuilder AddLiteMapperHealthCheck(this IHealthChecksBuilder builder);
}

// ASP.NET Core extensions
public static class LiteMapperEndpointExtensions
{
    public static IEndpointRouteBuilder MapLiteMapperDiagnostics(
        this IEndpointRouteBuilder endpoints, string prefix = "/diagnostics/mappings");
}
```

---

## 15. Comparison with AutoMapper

### 15.1 Feature Comparison Matrix

| Feature | AutoMapper | LiteMapper | Notes |
|---|---|---|---|
| Zero-config mapping | No | **Yes** | LiteMapper auto-discovers type pairs |
| Fluent API | Yes | **Yes** | LiteMapper's API is a superset |
| Attribute-based config | No | **Yes** | `[MapFrom<T>]`, `[MapTo<T>]`, `[IgnoreMap]` |
| Profile-based config | Yes | **Yes** | Compatible pattern |
| Inline config (no profile) | No | **Yes** | Configure directly in DI registration |
| Source generators | No | **Yes** | AOT-safe, zero-reflection mapping |
| IL Emit | No | **Yes** | Adaptive hot-path promotion |
| Parallel collections | No | **Yes** | Auto-parallel above threshold |
| SIMD collection copy | No | **Yes** | Primitive array fast-path |
| Streaming (IAsyncEnumerable) | No | **Yes** | `MapStreamAsync` |
| Multi-source mapping | No | **Yes** | Merge N sources into 1 destination |
| Flattening | Yes | **Yes** | Both support automatic flattening |
| Unflattening | Partial | **Yes** | LiteMapper auto-unflattens bidirectionally |
| Inheritance/polymorphism | Manual config | **Auto** | Auto-detected from type hierarchy |
| Discriminator mapping | No | **Yes** | `DiscriminateBy()` |
| Dictionary-to-object | No | **Yes** | Bidirectional |
| Tuple mapping | No | **Yes** | Named ValueTuple support |
| Record/init support | Partial | **Full** | Primary ctors, init-only, required members |
| Interface proxy mapping | No | **Yes** | Runtime DispatchProxy generation |
| Collection merging | No | **Yes** | Update-in-place with match/add/remove |
| ProjectTo (IQueryable) | Yes | **Yes** | Compatible API |
| Reverse mapping | Yes | **Yes** | `.ReverseMap()` |
| Conditional mapping | Yes | **Yes** | Pre-condition + condition |
| Custom resolvers (DI) | Yes | **Yes** | Compatible pattern |
| Custom converters | Yes | **Yes** | Compatible pattern |
| Middleware pipeline | No | **Yes** | ASP.NET Core-style middleware |
| Interceptors | No | **Yes** | Typed before/after hooks |
| Plugin system | No | **Yes** | Modular architecture |
| Events | No | **Yes** | OnMappingCreated, OnError, OnMemberMapped |
| Mapping explanation | No | **Yes** | `.Explain<S,D>()` traces every decision |
| Graph visualizer | No | **Yes** | DOT/SVG output, diagnostic endpoint |
| OpenTelemetry | No | **Yes** | Metrics + traces |
| Health checks | No | **Yes** | ASP.NET Core health check |
| Keyed DI services | No | **Yes** | .NET 8 keyed services |
| Strict mode | Partial | **Yes** | All-or-nothing validation |
| Configuration validation | `AssertConfigurationIsValid` | **Yes** | Richer error messages, startup validation |
| Lazy mapping | No | **Yes** | Deferred `IEnumerable` mapping |
| Object pooling | No | **Yes** | Reduces GC pressure |
| Snake/camel/pascal conv. | No | **Yes** | Built-in naming convention converters |
| Abbreviation aliases | No | **Yes** | `Addr` -> `Address` dictionary |

### 15.2 Code Comparison

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

#### LiteMapper: Same Mapping

```csharp
// 1. Register in DI (auto-discovers all mappings)
services.AddLiteMapper();

// 2. Use
var dto = mapper.Map<Order, OrderDto>(order);
```

**Lines of code: 2** (Customer, Address, OrderLine mappings are auto-discovered)

If `CustomerName` needs explicit config:

```csharp
services.AddLiteMapper(o => o.CreateMap<Order, OrderDto>(m =>
    m.ForMember(d => d.CustomerName, x => x.MapFrom(s => s.Customer.FullName))));
```

**Lines of code: 3**

### 15.3 Migration Guide from AutoMapper

| AutoMapper | LiteMapper Equivalent |
|---|---|
| `services.AddAutoMapper(assemblies)` | `services.AddLiteMapper()` |
| `Profile` class | `LiteMapperProfile` class (or remove entirely) |
| `CreateMap<S, D>()` | Auto-discovered, or `options.CreateMap<S, D>()` |
| `ForMember(d => ..., o => o.MapFrom(...))` | Same API: `.ForMember(d => ..., o => o.MapFrom(...))` |
| `ForMember(d => ..., o => o.Ignore())` | Same, or use `[IgnoreMap]` attribute |
| `IValueResolver<S,D,M>` | Same interface: `IValueResolver<S,D,M>` |
| `ITypeConverter<S,D>` | `IValueConverter<S,D>` |
| `mapper.Map<D>(source)` | `mapper.Map<S, D>(source)` |
| `AssertConfigurationIsValid()` | `options.ValidateOnStartup = true` |
| `ProjectTo<D>(config)` | `source.ProjectTo<D>(mapper)` |
| `.ReverseMap()` | `.ReverseMap()` (identical) |
| `.Include<DS, DD>()` | Auto-discovered, or `.IncludeDerived<DS, DD>()` |

---

## 16. Working Samples

### 16.1 Sample: Minimal API with LiteMapper

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLiteMapper();

var app = builder.Build();

app.MapGet("/orders/{id}", async (int id, ILiteMapper mapper, AppDbContext db) =>
{
    var order = await db.Orders
        .Include(o => o.Customer)
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.Id == id);

    return order is null
        ? Results.NotFound()
        : Results.Ok(mapper.Map<Order, OrderDto>(order));
});

app.MapGet("/orders", async (ILiteMapper mapper, AppDbContext db) =>
{
    var dtos = await db.Orders.ProjectTo<OrderListDto>(mapper).ToListAsync();
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

// DTOs - LiteMapper maps ALL of these automatically
public record OrderDto
{
    public int Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }     // DateTime -> DateTimeOffset auto-converted
    public string Status { get; init; }                 // Enum -> string auto-converted
    public string CustomerFullName { get; init; }       // Flattened from Customer.GetFullName()
    public string CustomerEmail { get; init; }          // Flattened from Customer.Email
    public string ShippingAddressCity { get; init; }    // Flattened from ShippingAddress.City
    public decimal Total { get; init; }                 // Matched to GetTotal() method
    public List<OrderLineDto> Lines { get; init; }      // Collection auto-mapped
}

public record OrderLineDto(int LineId, string ProductName, int Quantity, decimal UnitPrice);

// Usage - no configuration at all
services.AddLiteMapper();

var dto = mapper.Map<Order, OrderDto>(order);
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
List<VehicleDto> dtos = mapper.MapList<Vehicle, VehicleDto>(vehicles);
// dtos contains [CarDto, TruckDto, ElectricCarDto] with correct types
```

### 16.4 Sample: Middleware and Interceptors

```csharp
// Caching middleware - cache mapped DTOs for repeated lookups
public class CachingMiddleware : IMappingMiddleware
{
    private readonly IMemoryCache _cache;

    public CachingMiddleware(IMemoryCache cache) => _cache = cache;

    public async Task<object?> InvokeAsync(MappingContext ctx, MappingDelegate next)
    {
        if (ctx.Source is IHasId entity)
        {
            var key = $"map:{ctx.SourceType.Name}:{ctx.DestType.Name}:{entity.Id}";
            return await _cache.GetOrCreateAsync(key, async _ => await next(ctx));
        }
        return await next(ctx);
    }
}

// Validation interceptor
public class ValidationInterceptor : IMappingInterceptor<object, IValidatable>
{
    public void BeforeMap(object src, IValidatable dest, ResolutionContext ctx) { }
    public void AfterMap(object src, IValidatable dest, ResolutionContext ctx)
    {
        var errors = dest.Validate();
        if (errors.Any())
            throw new MappingValidationException(errors);
    }
}

services.AddLiteMapper(o =>
{
    o.Pipeline.Use<CachingMiddleware>();
    // ValidationInterceptor is auto-discovered
});
```

### 16.5 Sample: Streaming Large Datasets

```csharp
// Stream map from database cursor to API response
app.MapGet("/reports/orders", async (ILiteMapper mapper, AppDbContext db, HttpContext http) =>
{
    http.Response.ContentType = "application/json";
    
    var orders = db.Orders.AsAsyncEnumerable();
    var dtos = mapper.MapStreamAsync<Order, OrderReportDto>(orders);

    await JsonSerializer.SerializeAsync(http.Response.Body,
        dtos, cancellationToken: http.RequestAborted);
});
```

### 16.6 Sample: Source Generator (AOT-Safe)

```csharp
// Decorate with attribute - source generator emits mapping code
[MapFrom<Order>]
public partial record OrderDto
{
    public int Id { get; init; }
    public string CustomerFullName { get; init; }

    [IgnoreMap]
    public string CacheKey { get; init; }
}

// Generated code (visible in IDE, debuggable):
// public partial record OrderDto
// {
//     public static OrderDto MapFromOrder(Order source) => new()
//     {
//         Id = source.Id,
//         CustomerFullName = source.Customer?.GetFullName() ?? string.Empty,
//     };
// }

// The generated mapper is automatically registered and used by ILiteMapper
var dto = mapper.Map<Order, OrderDto>(order);  // uses generated code, zero reflection
```

### 16.7 Sample: Multi-Source Mapping

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

services.AddLiteMapper(o =>
{
    o.CreateMultiMap<DashboardViewModel>()
        .FromSource<User>(m => m
            .ForMember(d => d.UserName, x => x.MapFrom(s => s.FullName)))
        .FromSource<OrderSummary>(m => m
            .ForMember(d => d.OrderCount, x => x.MapFrom(s => s.Count))
            .ForMember(d => d.TotalRevenue, x => x.MapFrom(s => s.Revenue)))
        .FromSource<List<Notification>>(m => m
            .ForMember(d => d.RecentNotifications, x => x.MapFrom(s => s)))
        .FromSource<CompanyInfo>(m => m
            .ForMember(d => d.CompanyName, x => x.MapFrom(s => s.Name)));
});

var vm = mapper.Map<DashboardViewModel>(user, orderSummary, notifications, companyInfo);
```

### 16.8 Sample: Collection Merging (EF Core Update Pattern)

```csharp
public class OrderUpdateService
{
    private readonly ILiteMapper _mapper;
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
        _mapper.Map(dto, order);

        await _db.SaveChangesAsync();
    }
}

services.AddLiteMapper(o =>
{
    o.CreateMap<OrderUpdateDto, Order>(m => m
        .ForMember(d => d.Lines, x => x.MergeCollection(
            matchBy: (src, dst) => src.LineId == dst.LineId)));
});
```

---

## 17. Test Plan

### 17.1 Test Categories

| Category | Description | Approx. Count |
|---|---|---|
| **Unit: Convention Engine** | Name matching, flattening, unflattening, case conversion | 80+ |
| **Unit: Type Converters** | All built-in type converters | 60+ |
| **Unit: Collection Mapping** | All collection type combinations | 40+ |
| **Unit: Inheritance** | Polymorphic, discriminator, deep hierarchies | 30+ |
| **Unit: Construction** | All construction strategies | 20+ |
| **Unit: Fluent API** | All fluent configuration options | 50+ |
| **Unit: Attributes** | All attribute-based configurations | 25+ |
| **Integration: DI** | Service registration, resolution, keyed services | 20+ |
| **Integration: EF Core** | ProjectTo, collection merge, SaveChanges patterns | 15+ |
| **Integration: ASP.NET Core** | Middleware, endpoints, health checks | 10+ |
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
    var mapper = new LiteMapperBuilder()
        .ScanAssembliesContaining<Order>()
        .Build();

    var order = CreateSampleOrder();
    var dto = mapper.Map<Order, OrderDto>(order);

    Assert.Equal(order.Id, dto.Id);
    Assert.Equal(order.Customer.Email, dto.CustomerEmail);
    Assert.Equal(order.Customer.GetFullName(), dto.CustomerFullName);
}

[Fact]
public void Should_auto_flatten_nested_properties()
{
    var mapper = new LiteMapperBuilder().Build();
    var order = CreateSampleOrder();

    var flat = mapper.Map<Order, OrderFlatDto>(order);

    Assert.Equal(order.Customer.FirstName, flat.CustomerFirstName);
    Assert.Equal(order.Customer.Address.City, flat.CustomerAddressCity);
}

[Fact]
public void Should_auto_unflatten_to_nested_properties()
{
    var mapper = new LiteMapperBuilder().Build();
    var flat = new OrderFlatDto
    {
        CustomerFirstName = "Alice",
        CustomerAddressCity = "Seattle"
    };

    var order = mapper.Map<OrderFlatDto, Order>(flat);

    Assert.Equal("Alice", order.Customer.FirstName);
    Assert.Equal("Seattle", order.Customer.Address.City);
}
```

#### 17.2.2 Polymorphic Mapping Tests

```csharp
[Fact]
public void Should_map_polymorphic_types_without_configuration()
{
    var mapper = new LiteMapperBuilder()
        .ScanAssembliesContaining<Shape>()
        .Build();

    Shape shape = new Circle { X = 1, Y = 2, Radius = 5 };
    var dto = mapper.Map<Shape, ShapeDto>(shape);

    Assert.IsType<CircleDto>(dto);
    Assert.Equal(5, ((CircleDto)dto).Radius);
}

[Fact]
public void Should_map_collection_of_polymorphic_types()
{
    var mapper = new LiteMapperBuilder().Build();
    List<Shape> shapes = [new Circle(), new Rectangle(), new Circle()];

    var dtos = mapper.MapList<Shape, ShapeDto>(shapes);

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
    var mapper = new LiteMapperBuilder().Build();
    var emp = new Employee { Name = "Alice" };
    var mgr = new Employee { Name = "Bob" };
    emp.Manager = mgr;
    mgr.DirectReports = new List<Employee> { emp };

    var dto = mapper.Map<Employee, EmployeeDto>(emp);

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
    var mapper = new LiteMapperBuilder()
        .Configure(o => o.Performance.ParallelCollectionThreshold = 100)
        .Build();

    var orders = Enumerable.Range(0, 10_000)
        .Select(i => new Order { Id = i })
        .ToList();

    var dtos = mapper.MapList<Order, OrderDto>(orders);

    Assert.Equal(10_000, dtos.Count);
    Assert.All(dtos, (dto, i) => Assert.Equal(i, dto.Id));
}
```

#### 17.2.5 Thread Safety Tests

```csharp
[Fact]
public void Should_be_thread_safe_for_concurrent_mapping()
{
    var mapper = new LiteMapperBuilder().Build();
    var exceptions = new ConcurrentBag<Exception>();

    Parallel.For(0, 10_000, i =>
    {
        try
        {
            var order = new Order { Id = i, Customer = new Customer { FirstName = $"User{i}" } };
            var dto = mapper.Map<Order, OrderDto>(order);
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
    private readonly ILiteMapper _liteMapper;
    private readonly IMapper _autoMapper;
    private readonly Order _order;

    [GlobalSetup]
    public void Setup()
    {
        _liteMapper = new LiteMapperBuilder().Build();
        _autoMapper = new MapperConfiguration(c => c.CreateMap<Order, OrderDto>()).CreateMapper();
        _order = CreateSampleOrder();

        // Warm up
        _liteMapper.Map<Order, OrderDto>(_order);
        _autoMapper.Map<OrderDto>(_order);
    }

    [Benchmark(Baseline = true)]
    public OrderDto LiteMapper() => _liteMapper.Map<Order, OrderDto>(_order);

    [Benchmark]
    public OrderDto AutoMapper() => _autoMapper.Map<OrderDto>(_order);
}
```

### 17.3 Test Infrastructure

```
tests/
  LiteMapper.Tests.Unit/
    Conventions/
    Converters/
    Collections/
    Inheritance/
    Construction/
    FluentApi/
    Attributes/
    EdgeCases/
  LiteMapper.Tests.Integration/
    DependencyInjection/
    EntityFrameworkCore/
    AspNetCore/
    OpenTelemetry/
  LiteMapper.Tests.Performance/
    Benchmarks/
    MemoryTests/
    ParallelTests/
  LiteMapper.Tests.SourceGen/
    GeneratedCodeTests/
    AotTests/
```

---

## 18. Package and Distribution

### 18.1 NuGet Packages

| Package | Dependencies | Size Target |
|---|---|---|
| `LiteMapper` | None (zero dependencies) | < 150 KB |
| `LiteMapper.SourceGen` | Microsoft.CodeAnalysis (analyzer) | < 100 KB |
| `LiteMapper.DependencyInjection` | LiteMapper, Microsoft.Extensions.DependencyInjection.Abstractions | < 30 KB |
| `LiteMapper.AspNetCore` | LiteMapper.DependencyInjection, Microsoft.AspNetCore.* | < 50 KB |
| `LiteMapper.Diagnostics` | LiteMapper, OpenTelemetry.Api | < 50 KB |
| `LiteMapper.FluentValidation` | LiteMapper, FluentValidation | < 30 KB |

### 18.2 Supported Target Frameworks

| Framework | Support Level |
|---|---|
| .NET 9 | Full (all features) |
| .NET 8 | Full (all features including FrozenDictionary, keyed services) |
| .NET Standard 2.1 | Core features (no source gen, no SIMD, no keyed services) |

### 18.3 Repository Structure

```
litemapper/
  src/
    LiteMapper/
      Conventions/           # Naming conventions, type pairing
      Converters/            # Built-in type converters
      Engine/                # IL Emit, expression compilation, mapping execution
      Configuration/         # Fluent API, profiles, options
      Discovery/             # Assembly scanning, type analysis
      Collections/           # Collection mapping, parallel mapping
      Pipeline/              # Middleware, interceptors
      Caching/               # Multi-level cache
      Diagnostics/           # Explanation, graph, debug views
      Extensions/            # Object extensions, LINQ extensions
    LiteMapper.SourceGen/
      Analyzers/             # Roslyn analyzers for compile-time checks
      Generators/            # Incremental source generators
    LiteMapper.DependencyInjection/
    LiteMapper.AspNetCore/
    LiteMapper.Diagnostics/
    LiteMapper.FluentValidation/
  tests/
    LiteMapper.Tests.Unit/
    LiteMapper.Tests.Integration/
    LiteMapper.Tests.Performance/
    LiteMapper.Tests.SourceGen/
  samples/
    LiteMapper.Samples.MinimalApi/
    LiteMapper.Samples.WebApp/
    LiteMapper.Samples.Console/
    LiteMapper.Samples.Blazor/
  benchmarks/
    LiteMapper.Benchmarks/
  docs/
    requirements/
    api/
    guides/
  README.md
  LICENSE
  Directory.Build.props
  Directory.Packages.props
  LiteMapper.sln
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

- [x] Convention engine (name matching, flattening, unflattening)
- [x] Expression compilation engine
- [x] Fluent configuration API
- [x] Profile-based configuration
- [x] Attribute-based configuration
- [x] All built-in type converters
- [x] Collection mapping (all standard types)
- [x] Inheritance and polymorphic mapping
- [x] Circular reference handling
- [x] Record and init-only support
- [x] Nested object mapping
- [x] DI integration (`AddLiteMapper`)
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

- [ ] Source generator (`LiteMapper.SourceGen`)
- [ ] Middleware pipeline
- [ ] Interceptors
- [ ] Multi-source mapping
- [ ] Collection merging
- [ ] Streaming mapping (IAsyncEnumerable)
- [ ] Lazy mapping (deferred IEnumerable)

### Phase 4: Ecosystem (v1.3)

- [ ] ASP.NET Core integration package
- [ ] OpenTelemetry instrumentation
- [ ] Mapping graph visualizer
- [ ] Health checks
- [ ] FluentValidation integration
- [ ] Diagnostic endpoints

### Phase 5: Intelligence (v2.0)

- [ ] `ExpandoObject` / `dynamic` mapping
- [ ] Dictionary-to-object deep mapping
- [ ] Tuple mapping enhancements
- [ ] Custom expression tree visitors for ProjectTo
- [ ] Roslyn analyzer warnings for common mistakes
- [ ] Mapping code fix providers (IDE quick-fixes)
- [ ] Performance profiler integration (dotTrace, PerfView)

---

## 20. Glossary

| Term | Definition |
|---|---|
| **Mapping Plan** | Immutable instruction set describing how every member of a destination type is populated from a source type. |
| **Member Binding** | A single instruction within a MappingPlan that describes how one destination member gets its value. |
| **Convention** | A rule that automatically matches source members to destination members (e.g., name matching, flattening). |
| **Value Resolver** | A component that extracts a value from the source object for a specific destination member. |
| **Value Converter** | A component that converts a value from one type to another (e.g., string to int). |
| **Mapping Strategy** | The code generation approach used for a mapping (IL Emit, Source Generated, Expression Compiled, Interpreted). |
| **Adaptive Promotion** | The process of automatically upgrading a mapping from Expression Compiled to IL Emit after a usage threshold is reached. |
| **Flattening** | Mapping nested source properties to flat destination properties (e.g., `Customer.Address.City` to `CustomerAddressCity`). |
| **Unflattening** | The reverse of flattening: mapping flat source properties to nested destination properties. |
| **Type Pair** | A `(SourceType, DestinationType)` tuple that uniquely identifies a mapping configuration. |
| **Resolution Context** | Per-mapping context object carrying state like depth, visited objects, and service provider. |
| **Mapping Middleware** | A pipeline component that wraps the mapping execution, similar to ASP.NET Core middleware. |
| **Interceptor** | A typed before/after hook that runs for specific source/destination type combinations. |
| **Projection** | Converting a mapping plan into an `Expression<Func<S,D>>` for IQueryable translation (e.g., EF Core SQL generation). |
| **Plugin** | A modular extension that can add conventions, converters, middleware, or other features to LiteMapper. |
| **Hot Path** | A mapping that is invoked frequently and is eligible for IL Emit promotion. |
| **Cold Path** | A mapping that is invoked infrequently and uses the Expression Compiled strategy. |
| **Source Generator** | A Roslyn incremental generator that emits mapping code at compile time, eliminating runtime reflection. |
| **Structural Similarity** | A scoring algorithm that determines how well two types match based on their member names and types. |

---

*End of Specification*

*LiteMapper: Less code. More features. Better performance.*
