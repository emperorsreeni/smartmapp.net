using System.Text.Json;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that converts <see cref="JsonElement"/> to an arbitrary type <c>T</c>
/// using <see cref="JsonSerializer"/>.
/// </summary>
public sealed class JsonElementToObjectTransformer : ITypeTransformer
{
    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(JsonElement) && targetType != typeof(JsonElement);

    /// <summary>
    /// Deserializes a <see cref="JsonElement"/> to the specified target type.
    /// </summary>
    /// <param name="origin">The <see cref="JsonElement"/> to deserialize.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The deserialized object.</returns>
    public object? Transform(object origin, Type targetType, MappingScope scope)
    {
        var element = (JsonElement)origin;

        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
        {
            if (targetType.IsValueType)
                return Activator.CreateInstance(targetType);
            return null;
        }

        // Try to get JsonSerializerOptions from DI
        var options = scope.TryGetService<JsonSerializerOptions>();

        try
        {
            var rawText = element.GetRawText();
            return JsonSerializer.Deserialize(rawText, targetType, options ?? new JsonSerializerOptions());
        }
        catch (JsonException ex)
        {
            throw new TransformationException(
                $"Cannot deserialize JsonElement to {targetType.Name}.",
                origin, typeof(JsonElement), targetType, ex);
        }
    }
}
