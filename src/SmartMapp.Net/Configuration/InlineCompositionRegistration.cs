using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Configuration;

/// <summary>
/// Internal record capturing an inline <c>options.Compose&lt;TTarget&gt;()</c> registration.
/// Executed against the shared <see cref="IBlueprintBuilder"/> during
/// <c>SculptorBuilder.Forge()</c> so the rule ends up in the builder's composition
/// accumulator, materialised into a <see cref="Composition.CompositionBlueprint"/> by the
/// forge pipeline per spec §S8-T08.
/// </summary>
internal sealed class InlineCompositionRegistration
{
    internal Type TargetType { get; }
    internal Action<IBlueprintBuilder> Apply { get; }

    internal InlineCompositionRegistration(Type targetType, Action<IBlueprintBuilder> apply)
    {
        TargetType = targetType;
        Apply = apply;
    }
}
