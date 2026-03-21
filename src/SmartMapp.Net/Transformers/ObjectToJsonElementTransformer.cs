using System.Text.Json;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that converts an arbitrary type <c>T</c> to <see cref="JsonElement"/>
/// using <see cref="JsonSerializer"/>.
/// </summary>
public sealed class ObjectToJsonElementTransformer : ITypeTransformer
{
    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => targetType == typeof(JsonElement) && originType != typeof(JsonElement);

    /// <summary>
    /// Serializes the origin object to a <see cref="JsonElement"/>.
    /// </summary>
    /// <param name="origin">The object to serialize.</param>
    /// <param name="originType">The origin type.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The serialized <see cref="JsonElement"/>.</returns>
    public object Transform(object? origin, Type originType, MappingScope scope)
    {
        if (origin is null)
        {
            return JsonDocument.Parse("null").RootElement;
        }

        var options = scope.TryGetService<JsonSerializerOptions>();

        try
        {
            var json = JsonSerializer.Serialize(origin, originType, options ?? new JsonSerializerOptions());
            return JsonDocument.Parse(json).RootElement;
        }
        catch (JsonException ex)
        {
            throw new TransformationException(
                $"Cannot serialize {originType.Name} to JsonElement.",
                origin, originType, typeof(JsonElement), ex);
        }
    }
}
