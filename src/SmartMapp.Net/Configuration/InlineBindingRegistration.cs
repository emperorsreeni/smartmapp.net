using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Configuration;

/// <summary>
/// Internal record capturing an inline <c>options.Bind&lt;S,D&gt;(rule => ...)</c> registration.
/// Executed against the shared <see cref="IBlueprintBuilder"/> during <c>SculptorBuilder.Forge()</c>.
/// </summary>
internal sealed class InlineBindingRegistration
{
    internal TypePair TypePair { get; }
    internal Action<IBlueprintBuilder> Apply { get; }

    internal InlineBindingRegistration(TypePair typePair, Action<IBlueprintBuilder> apply)
    {
        TypePair = typePair;
        Apply = apply;
    }
}
