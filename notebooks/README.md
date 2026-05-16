# SmartMapp.Net — Interactive Notebooks

Nine [Polyglot Notebooks](https://github.com/dotnet/interactive) — one per v1.0 sprint plus a
cross-cutting regression guard — that exercise the SmartMapp.Net feature surface end-to-end.

Open any `.ipynb` file in **VS Code** with the [.NET Interactive Notebooks](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode)
extension installed and click **Run All** to execute top-to-bottom.

## Prerequisites

| Dependency                                                                 | Purpose                                                                    |
| -------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| .NET SDK **10.0.100** (pinned by `global.json`)                            | Runs the notebook kernel and compiles the referenced assemblies.           |
| VS Code + **Polyglot Notebooks** / **.NET Interactive Notebooks** extension | Hosts the kernel. Jupyter with `.net-csharp` kernel works too.             |
| Internet access on first run                                                | Resolves the `#r "nuget: …"` cells (EF Core InMemory, Hosting, Logging).   |

Before running the notebooks for the first time, build the repo once in Release:

```pwsh
dotnet build --configuration Release
```

The notebooks reference the Release-built DLLs via relative `#r` paths
(`../src/SmartMapp.Net/bin/Release/net10.0/SmartMapp.Net.dll`), so a missing
Release build will surface as a "file not found" error on the first `#r` line.

## Per-Sprint Notebook Catalogue

| # | Notebook                                                | Sprint spec                                                               | Headline features                                                                                                        |
| - | ------------------------------------------------------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| 1 | [`sprint-01-core-api.ipynb`](sprint-01-core-api.ipynb)  | [S1-T00..T10](../../litemapper/docs/requirements/sprint-1-tasks.md)       | `TypePair`, `SculptorBuilder` → `Forge()`, `ISculptor.Map`, `IMapper<S,D>`, `Blueprint` + `PropertyLink`, `MappingScope`. |
| 2 | [`sprint-02-conventions.ipynb`](sprint-02-conventions.ipynb) | [S2-T00..T11](../../litemapper/docs/requirements/sprint-2-tasks.md)  | Exact-name, flattening, unflattening, prefix-dropping, case-normalization, method-to-property conventions.                |
| 3 | [`sprint-03-type-transformers.ipynb`](sprint-03-type-transformers.ipynb) | [S3-T00..T13](../../litemapper/docs/requirements/sprint-3-tasks.md) | Built-in transformers (`Parsable`, `ToString`, `DateTime`, `Enum`, `Base64`) + custom `ITypeTransformer<TFrom, TTo>`.     |
| 4 | [`sprint-04-construction-and-graph.ipynb`](sprint-04-construction-and-graph.ipynb) | [S4-T00..T10](../../litemapper/docs/requirements/sprint-4-tasks.md) | Records / init-only construction, `BuildWith(factory)`, recursive nested mapping, `TrackReferences()`, `DepthLimit(n)`.   |
| 5 | [`sprint-05-collections.ipynb`](sprint-05-collections.ipynb) | [S5-T00..T11](../../litemapper/docs/requirements/sprint-5-tasks.md)    | `MapAll`, arrays / `List<T>` / `HashSet<T>`, `Dictionary<K,V>`, `ImmutableList<T>` / `ImmutableArray<T>`, nested collections. |
| 6 | [`sprint-06-polymorphism-and-blueprints.ipynb`](sprint-06-polymorphism-and-blueprints.ipynb) | [S6-T00..T07](../../litemapper/docs/requirements/sprint-6-tasks.md) | `ExtendWith<,>`, `Materialize<TConcrete>()`, `InheritFrom<,>`, reusable `MappingBlueprint` classes, full fluent chain.    |
| 7 | [`sprint-07-attributes-validation-diagnostics.ipynb`](sprint-07-attributes-validation-diagnostics.ipynb) | [S7-T00..T12](../../litemapper/docs/requirements/sprint-7-tasks.md) | `[MappedBy<T>]` / `[Unmapped]` / `[LinkedFrom]`, `Inspect<S,D>()`, `MappingAtlas` + DOT, `Validate()` + `StrictMode`.       |
| 8 | [`sprint-08-di-projection-compose.ipynb`](sprint-08-di-projection-compose.ipynb) | [S8-T00..T12](../../litemapper/docs/requirements/sprint-8-tasks.md) | `AddSculptor`, `IMapper<>` DI, `IValueProvider<,,>` DI-resolved, env-aware `ValidateOnStartup`, `SelectAs<T>`, `MapTo<T>`, `Compose<T>`. |
| — | [`99-acceptance-tests.ipynb`](99-acceptance-tests.ipynb) | Cross-cutting                                                             | Assertion-driven regression guard with ✅/❌ helper — throws on the summary cell if any Sprint 8 acceptance behaviour regresses. |

## Reading order

For a **learner** — follow the numeric order (Sprint 1 → Sprint 8). Each notebook builds on
the prior sprint's scaffolding.

For a **returning user** — jump straight to the notebook whose sprint scope matches your
question (e.g. *"how do projections work?"* → `sprint-08`, *"how does `[MappedBy<T>]`
work?"* → `sprint-07`).

For a **CI / post-refactor check** — run `99-acceptance-tests.ipynb` alone. It's independent
of the per-sprint notebooks and will throw loudly on regressions.

## Troubleshooting

- **"Could not find assembly … SmartMapp.Net.dll"** — run `dotnet build --configuration Release` at the repo root first. The notebooks pin Release `net10.0` outputs.
- **NuGet restore hangs on `#r "nuget: …"`** — first-run cost; the kernel downloads transitive dependencies into `%USERPROFILE%\.nuget\packages`. Subsequent runs are instant.
- **"The type or namespace name 'DbContext' could not be found"** — the Sprint 8 notebook has an explicit `#r "nuget: Microsoft.EntityFrameworkCore.InMemory, …"` cell; make sure that cell ran successfully before the model-declaration cell.
- **Kernel hangs on a cell** — the kernel state is per-notebook; use **Restart Kernel** (command palette → "Polyglot Notebook: Restart the current notebook's kernel") and re-run from the top.
- **"ISculptorConfiguration.GetAllBlueprints() is inaccessible"** — the `ISculptorConfiguration` interface has explicit implementations; cast the sculptor first: `((ISculptorConfiguration)sculptor).GetAllBlueprints()`.

## Extending

Every notebook begins with a **Setup** cell that loads the SmartMapp.Net DLL(s) plus any
NuGet dependencies. Copy that cell into a new `.ipynb` file to start experimenting — the
rest of the repo's public API is immediately available to the kernel.
