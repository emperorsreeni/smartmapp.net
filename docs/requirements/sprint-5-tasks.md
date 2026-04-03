# Sprint 5 — Collections & Flattening

> **Sprint Duration:** Weeks 9–10 (10 working days)  
> **Sprint Goal:** Map every collection type .NET offers and make flattening/unflattening seamless. By sprint end, all 12 collection types from §8.3 map correctly, `Dictionary<string, object>` ↔ object round-trips work, `ValueTuple` ↔ object mapping is functional, and flattening/unflattening integrates directly into the expression compilation pipeline.  
> **Total Estimate:** 118 story points (~118 hours)  
> **Parent Document:** [implementation-plan.md](./implementation-plan.md)  
> **Spec Reference:** [spec.md](./spec.md) — Sections 8.3, 8.4, 8.6, 8.7, 9.1

---

## Table of Contents

1. [Task Pipeline Overview](#1-task-pipeline-overview)
2. [Execution Order & Dependency Graph](#2-execution-order--dependency-graph)
3. [Detailed Task Breakdown](#3-detailed-task-breakdown)
4. [Test Inventory](#4-test-inventory)
5. [Sprint Definition of Done](#5-sprint-definition-of-done)

---

## 1. Task Pipeline Overview

```
DAY 1-2          DAY 2-4              DAY 4-6                DAY 6-7           DAY 7-8          DAY 8-10
──────           ──────               ──────                 ──────            ──────            ────────
S5-T00 ──┐       S5-T01 ──┐
(Collect.  │     (Array)    │
 Mapper    │                ├──> S5-T03(IEnum) ──┐
 Infra)    │     S5-T02 ──┤                      │
           │     (List<T>)  │                     │
           │                ├──> S5-T04(ICol/     │
           │                │    IReadOnly)       ├──> S5-T08 ──> S5-T10 ──> S5-T11
           │                │                     │   (Immutable  (DictObj   (Integration
           │     S5-T05 ────┤                     │    +Observ.)   +Tuple)    Tests)
           │     (HashSet)  │                     │
           │                └──> S5-T06 ──────────┤
           │                    (Dictionary)      │
           │                                      │
           │     S5-T07 ──────────────────────────┤
           │     (Flatten/Unflatten               │
           │      Integration)                    │
           │                                      │
           │     S5-T09 ──────────────────────────┘
           │     (Nested Collections)
           │
           └──────────────────────────────────────────────────────────────────────
```

### Execution Lanes

| Lane | Tasks | Focus |
|---|---|---|
| **Lane A — Infrastructure** | T00 | Collection type detection, dispatcher, shared helpers |
| **Lane B — Core Collections** | T01, T02, T03, T04 | Array, List, IEnumerable, ICollection/IReadOnlyList |
| **Lane C — Specialized Collections** | T05, T06 | HashSet, Dictionary |
| **Lane D — Immutable & Observable** | T08 | ImmutableList, ImmutableArray, ObservableCollection, ReadOnlyCollection |
| **Lane E — Flattening** | T07 | Flatten/unflatten integration into expression compiler |
| **Lane F — Advanced Mapping** | T09, T10 | Nested collections, Dictionary↔Object, ValueTuple↔Object |
| **Lane G — Verification** | T11 | End-to-end tests, polish, benchmark baseline |

---

## 2. Execution Order & Dependency Graph

```
Sprint 1 (TypeModel — IsCollection, IsDictionary, CollectionElementType)            ──┐
Sprint 2 (ConventionPipeline, FlatteningConvention, UnflatteningConvention)           │
Sprint 3 (TypeTransformerRegistry)                                                    │
Sprint 4 (BlueprintCompiler, ExpressionMappingCompiler, MappingDelegateCache,         │
          ComplexTypeDetector, NullSafeAccessBuilder, PropertyAssignmentBuilder)       │
                                                                                       v
    S5-T00 (CollectionMapper Infrastructure) ──────────────────────────────────────────┐
        │                                                                               │
        ├──> S5-T01 (Array Mapping) ──────────┬──> S5-T03 (IEnumerable) ──────────────┐│
        │                                     │                                        ││
        ├──> S5-T02 (List<T> Mapping) ────────┤                                       ││
        │                                     ├──> S5-T04 (ICollection/IReadOnly) ────┤│
        ├──> S5-T05 (HashSet<T>) ─────────────┤                                       ││
        │                                     │                                        ││
        ├──> S5-T06 (Dictionary<K,V>) ────────┘                                       ││
        │                                                                              ││
        ├──> S5-T07 (Flatten/Unflatten Integration) ──────────────────────────────────┤│
        │                                                                              vv
        │                                              S5-T08 (Immutable + Observable) ─┤
        │                                                                               │
        ├──> S5-T09 (Nested Collections) ──────────────────────────────────────────────┤
        │                                                                               v
        │                                              S5-T10 (Dict↔Object + Tuple↔Object)
        │                                                                               │
        │                                                                               v
        └──────────────────────────────── S5-T11 (Integration Tests + Polish + Benchmark)
```

### Critical Path

**Sprint 1/2/3/4 → T00 → T01 → T03 → T08 → T10 → T11**

---

## 3. Detailed Task Breakdown

---

### S5-T00 — `CollectionMapper` Infrastructure & Type Dispatch

| Field | Detail |
|---|---|
| **ID** | S5-T00 |
| **Title** | Build `CollectionMapper` — collection type detection, strategy dispatch, and shared helpers |
| **Estimate** | 10 pts |
| **Day** | Day 1–2 |
| **Predecessors** | Sprint 1 (`TypeModel.IsCollection`, `TypeModel.IsDictionary`, `TypeModel.CollectionElementType`), Sprint 4 (`BlueprintCompiler`, `MappingDelegateCache`, `ComplexTypeDetector`) |
| **Dependents** | S5-T01, S5-T02, S5-T03, S5-T04, S5-T05, S5-T06, S5-T07, S5-T08, S5-T09, S5-T10, S5-T11 |

#### Description

Central dispatcher that detects collection types on both origin and target, selects the appropriate mapping strategy, and delegates to the correct handler. This is the entry point for all collection mapping, invoked from `BlueprintCompiler`/`PropertyAssignmentBuilder` when a `PropertyLink` connects two collection-typed members.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/CollectionMapper.cs`.
2. Define `CollectionCategory` enum: `Array`, `List`, `Enumerable`, `Collection`, `ReadOnlyList`, `ReadOnlyCollection`, `HashSet`, `Dictionary`, `ImmutableList`, `ImmutableArray`, `ObservableCollection`, `Unknown`.
3. Implement `CollectionCategoryResolver.Resolve(TypeModel)` — inspect the target `TypeModel` and return the appropriate category based on interface implementations and concrete type.
4. Create `src/SmartMapp.Net/Collections/CollectionMappingStrategy.cs` record — holds origin/target element types, category, and whether element mapping is needed (complex type vs. assignable).
5. Create `src/SmartMapp.Net/Collections/ICollectionMappingHandler.cs` interface — `Expression BuildMappingExpression(CollectionMappingStrategy, ParameterExpression origin, ParameterExpression scope)`.
6. Implement `CollectionMapper.BuildCollectionExpression(...)` — resolves category, selects handler, returns expression tree fragment for the collection mapping.
7. Integrate into `PropertyAssignmentBuilder`: when `ComplexTypeDetector` identifies a collection type, delegate to `CollectionMapper` instead of recursive nested-object mapping.
8. Create shared `src/SmartMapp.Net/Collections/CollectionExpressionHelpers.cs` — reusable expression fragments for `for` loops, element mapping invocation, null checks on source collections.

#### Acceptance Criteria

- [ ] `CollectionCategoryResolver` correctly identifies all 12 collection types from §8.3 table
- [ ] Returns `Unknown` for non-collection types (classes, structs, string)
- [ ] `string` is NOT classified as a collection (despite implementing `IEnumerable<char>`)
- [ ] `CollectionMapper.BuildCollectionExpression` dispatches to the correct handler
- [ ] Integration into `PropertyAssignmentBuilder` — collection properties use `CollectionMapper` instead of recursive mapping
- [ ] Null source collection returns `null` (or configurable default) on target
- [ ] Compiles on all TFMs; XML docs complete

#### Technical Considerations

- `TypeModel.IsCollection` and `TypeModel.CollectionElementType` from Sprint 1 provide the core metadata. Extend if needed (e.g., `TypeModel.DictionaryKeyType`, `TypeModel.DictionaryValueType`).
- Category resolution priority: concrete type first (e.g., `HashSet<T>` over `IEnumerable<T>`), then interface fallback.
- `ICollectionMappingHandler` implementations are internal — one per category.
- On `netstandard2.1`, `ImmutableList<T>` requires `System.Collections.Immutable` — add conditional `PackageReference`.
- Element mapping: for primitive/assignable elements, emit direct assignment. For complex elements, emit recursive delegate call via `MappingDelegateCache`.

---

### S5-T01 — Array Mapping (`T[]` → `T[]`)

| Field | Detail |
|---|---|
| **ID** | S5-T01 |
| **Title** | Array mapping with `Array.Copy` fast-path for same-type elements |
| **Estimate** | 8 pts |
| **Day** | Day 2–3 |
| **Predecessors** | S5-T00 |
| **Dependents** | S5-T03, S5-T09, S5-T11 |

#### Description

Map arrays with optimal strategies based on element types. Same-type primitive arrays use `Array.Copy` for maximum throughput. Complex-type arrays allocate a new array and map each element via the compiled delegate.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/Handlers/ArrayMappingHandler.cs` implementing `ICollectionMappingHandler`.
2. **Fast-path:** Same element type + assignable → `Array.Copy` (zero per-element overhead).
3. **Element mapping path:** Different/complex element types → allocate `new TTarget[source.Length]`, iterate with `for` loop, call element mapping delegate per item.
4. Build expression tree: `Expression.NewArrayBounds(elementType, lengthExpr)` for allocation, `Expression.ArrayAccess` for indexing.
5. Null source array → return `null`.
6. Empty source array → return empty array (`Array.Empty<T>()` on .NET 8+, `new T[0]` on netstandard2.1).

#### Acceptance Criteria

- [ ] `int[]` → `int[]` uses `Array.Copy` (verified via benchmark or expression tree inspection)
- [ ] `string[]` → `string[]` uses `Array.Copy`
- [ ] `Order[]` → `OrderDto[]` maps each element via compiled delegate
- [ ] Empty array → empty array (not null)
- [ ] Null source → null target
- [ ] Single-element array maps correctly
- [ ] Large array (10K elements) maps correctly and performs well
- [ ] XML docs complete

#### Technical Considerations

- `Array.Copy` is the fastest possible path for same-type elements — ~10x faster than per-element iteration for primitives.
- `Expression.Call(typeof(Array), "Copy", ...)` for the fast-path expression.
- For `#if NET8_0_OR_GREATER`, use `Array.Empty<T>()` for empty arrays. On `netstandard2.1`, cache empty arrays to avoid allocation.
- Element mapping delegate obtained via `MappingDelegateCache.GetOrCompile(elementTypePair)`.

---

### S5-T02 — `List<T>` Mapping with Pre-Sized Allocation

| Field | Detail |
|---|---|
| **ID** | S5-T02 |
| **Title** | `List<T>` mapping with pre-sized capacity and element mapping |
| **Estimate** | 8 pts |
| **Day** | Day 2–3 |
| **Predecessors** | S5-T00 |
| **Dependents** | S5-T03, S5-T04, S5-T09, S5-T11 |

#### Description

Map `List<T>` to `List<T>` with pre-allocated capacity to avoid list resizing. Supports both same-type (direct add) and cross-type (mapped add) element scenarios.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/Handlers/ListMappingHandler.cs` implementing `ICollectionMappingHandler`.
2. **Pre-sized allocation:** `new List<TTarget>(source.Count)` — avoids internal array resizing.
3. **Assignable elements:** Direct `Add` without mapping.
4. **Complex elements:** Map each element via compiled delegate, then `Add`.
5. Build expression tree with `for` loop using `source.Count` and indexer access (`source[i]`).
6. Handle `IList<T>` targets (concrete `List<T>` instantiation).
7. Null source → null target. Empty source → empty list.

#### Acceptance Criteria

- [ ] `List<int>` → `List<int>` copies all elements, pre-sized
- [ ] `List<Order>` → `List<OrderDto>` maps each element via delegate
- [ ] Pre-sized allocation verified (capacity ≥ source.Count)
- [ ] Empty list → empty list (not null)
- [ ] Null source → null target
- [ ] `IList<T>` target resolved to `List<T>` concrete
- [ ] 10K-element list maps correctly
- [ ] XML docs complete

#### Technical Considerations

- `Expression.New(typeof(List<>).MakeGenericType(targetElementType), new[] { countExpr })` for pre-sized ctor.
- Use `Expression.Property(listExpr, "Count")` and `Expression.Call(listExpr, "get_Item", indexExpr)` for indexed access.
- `List<T>.Add` via `Expression.Call(targetList, addMethod, elementExpr)`.
- Performance target from §9.1: collection of 1000 flat DTOs < 100μs.

---

### S5-T03 — `IEnumerable<T>` Materialization

| Field | Detail |
|---|---|
| **ID** | S5-T03 |
| **Title** | `IEnumerable<T>` → materialized `List<T>` / `T[]` mapping |
| **Estimate** | 8 pts |
| **Day** | Day 4–5 |
| **Predecessors** | S5-T00, S5-T01, S5-T02 |
| **Dependents** | S5-T08, S5-T10, S5-T11 |

#### Description

When the origin is `IEnumerable<T>` (not a concrete collection), materialize it into the target collection type. Detect whether the `IEnumerable<T>` is actually backed by a countable collection (via `ICollection<T>` or `IReadOnlyCollection<T>`) for pre-sizing, otherwise fall back to streaming enumeration.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/Handlers/EnumerableMappingHandler.cs` implementing `ICollectionMappingHandler`.
2. **Countable fast-path:** Runtime `is ICollection<T>` check → extract count → pre-allocate.
3. **Streaming path:** Enumerate and add to growing `List<T>`, then convert to target type if needed (e.g., `.ToArray()`).
4. Target type dispatch:
   - Target is `List<T>` → materialize to `List<T>`.
   - Target is `T[]` → materialize to `List<T>`, then `.ToArray()`.
   - Target is `IEnumerable<T>` → materialize to `List<T>` (concrete backing).
   - Target is `IReadOnlyList<T>` → materialize to `List<T>` (implements the interface).
5. Handle deferred `IEnumerable<T>` (LINQ queries, generators) — must enumerate exactly once.
6. Null source → null target.

#### Acceptance Criteria

- [ ] `IEnumerable<int>` → `List<int>` materializes correctly
- [ ] `IEnumerable<int>` → `int[]` materializes and converts to array
- [ ] `IEnumerable<Order>` → `List<OrderDto>` maps each element
- [ ] Countable `IEnumerable<T>` (backed by List) pre-sizes allocation
- [ ] Non-countable `IEnumerable<T>` (yield return) enumerates once and materializes
- [ ] Deferred enumeration: side effects execute exactly once
- [ ] Empty enumerable → empty collection (not null)
- [ ] Null source → null target
- [ ] XML docs complete

#### Technical Considerations

- Runtime `is ICollection<T>` check via `Expression.TypeIs(sourceExpr, typeof(ICollection<>).MakeGenericType(...))` for pre-sizing.
- `IEnumerable<T>` requires `GetEnumerator()` / `MoveNext()` / `Current` pattern in expression trees. Use a helper method `CollectionExpressionHelpers.EnumerateAndMap(source, mapper)` to simplify expression tree construction.
- Alternative: emit `Expression.Call(typeof(Enumerable), "ToList", ...)` for simple materialization, then map in a second pass. Evaluate trade-off between expression tree complexity and performance.
- Enumerating exactly once is critical for correctness with generators and database queries.

---

### S5-T04 — `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` Mapping

| Field | Detail |
|---|---|
| **ID** | S5-T04 |
| **Title** | Interface collection types: `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` |
| **Estimate** | 8 pts |
| **Day** | Day 4–5 |
| **Predecessors** | S5-T00, S5-T02 |
| **Dependents** | S5-T08, S5-T11 |

#### Description

Handle target properties typed as collection interfaces. Resolve each interface to an appropriate concrete implementation, then delegate to the corresponding handler.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/Handlers/InterfaceCollectionMappingHandler.cs` implementing `ICollectionMappingHandler`.
2. Interface → concrete resolution:
   - `ICollection<T>` → `List<T>`
   - `IReadOnlyList<T>` → `List<T>` (implements `IReadOnlyList<T>`)
   - `IReadOnlyCollection<T>` → `List<T>` (implements `IReadOnlyCollection<T>`)
   - `IList<T>` → `List<T>`
3. Pre-size when source is countable. Stream when not.
4. Element mapping for complex types via compiled delegate.
5. Null source → null target. Empty source → empty concrete collection.

#### Acceptance Criteria

- [ ] `List<int>` → `ICollection<int>` returns a `List<int>` instance
- [ ] `int[]` → `IReadOnlyList<int>` returns a `List<int>` typed as `IReadOnlyList<int>`
- [ ] `List<Order>` → `IReadOnlyCollection<OrderDto>` maps elements correctly
- [ ] Pre-sized when source implements `ICollection<T>`
- [ ] Empty source → empty collection (correct concrete type)
- [ ] Null source → null target
- [ ] Returned instances implement the target interface verified via `is` check
- [ ] XML docs complete

#### Technical Considerations

- `List<T>` implements `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, `IList<T>`, and `IEnumerable<T>` — making it the universal concrete fallback.
- Expression must cast the constructed `List<T>` to the target interface type via `Expression.Convert`.
- Target interface detection via `TypeModel.ImplementedInterfaces` and generic type definition matching.
- Consider `ISet<T>` → `HashSet<T>` resolution (defer to S5-T05 handler).

---

### S5-T05 — `HashSet<T>` Mapping (Set Semantics)

| Field | Detail |
|---|---|
| **ID** | S5-T05 |
| **Title** | `HashSet<T>` mapping with set semantics preservation |
| **Estimate** | 6 pts |
| **Day** | Day 3–4 |
| **Predecessors** | S5-T00 |
| **Dependents** | S5-T11 |

#### Description

Map `HashSet<T>` with correct set semantics: duplicate elements in the mapped output are handled by the target `HashSet<T>`'s equality comparer. Support both same-type and cross-type element scenarios.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/Handlers/HashSetMappingHandler.cs` implementing `ICollectionMappingHandler`.
2. **Pre-sized allocation:** `new HashSet<TTarget>(source.Count)` (capacity ctor available .NET 8+; use default ctor on netstandard2.1).
3. **Assignable elements:** Direct `Add` per element.
4. **Complex elements:** Map each element via delegate, then `Add` (duplicates naturally handled by set).
5. Handle `ISet<T>` target → resolve to `HashSet<T>`.
6. Null source → null target. Empty source → empty `HashSet<T>`.

#### Acceptance Criteria

- [ ] `HashSet<int>` → `HashSet<int>` preserves all unique values
- [ ] `HashSet<Order>` → `HashSet<OrderDto>` maps elements
- [ ] Duplicate mapped elements handled by set semantics (no exception)
- [ ] `ISet<T>` target → `HashSet<T>` concrete
- [ ] Empty set → empty set
- [ ] Null source → null target
- [ ] `#if NET8_0_OR_GREATER` for capacity constructor
- [ ] XML docs complete

#### Technical Considerations

- `HashSet<T>(int capacity)` constructor is .NET Core 2.1+ / .NET Standard 2.1 — verify availability.
- Set semantics mean mapped output may have fewer elements than input if element mapping produces duplicates. This is correct behavior.
- Enumerate source via `foreach` pattern (`GetEnumerator`/`MoveNext`/`Current`) in expression tree.
- Consider preserving custom `IEqualityComparer<T>` if the source set has one — scope for future enhancement, not this sprint.

---

### S5-T06 — `Dictionary<K,V>` Mapping (Key + Value Mapping)

| Field | Detail |
|---|---|
| **ID** | S5-T06 |
| **Title** | `Dictionary<K,V>` mapping with key and value mapping |
| **Estimate** | 10 pts |
| **Day** | Day 3–5 |
| **Predecessors** | S5-T00 |
| **Dependents** | S5-T10, S5-T11 |

#### Description

Map dictionaries with independent key and value transformations. Support same-type keys (common case: `string` keys), cross-type keys (less common), and complex value mapping.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/Handlers/DictionaryMappingHandler.cs` implementing `ICollectionMappingHandler`.
2. Extract key/value types from origin and target `TypeModel` (via `TypeModel.DictionaryKeyType`, `TypeModel.DictionaryValueType` — extend `TypeModel` if needed).
3. **Same key+value types:** Direct copy via `new Dictionary<K,V>(source)` copy constructor.
4. **Different value types:** Iterate `KeyValuePair<K,V>`, map values, add to new dictionary.
5. **Different key types:** Iterate, map both keys and values, add to new dictionary.
6. Pre-size dictionary: `new Dictionary<TK2, TV2>(source.Count)`.
7. Handle `IDictionary<K,V>` target → resolve to `Dictionary<K,V>`.
8. Handle `IReadOnlyDictionary<K,V>` target → resolve to `Dictionary<K,V>`.
9. Null source → null. Empty → empty dictionary.

#### Acceptance Criteria

- [ ] `Dictionary<string, int>` → `Dictionary<string, int>` copies all entries
- [ ] `Dictionary<string, Order>` → `Dictionary<string, OrderDto>` maps values
- [ ] `Dictionary<int, string>` → `Dictionary<long, string>` maps keys via transformer
- [ ] Both keys and values mapped when both types differ
- [ ] Pre-sized allocation (capacity = source.Count)
- [ ] `IDictionary<K,V>` target resolved to `Dictionary<K,V>`
- [ ] `IReadOnlyDictionary<K,V>` target resolved to `Dictionary<K,V>`
- [ ] Duplicate mapped keys throw (standard Dictionary behavior)
- [ ] Empty dictionary → empty dictionary
- [ ] Null source → null target
- [ ] XML docs complete

#### Technical Considerations

- Dictionary copy constructor `new Dictionary<K,V>(source)` is the optimal same-type path.
- Expression tree iteration: enumerate `source` as `IEnumerable<KeyValuePair<K,V>>`, access `.Key` and `.Value` properties.
- Key mapping uses `TypeTransformerRegistry` (e.g., `int` → `long`). Value mapping uses `MappingDelegateCache` for complex types or `TypeTransformerRegistry` for simple conversions.
- On `netstandard2.1`, `Dictionary<K,V>(int capacity)` constructor is available.
- `ConcurrentDictionary<K,V>` is out of scope for this sprint — can be added later.

---

### S5-T07 — Flatten/Unflatten Integration into Expression Compiler

| Field | Detail |
|---|---|
| **ID** | S5-T07 |
| **Title** | Integrate flattening and unflattening into the mapping expression compilation pipeline |
| **Estimate** | 13 pts |
| **Day** | Day 3–6 |
| **Predecessors** | S5-T00, Sprint 2 (`FlatteningConvention`, `UnflatteningConvention`), Sprint 4 (`BlueprintCompiler`, `NullSafeAccessBuilder`, `PropertyAssignmentBuilder`) |
| **Dependents** | S5-T11 |

#### Description

Sprint 2 conventions already detect flattened/unflattened property links (e.g., `CustomerAddressCity` → `Customer.Address.City`). This task integrates those links into the expression compilation pipeline so that the compiled delegate performs multi-level property traversal (flattening) and multi-level object construction (unflattening) seamlessly.

**Subtasks:**

1. Create `src/SmartMapp.Net/Compilation/FlattenExpressionBuilder.cs`.
2. **Flattening (deep read):** For a `PropertyLink` where `LinkedBy.ConventionName == "Flattening"`, the origin member path is a compound chain (e.g., `Customer.Address.City`). Build a chain of `Expression.Property(Expression.Property(...))` calls with null-safe guards at each level (reuse `NullSafeAccessBuilder`).
3. **Unflattening (deep write):** For a `PropertyLink` where `LinkedBy.ConventionName == "Unflattening"`, the target member path is a compound chain. Build:
   a. Null-check each intermediate target object.
   b. Construct intermediate objects if null (e.g., `target.Customer ??= new Customer()`).
   c. Assign the leaf property.
4. Modify `PropertyAssignmentBuilder` to detect flattened/unflattened links and delegate to `FlattenExpressionBuilder`.
5. Support 1, 2, and 3 levels of depth for both flattening and unflattening.
6. Handle mixed scenarios: some properties are flat, some are flattened, some are unflattened within the same Blueprint.

#### Acceptance Criteria

- [ ] Flattening: `Order.Customer.FirstName` → `OrderFlatDto.CustomerFirstName` works
- [ ] Flattening: 3 levels deep (`Order.Customer.Address.City` → `CustomerAddressCity`)
- [ ] Flattening: null intermediate (`Customer == null`) → target property is `default`
- [ ] Unflattening: `OrderFlatDto.CustomerFirstName` → `Order.Customer.FirstName` works
- [ ] Unflattening: 3 levels deep
- [ ] Unflattening: intermediate objects auto-constructed when null
- [ ] Mixed flat + flattened + unflattened properties in same Blueprint
- [ ] Works with existing null-safe navigation from Sprint 4
- [ ] Round-trip: flatten then unflatten preserves values
- [ ] XML docs complete

#### Technical Considerations

- **Flattening** is read-side — the origin member path is multi-part. `PropertyLink.Provider` for flattened links should provide the member chain (array of `MemberInfo`). `NullSafeAccessBuilder` already handles multi-level null guards.
- **Unflattening** is write-side — more complex. Requires `Expression.Assign` on intermediate members + null-coalescing construction. Use `Expression.Coalesce(member, Expression.Assign(member, newExpr))` pattern.
- Intermediate object construction must use `TargetConstructionResolver` to handle types with non-default constructors.
- Sprint 2's `ConventionMatch.OriginMemberPath` contains the dot-separated path (e.g., `"Customer.Address.City"`).
- Performance: flattening adds expression tree depth but compiled code is optimal — just property access chains with null checks.

---

### S5-T08 — Immutable Collections & Observable/ReadOnly Collections

| Field | Detail |
|---|---|
| **ID** | S5-T08 |
| **Title** | `ImmutableList<T>`, `ImmutableArray<T>`, `ObservableCollection<T>`, `ReadOnlyCollection<T>` mapping |
| **Estimate** | 10 pts |
| **Day** | Day 6–7 |
| **Predecessors** | S5-T00, S5-T03, S5-T04 |
| **Dependents** | S5-T11 |

#### Description

Map immutable and specialized observable/read-only collections. Immutable collections use their Builder pattern for efficient construction. Observable/ReadOnly collections wrap standard lists.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/Handlers/ImmutableCollectionMappingHandler.cs` implementing `ICollectionMappingHandler`.
2. **`ImmutableList<T>`:** Use `ImmutableList.CreateBuilder<T>()`, add mapped elements, call `.ToImmutable()`.
3. **`ImmutableArray<T>`:** Use `ImmutableArray.CreateBuilder<T>(source.Count)`, add mapped elements, call `.ToImmutable()`.
4. Create `src/SmartMapp.Net/Collections/Handlers/ObservableCollectionMappingHandler.cs`.
5. **`ObservableCollection<T>`:** Construct `new ObservableCollection<T>()`, add mapped elements via `.Add()`.
6. **`ReadOnlyCollection<T>`:** Construct `new List<T>(capacity)`, populate, then wrap with `new ReadOnlyCollection<T>(list)`.
7. Handle `IImmutableList<T>` interface target → resolve to `ImmutableList<T>`.
8. All handlers: null source → null target; empty source → empty collection.

#### Acceptance Criteria

- [ ] `List<int>` → `ImmutableList<int>` uses builder pattern, produces correct immutable list
- [ ] `int[]` → `ImmutableArray<int>` uses builder pattern, produces correct immutable array
- [ ] `List<Order>` → `ImmutableList<OrderDto>` maps elements via delegate
- [ ] `List<int>` → `ObservableCollection<int>` produces correct observable
- [ ] `List<Order>` → `ReadOnlyCollection<OrderDto>` maps and wraps in ReadOnly
- [ ] Empty collections → empty immutable/observable/readonly (not null)
- [ ] Null source → null target
- [ ] `IImmutableList<T>` target → `ImmutableList<T>` concrete
- [ ] `System.Collections.Immutable` package referenced conditionally
- [ ] XML docs complete

#### Technical Considerations

- `System.Collections.Immutable` NuGet package is required for `netstandard2.1`. For `net8.0`/`net9.0`, it's included in the SDK.
- Add `<PackageReference Include="System.Collections.Immutable" />` in `Directory.Packages.props`, conditionally referenced by the core project.
- Builder pattern expressions: `Expression.Call(typeof(ImmutableList), "CreateBuilder", ...)` followed by `Add` calls and `.ToImmutable()`.
- `ImmutableArray.CreateBuilder<T>(int capacity)` allows pre-sizing — prefer when count is known.
- `ObservableCollection<T>` is in `System.Collections.ObjectModel` — available in all TFMs.
- `ReadOnlyCollection<T>` wraps an `IList<T>` — construct the inner list first, then wrap.

---

### S5-T09 — Nested Collections (Collections of Collections)

| Field | Detail |
|---|---|
| **ID** | S5-T09 |
| **Title** | Support nested/composite collections: `List<List<T>>`, `T[][]`, `Dictionary<K, List<V>>` |
| **Estimate** | 8 pts |
| **Day** | Day 5–7 |
| **Predecessors** | S5-T00, S5-T01, S5-T02, S5-T06 |
| **Dependents** | S5-T11 |

#### Description

Handle collections whose elements are themselves collections. The `CollectionMapper` must recursively detect that the element type is a collection and apply collection mapping at the element level rather than treating the inner collection as a complex object.

**Subtasks:**

1. Modify `CollectionMapper.BuildCollectionExpression` to detect when the element type is itself a collection (via `TypeModel.IsCollection` on the element type).
2. For nested collection elements, recursively invoke `CollectionMapper` rather than the standard `MappingDelegateCache` complex-type path.
3. **`List<List<int>>` → `List<List<int>>`**: outer List iteration, inner List copied per element.
4. **`int[][]` → `int[][]`**: outer array iteration, inner arrays via `Array.Copy`.
5. **`Dictionary<string, List<Order>>` → `Dictionary<string, List<OrderDto>>`**: dictionary iteration, value is a collection → collection mapping for each value.
6. Support up to 3 levels of nesting.
7. Null inner collections → null inner elements in target.

#### Acceptance Criteria

- [ ] `List<List<int>>` → `List<List<int>>` maps all inner lists correctly
- [ ] `int[][]` → `int[][]` copies inner arrays
- [ ] `List<Order[]>` → `List<OrderDto[]>` maps inner elements
- [ ] `Dictionary<string, List<Order>>` → `Dictionary<string, List<OrderDto>>` maps dictionary values
- [ ] 3-level nesting: `List<List<List<int>>>` works correctly
- [ ] Null inner collection → null in target
- [ ] Empty inner collection → empty in target
- [ ] XML docs complete

#### Technical Considerations

- Recursive `CollectionMapper` invocation via expression tree — the element mapping expression for the outer collection IS a collection mapping expression for the inner collection.
- Avoid infinite recursion for pathological types (e.g., `List<T>` where `T` is `List<T>`) — use `MappingScope` depth limit.
- `ComplexTypeDetector.IsComplexType` must return `false` for collection types — collections are handled by `CollectionMapper`, not nested-object mapping.
- Performance: nested collections compound — `List<List<int>>` with 100×100 = 10K total elements should map in < 200μs.

---

### S5-T10 — `Dictionary<string, object>` ↔ Object & ValueTuple ↔ Object

| Field | Detail |
|---|---|
| **ID** | S5-T10 |
| **Title** | Dictionary-to-object, object-to-dictionary, and ValueTuple-to-object bidirectional mapping |
| **Estimate** | 13 pts |
| **Day** | Day 7–9 |
| **Predecessors** | S5-T03, S5-T06, Sprint 4 (`BlueprintCompiler`, `TypeTransformerRegistry`) |
| **Dependents** | S5-T11 |

#### Description

Two advanced mapping scenarios that bridge unstructured data to typed objects:
1. **Dictionary ↔ Object:** Map `Dictionary<string, object>` to/from typed objects using property name matching.
2. **ValueTuple ↔ Object:** Map named `ValueTuple` fields to/from typed object properties.

**Subtasks:**

1. Create `src/SmartMapp.Net/Collections/DictionaryObjectMapper.cs`.
2. **Dict → Object:**
   a. For each writable target member, look up the dictionary by member name (case-insensitive).
   b. Apply type transformation if the dictionary value type differs from the target member type.
   c. Handle nested dictionaries → recursive mapping to nested complex types.
   d. Handle dictionary values that are collections (e.g., `List<object>` → `List<T>`).
3. **Object → Dict:**
   a. For each readable origin member, add entry to dictionary: key = member name, value = member value.
   b. Complex nested properties → recursive conversion to nested `Dictionary<string, object>`.
   c. Collection properties → preserve as-is or convert to `List<object>`.
4. Create `src/SmartMapp.Net/Collections/ValueTupleMapper.cs`.
5. **Tuple → Object:**
   a. Detect `ValueTuple<...>` and extract `Item1`/`Item2`/.../`ItemN` fields.
   b. Use tuple element names (from `TupleElementNamesAttribute`) when available for name matching.
   c. Match tuple fields to target properties by name (named tuples) or by position (unnamed).
   d. Apply type transformers as needed.
6. **Object → Tuple:**
   a. Construct `ValueTuple<...>` from matched origin properties.
   b. Use `Activator.CreateInstance` or `Expression.New` for tuple construction.
7. Register `DictionaryObjectMapper` and `ValueTupleMapper` in `BlueprintCompiler` as special-case handlers.

#### Acceptance Criteria

- [ ] `Dictionary<string, object>` → `CustomerDto` maps `["Id"] = 1, ["Name"] = "Alice"` correctly
- [ ] Nested dictionary → nested object: `["Address"] = { ["City"] = "Seattle" }` → `Customer.Address.City`
- [ ] `CustomerDto` → `Dictionary<string, object>` produces correct key-value pairs
- [ ] Object → Dict → Object round-trip preserves all values
- [ ] Type coercion: dictionary `int` value to target `long` property via transformer
- [ ] `(int Id, string Name, int Age)` → `PersonDto` maps named tuple fields
- [ ] `PersonDto` → `(int Id, string Name, int Age)` constructs correct tuple
- [ ] Unnamed tuple: `(int, string)` → object matches by position (Item1→first writable, Item2→second)
- [ ] Tuple ↔ Object round-trip preserves values
- [ ] Null dictionary/tuple → null target
- [ ] XML docs complete

#### Technical Considerations

- **Dictionary key lookup:** Use `StringComparer.OrdinalIgnoreCase` for case-insensitive matching.
- **Nested dictionaries:** Recursive `DictionaryObjectMapper` invocation when dictionary value is itself `IDictionary<string, object>` and target member is a complex type.
- **Type coercion from `object`:** Dictionary values are `object` — use `Expression.Convert` and `TypeTransformerRegistry` for safe conversion.
- **ValueTuple element names:** `TupleElementNamesAttribute` is applied to the containing method/property, not the tuple type itself. Require attribute access from the declaring member's metadata.
- **ValueTuple construction:** `ValueTuple<T1, T2, ...>` has a constructor taking all fields — use `Expression.New(tupleCtorInfo, args)`.
- For tuples with > 7 elements, .NET uses `ValueTuple<T1, ..., T7, TRest>` nesting — handle this recursion.
- On `netstandard2.1`, `System.ValueTuple` is available. `TupleElementNamesAttribute` is in `System.Runtime`.

---

### S5-T11 — Integration Tests, Edge Cases, Benchmark Baseline & Polish

| Field | Detail |
|---|---|
| **ID** | S5-T11 |
| **Title** | Comprehensive integration tests, edge cases, benchmark baseline, and code polish |
| **Estimate** | 16 pts |
| **Day** | Day 8–10 |
| **Predecessors** | S5-T00 through S5-T10 |
| **Dependents** | Sprint 6, Sprint 7, Sprint 8 |

#### Description

End-to-end validation of all collection mapping, flattening, dictionary↔object, and tuple↔object features. Establish benchmark baselines. Polish code, XML docs, and ensure zero regressions.

**Subtasks:**

1. Create test types: collection-holding domain models and DTOs for every collection type.
   - `OrderWithLines` (List), `Matrix` (jagged arrays), `TagCloud` (HashSet), `Metadata` (Dictionary), `ImmutableOrder`, `ObservableOrder`.
   - Flat DTOs for flatten/unflatten: `OrderFlatDto`, `CustomerFlatDto` (1/2/3 levels).
   - Tuple types: `PersonTuple`, named and unnamed variants.
2. **Collection tests** (~24):
   - Each of 12 collection types: empty, single, many (10+ elements).
   - Complex element mapping within each collection type.
   - Null source collection → null target.
3. **Nested collection tests** (~4):
   - `List<List<int>>`, `int[][]`, `List<Order[]>`, `Dict<string, List<Order>>`.
4. **Dictionary↔Object tests** (~5):
   - Dict → object, object → dict, round-trip, nested dict, type coercion.
5. **Tuple↔Object tests** (~4):
   - Named tuple → object, object → tuple, round-trip, unnamed tuple.
6. **Flattening tests** (~6):
   - 1-level, 2-level, 3-level flatten and unflatten. Null intermediates. Round-trip.
7. **Edge cases** (~5):
   - Null elements within collections.
   - Collection of null references.
   - Mixed collection (e.g., `List<object>` containing different types).
   - Extremely large collection (100K elements) — correctness, not perf.
   - Collection property that is both collection and has `[Unmapped]`.
8. **Benchmark baseline:**
   - BenchmarkDotNet suite: 1K flat DTOs collection, 10K flat DTOs collection, nested collection, dict↔object.
   - Record baselines for Sprint 9 comparison.
9. **Polish:**
   - XML docs on all new public/internal types.
   - Zero compiler warnings.
   - All Sprint 1–4 tests still pass (no regressions).

#### Acceptance Criteria

- [ ] ≥ 48 new unit tests, all passing
- [ ] All 12 collection types from §8.3 table covered
- [ ] Dictionary↔object and Tuple↔object round-trips verified
- [ ] Flattening/unflattening 1–3 levels verified
- [ ] Nested collections verified
- [ ] All Sprint 1–4 tests still pass (no regressions)
- [ ] Zero compiler warnings
- [ ] Code coverage ≥ 90% for `Collections/` namespace
- [ ] XML docs complete on all new types
- [ ] BenchmarkDotNet baselines recorded for collection scenarios
- [ ] Performance: 1K flat DTO collection < 100μs (§9.1)

#### Technical Considerations

- Use `FluentAssertions` for readable collection assertions: `.Should().BeEquivalentTo(expected)`.
- Create reusable test fixture factory methods in `tests/SmartMapp.Net.Tests.Unit/TestTypes/` for collection test data.
- BenchmarkDotNet: `[MemoryDiagnoser]` to track allocations. Compare pre-sized vs non-pre-sized paths.
- Thread safety: run parallel `Map` calls on collection properties from 1K tasks — verify no corruption.
- Test on all TFMs to catch conditional compilation issues (especially `System.Collections.Immutable`).

---

## 4. Test Inventory

| Area | Test File | Count | Focus |
|---|---|---|---|
| CollectionMapper Infrastructure | `CollectionMapperTests.cs` | 5 | Category resolution, dispatch, string exclusion |
| Array Mapping | `ArrayMappingTests.cs` | 5 | Same-type fast-path, complex elements, empty, null, large |
| List Mapping | `ListMappingTests.cs` | 5 | Pre-sized, complex elements, IList target, empty, null |
| IEnumerable Materialization | `EnumerableMappingTests.cs` | 5 | Countable, streaming, ToArray, deferred, null |
| Interface Collections | `InterfaceCollectionMappingTests.cs` | 5 | ICollection, IReadOnlyList, IReadOnlyCollection, empty, null |
| HashSet Mapping | `HashSetMappingTests.cs` | 4 | Same-type, complex, duplicates, ISet target |
| Dictionary Mapping | `DictionaryMappingTests.cs` | 6 | Same-type, value mapping, key mapping, both, IDictionary, IReadOnlyDictionary |
| Immutable Collections | `ImmutableCollectionMappingTests.cs` | 5 | ImmutableList, ImmutableArray, builder pattern, interface target, empty |
| Observable/ReadOnly | `ObservableReadOnlyMappingTests.cs` | 4 | ObservableCollection, ReadOnlyCollection, complex elements, empty |
| Nested Collections | `NestedCollectionMappingTests.cs` | 4 | List-of-list, jagged array, dict-of-list, 3-level |
| Dictionary↔Object | `DictionaryObjectMappingTests.cs` | 5 | Dict→obj, obj→dict, round-trip, nested dict, type coercion |
| Tuple↔Object | `ValueTupleMappingTests.cs` | 4 | Named→obj, obj→tuple, round-trip, unnamed |
| Flattening Integration | `FlattenUnflattenMappingTests.cs` | 6 | 1/2/3-level flatten, 1/2/3-level unflatten, null intermediates, round-trip |
| Edge Cases | `CollectionEdgeCaseTests.cs` | 5 | Null elements, large collection, mixed types, Unmapped, collection-of-null |
| **Total** | | **~68** | |

---

## 5. Sprint Definition of Done

### Task-Level DoD

A task is **done** when:

- [ ] Implementation compiles on all TFMs (`netstandard2.1`, `net8.0`, `net9.0`)
- [ ] All acceptance criteria verified via unit tests
- [ ] Zero compiler warnings (`TreatWarningsAsErrors`)
- [ ] XML documentation on all public and internal members
- [ ] No new analyzer diagnostics (StyleCop, .NET analyzers, SonarAnalyzer)
- [ ] Existing tests still pass (no regression)
- [ ] New code follows established patterns and conventions (`.editorconfig` compliance)

### Sprint-Level DoD

Sprint 5 is **done** when:

- [ ] All 12 tasks (T00–T11) implemented and verified
- [ ] All ~68 unit tests pass (100% green)
- [ ] All 12 collection types from §8.3 table map correctly end-to-end
- [ ] Dictionary↔object bidirectional mapping works with nested dictionaries
- [ ] ValueTuple↔object bidirectional mapping works with named and unnamed tuples
- [ ] Flattening/unflattening integrated into expression compiler for 1–3 levels
- [ ] Nested collections (collections of collections) map correctly
- [ ] Code coverage ≥ 90% for `Collections/` and `Compilation/FlattenExpressionBuilder`
- [ ] Performance: 1K flat DTO collection < 100μs (§9.1 target)
- [ ] BenchmarkDotNet baselines recorded for collection scenarios
- [ ] No performance regression > 10% on Sprint 1–4 benchmarks
- [ ] Thread-safety verified for concurrent collection mapping
- [ ] Conditional compilation verified — builds succeed on all target TFMs
- [ ] `System.Collections.Immutable` package reference correct for netstandard2.1
- [ ] CHANGELOG.md updated with Sprint 5 entries
- [ ] Sprint tasks document finalized in `docs/requirements/sprint-5-tasks.md`
- [ ] Sprint demo / review completed

### Release Gate

- [ ] Mutation testing score ≥ 80% for `Collections/` namespace
- [ ] BenchmarkDotNet baselines established for: array, list, dictionary, immutable, nested, 1K collection, 10K collection
- [ ] All existing tests (Sprint 1–4) pass without modification
- [ ] `sculptor.Map<Order, OrderDto>(order)` with collection properties works end-to-end
- [ ] `sculptor.Map<Order, OrderFlatDto>(order)` with flattening works end-to-end

---

## Appendix A — Task Summary Table

| ID | Title | Est. | Day | Predecessors | Dependents | Critical Path? |
|---|---|---|---|---|---|---|
| S5-T00 | CollectionMapper Infrastructure | 10 pts | 1–2 | Sprint 1/2/3/4 | All S5 tasks | Yes |
| S5-T01 | Array Mapping (`T[]` → `T[]`) | 8 pts | 2–3 | T00 | T03, T09, T11 | Yes |
| S5-T02 | List<T> Mapping | 8 pts | 2–3 | T00 | T03, T04, T09, T11 | No |
| S5-T03 | IEnumerable<T> Materialization | 8 pts | 4–5 | T00, T01, T02 | T08, T10, T11 | Yes |
| S5-T04 | ICollection/IReadOnlyList/IReadOnlyCollection | 8 pts | 4–5 | T00, T02 | T08, T11 | No |
| S5-T05 | HashSet<T> Mapping | 6 pts | 3–4 | T00 | T11 | No |
| S5-T06 | Dictionary<K,V> Mapping | 10 pts | 3–5 | T00 | T10, T11 | No |
| S5-T07 | Flatten/Unflatten Integration | 13 pts | 3–6 | T00, Sprint 2, Sprint 4 | T11 | No |
| S5-T08 | Immutable + Observable/ReadOnly | 10 pts | 6–7 | T00, T03, T04 | T11 | Yes |
| S5-T09 | Nested Collections | 8 pts | 5–7 | T00, T01, T02, T06 | T11 | No |
| S5-T10 | Dict↔Object + Tuple↔Object | 13 pts | 7–9 | T03, T06, Sprint 4 | T11 | Yes |
| S5-T11 | Integration Tests + Polish + Benchmark | 16 pts | 8–10 | All | Sprint 6, 7, 8 | Yes |
| | **TOTAL** | **118 pts** | | | | |

---

## Appendix B — Parallel Execution Schedule

For a team of **2 developers**:

| Day | Dev 1 | Dev 2 |
|---|---|---|
| 1 | **T00** (CollectionMapper Infrastructure) | — (waiting / assist T00) |
| 2 | **T01** (Array Mapping) | **T02** (List<T> Mapping) |
| 3 | **T01** finish + **T05** (HashSet) | **T06** (Dictionary) — start |
| 4 | **T03** (IEnumerable) — start | **T06** (Dictionary) — finish + **T07** (Flatten) — start |
| 5 | **T03** (IEnumerable) — finish + **T04** (Interface Collections) | **T07** (Flatten) — continue |
| 6 | **T08** (Immutable + Observable) — start | **T07** (Flatten) — finish + **T09** (Nested Collections) — start |
| 7 | **T08** (Immutable + Observable) — finish | **T09** (Nested Collections) — finish |
| 8 | **T10** (Dict↔Object + Tuple) — start | **T11** (Integration Tests) — start: collection type tests |
| 9 | **T10** (Dict↔Object + Tuple) — finish | **T11** (Integration Tests) — flatten tests, edge cases |
| 10 | **T11** (Integration Tests) — benchmarks, polish | **T11** (Integration Tests) — cross-TFM verification, docs audit |

For a **solo developer**, the sprint is achievable in 10 days following the dependency graph top-to-bottom.

---

*Sprint 5 completes collection support, making SmartMapp.Net capable of mapping the full spectrum of .NET collection types. Combined with flattening, dictionary↔object, and tuple↔object mapping, the library now handles the vast majority of real-world mapping scenarios.*
