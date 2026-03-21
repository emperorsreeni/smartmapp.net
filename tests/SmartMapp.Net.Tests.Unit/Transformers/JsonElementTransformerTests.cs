using System.Text.Json;
using FluentAssertions;
using SmartMapp.Net.Tests.Unit.TestTypes;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class JsonElementTransformerTests
{
    private readonly MappingScope _scope = new();

    [Fact]
    public void JsonElementToObject_Poco_Deserializes()
    {
        var json = """{"Id":42,"Name":"Test"}""";
        var element = JsonDocument.Parse(json).RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(JsonTestDto), _scope);

        result.Should().NotBeNull();
        var dto = result as JsonTestDto;
        dto!.Id.Should().Be(42);
        dto.Name.Should().Be("Test");
    }

    [Fact]
    public void JsonElementToObject_Number_DeserializesToInt()
    {
        var element = JsonDocument.Parse("42").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(int), _scope);

        result.Should().Be(42);
    }

    [Fact]
    public void JsonElementToObject_String_DeserializesToString()
    {
        var element = JsonDocument.Parse("\"hello\"").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(string), _scope);

        result.Should().Be("hello");
    }

    [Fact]
    public void JsonElementToObject_Null_ReturnsNullForRefType()
    {
        var element = JsonDocument.Parse("null").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(string), _scope);

        result.Should().BeNull();
    }

    [Fact]
    public void JsonElementToObject_Null_ReturnsDefaultForValueType()
    {
        var element = JsonDocument.Parse("null").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(int), _scope);

        result.Should().Be(0);
    }

    [Fact]
    public void ObjectToJsonElement_Poco_Serializes()
    {
        var dto = new JsonTestDto { Id = 7, Name = "Seven" };
        var transformer = new ObjectToJsonElementTransformer();

        var result = transformer.Transform(dto, typeof(JsonTestDto), _scope);

        result.Should().BeOfType<JsonElement>();
        var element = (JsonElement)result;
        element.GetProperty("Id").GetInt32().Should().Be(7);
        element.GetProperty("Name").GetString().Should().Be("Seven");
    }

    [Fact]
    public void ObjectToJsonElement_Null_ReturnsNullElement()
    {
        var transformer = new ObjectToJsonElementTransformer();

        var result = transformer.Transform(null, typeof(JsonTestDto), _scope);

        result.Should().BeOfType<JsonElement>();
        ((JsonElement)result).ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void CanTransform_JsonElementToT_ReturnsTrue()
    {
        var toObj = new JsonElementToObjectTransformer();
        toObj.CanTransform(typeof(JsonElement), typeof(JsonTestDto)).Should().BeTrue();
        toObj.CanTransform(typeof(JsonElement), typeof(JsonElement)).Should().BeFalse();
    }

    [Fact]
    public void CanTransform_TToJsonElement_ReturnsTrue()
    {
        var fromObj = new ObjectToJsonElementTransformer();
        fromObj.CanTransform(typeof(JsonTestDto), typeof(JsonElement)).Should().BeTrue();
        fromObj.CanTransform(typeof(JsonElement), typeof(JsonElement)).Should().BeFalse();
    }

    [Fact]
    public void JsonElementToObject_Boolean_DeserializesToBool()
    {
        var element = JsonDocument.Parse("true").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(bool), _scope);

        result.Should().Be(true);
    }

    [Fact]
    public void JsonElementToObject_Decimal_DeserializesToDecimal()
    {
        var element = JsonDocument.Parse("99.99").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(decimal), _scope);

        result.Should().Be(99.99m);
    }

    [Fact]
    public void JsonElementToObject_Long_DeserializesToLong()
    {
        var element = JsonDocument.Parse("9999999999").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(long), _scope);

        result.Should().Be(9999999999L);
    }

    [Fact]
    public void JsonElementToObject_Array_DeserializesToArray()
    {
        var element = JsonDocument.Parse("[1,2,3]").RootElement;
        var transformer = new JsonElementToObjectTransformer();

        var result = transformer.Transform(element, typeof(int[]), _scope);

        result.Should().BeOfType<int[]>();
        ((int[])result).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }
}
