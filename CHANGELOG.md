# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added — Sprint 3: Built-in Type Transformers

- **`TypeTransformerRegistry`** — central transformer lookup and storage with exact-match dictionary + open transformer `CanTransform` scan with cached results. Thread-safe via `ConcurrentDictionary`. Supports `Register<TOrigin, TTarget>`, `RegisterOpen`, `GetTransformer`, `HasTransformer`, `GetRegisteredPairs`, `Clear`, and `ClearOpen`.
- **`TypeTransformerRegistryDefaults.RegisterDefaults()`** — bulk-registers all 20+ built-in transformers (15 exact-match + 10 open). Idempotent on re-call.
- **`ParsableTransformer`** — open transformer: `string` → any `IParsable<T>` (.NET 7+) or `TypeConverter`-compatible type (`netstandard2.1` fallback). Cached parse delegates per target type.
- **`ToStringTransformer`** — open transformer: any `T` → `string` via `ToString()` with `IFormattable` culture-aware formatting. Lowest-priority open transformer.
- **`DateTimeToDateTimeOffsetTransformer`** — respects `DateTimeKind` (Utc/Local/Unspecified).
- **`DateTimeOffsetToDateTimeTransformer`** — extracts `UtcDateTime`.
- **`DateTimeToDateOnlyTransformer`** / **`DateTimeToTimeOnlyTransformer`** / **`DateOnlyToDateTimeTransformer`** / **`TimeOnlyToTimeSpanTransformer`** — `#if NET6_0_OR_GREATER` guarded.
- **`StringToDateTimeTransformer`** — `DateTime.TryParse` with `InvariantCulture`; exact-match precedence over `ParsableTransformer`.
- **`EnumToStringTransformer`** — open: `Enum` → `string` with optional `[Description]` attribute support. Handles `[Flags]`.
- **`StringToEnumTransformer`** — open: `string` → `Enum` with case-insensitive parse and fallback value support.
- **`EnumToEnumTransformer`** — open: `Enum` → `Enum` by name (default) or by value. Supports `[Flags]` split-by-comma. Cached converters per `TypePair`.
- **`EnumTransformerOptions`** — `Strategy`, `CaseInsensitive`, `UseDescriptionAttribute`, `FallbackValue<T>()`.
- **`EnumMappingStrategy`** — `ByName`, `ByValue`, `ByAttribute`.
- **`GuidToStringTransformer`** / **`StringToGuidTransformer`** — `Guid` ↔ `string` ("D" format).
- **`StringToUriTransformer`** / **`UriToStringTransformer`** — `string` ↔ `Uri` (`UriKind.RelativeOrAbsolute`, `OriginalString`).
- **`BoolToIntTransformer`** / **`IntToBoolTransformer`** — `bool` ↔ `int`.
- **`NullableWrapTransformer`** / **`NullableUnwrapTransformer`** — open: `T` ↔ `Nullable<T>` wrap/unwrap.
- **`ImplicitExplicitOperatorTransformer`** — open: detects `op_Implicit`/`op_Explicit` operators via reflection, compiles to `Func<object, object>` via expression trees. Prefers implicit. `AllowExplicit` configuration.
- **`ByteArrayToBase64Transformer`** / **`Base64ToByteArrayTransformer`** — `byte[]` ↔ `string` (Base64).
- **`JsonElementToObjectTransformer`** / **`ObjectToJsonElementTransformer`** — open: `JsonElement` ↔ `T` via `System.Text.Json`.
- **`StringTransformationOptions`** — `TrimAll`, `NullToEmpty`, `Apply(Func<string, string>)`, `Process(string?)`.
- **`StringPostProcessor`** — `ITypeTransformer<string, string>` post-processing transformer applying `StringTransformationOptions`.
- **`SmartMappException`** — base exception for all SmartMapp.Net library exceptions. Inherits `InvalidOperationException`.
- **`TransformationException`** — custom exception inheriting `SmartMappException` with `OriginValue`, `OriginType`, `TargetType` diagnostic properties.

### Test Coverage

- 150+ new transformer tests (378 total across project).
- Test fixtures in `TestTypes/TransformerTestTypes.cs` covering: `Money` (implicit/explicit operators), `Temperature` (implicit preference), `Percentage` (target-type operator), `NoOperatorType`, `OrderStatus`/`OrderStatusDto`/`PaymentStatus` enums, `FilePermissions`/`FilePermissionsDto` `[Flags]` enums, `JsonTestDto`, `TransformerPropertySource`/`TransformerPropertyTarget` (Sprint 4 reuse).
- Comprehensive §7.1 golden test verifying all 26 type pair lookups resolve to correct transformer types.
- Thread-safety test with `Parallel.For` concurrent lookups.
- `RegisterDefaults` idempotency test.

