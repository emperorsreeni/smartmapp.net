using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Configuration;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Immutable snapshot of everything a <see cref="ISculptorBuilder"/> accumulated by the time
/// <c>Forge()</c> is called. Consumed by <see cref="SculptorBuildPipeline"/>.
/// </summary>
internal sealed class SculptorBuildInputs
{
    internal required SculptorOptions Options { get; init; }
    internal required IReadOnlyList<Assembly> Assemblies { get; init; }
    internal required IReadOnlyList<Type> BlueprintTypes { get; init; }
    internal required IReadOnlyList<MappingBlueprint> BlueprintInstances { get; init; }
    internal required IReadOnlyList<Type> TransformerTypes { get; init; }
    internal required IReadOnlyList<InlineBindingRegistration> InlineBindings { get; init; }
    internal required IBlueprintBuilder BlueprintBuilder { get; init; }
    internal required BlueprintBuilder BlueprintBuilderImpl { get; init; }
}