---

### Added — Sprint 2: Convention Engine

- **`IPropertyConvention`** interface — contract for property-level conventions with `Priority` and `TryLink`.
- **`PropertyAccessProvider`** — compiled `IValueProvider` for reading a single property/field from origin via `Expression.MakeMemberAccess`.
- **`ExactNameConvention`** (Priority 100) — case-insensitive exact name matching; prefers exact-case and properties over fields.
- **`CaseConvention`** (Priority 200) — cross-case matching (snake_case, camelCase, PascalCase, SCREAMING_SNAKE, kebab-case) via `NameNormalizer`.
- **`NameNormalizer`** — static utility to segment, normalize, and compare member names across casing conventions. Includes `ReadOnlySpan<char>` optimization on .NET 8+.
- **`PrefixDroppingConvention`** (Priority 250) — strips configurable prefixes (`Get`, `m_`, `_`, `Str`) and suffixes (`Field`, `Property`, `Prop`) before matching.
- **`MethodToPropertyConvention`** (Priority 275) — links target properties to parameterless origin methods (e.g., `GetFullName()` → `FullName`).
- **`MethodAccessProvider`** — compiled `IValueProvider` for invoking a parameterless method via `Expression.Call`; null-safe origin handling.
- **`FlatteningConvention`** (Priority 300) — decomposes flattened target names into origin member chains (e.g., `CustomerAddressCity` → `Customer.Address.City`) using recursive greedy prefix matching with backtracking. Max depth 5.
- **`ChainedPropertyAccessProvider`** — `IValueProvider` navigating a `MemberInfo[]` chain with null-safe traversal via composed compiled accessors.
- **`UnflatteningConvention`** (Priority 350) — reverse of flattening; maps flat origin members to nested target structures with intermediate object creation via `Activator.CreateInstance`.
- **`UnflatteningValueProvider`** — reads multiple flat origin members and assembles them into a nested target object.
- **`AbbreviationConvention`** (Priority 400) — bidirectional abbreviation expansion via configurable alias dictionary (default: `Addr`↔`Address`, `Qty`↔`Quantity`, `Amt`↔`Amount`, `Desc`↔`Description`, `Num`↔`Number`, and more). Uses `NameNormalizer` for multi-segment expansion.
- **`StructuralSimilarityScorer`** — computes similarity score (0.0–1.0) between two types based on member name and type compatibility. Returns detailed `StructuralSimilarityResult` with matched/unmatched member lists.
- **`ConventionPipeline`** — orchestrator that runs conventions in priority order to build `List<PropertyLink>` for any origin/target pair. Includes `StrictMode` (throws on unlinked required members), `IgnoreUnlinked`, and `CreateDefault()` factory.
- **`NullValueProvider`** — internal no-op provider for skipped (unlinked) property links.
- **`ITypeConvention`** interface — contract for type-level conventions that discover which type pairs should be mapped.
- **`NameSuffixTypeConvention`** — auto-pairs types by matching base names with configurable suffixes (9 defaults: `Dto`, `ViewModel`, `Vm`, `Model`, `Response`, `Request`, `Command`, `Entity→Dto`, `Entity→ViewModel`). Verifies structural similarity above threshold (default 0.7).
- **`ConventionMatch`** factory methods — `CaseNormalized`, `Unflattened`, `PrefixDropped`, `MethodToProperty`, `Abbreviation`, `None`.

### Test Coverage

- 137 new convention tests (224 total across project).
- Test fixtures in `TestTypes/ConventionTestTypes.cs` covering: exact match, case interop, flattening (1–3 levels), backtracking, unflattening, prefix/suffix stripping, method-to-property, abbreviation expansion, structural similarity scoring, pipeline orchestration (strict mode, ignore unlinked, golden value verification, thread safety), and type convention pairing.

---

## [Sprint 1] — Foundation

### Added

- Core type system: `TypeModel`, `MemberModel`, `MethodModel`, `TypeModelCache`, `TypePair`.
- Mapping primitives: `Blueprint`, `PropertyLink`, `ConventionMatch`, `MappingScope`.
- Abstractions: `IValueProvider`, `ITypeTransformer`, `ISculptor`, `IMapper<S,D>`.
- Configuration: `MappingBlueprint`, `MappingStrategy`, `MappingContext`.
- Multi-TFM support: `netstandard2.1`, `net8.0`, `net10.0`.
