using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class NullableTransformerTests
{
    private readonly MappingScope _scope = new();

    // ── NullableWrap ─────────────────────────────────────────────────

    [Fact]
    public void Wrap_CanTransform_IntToNullableInt_ReturnsTrue()
    {
        var transformer = new NullableWrapTransformer();
        transformer.CanTransform(typeof(int), typeof(int?)).Should().BeTrue();
    }

    [Fact]
    public void Wrap_CanTransform_StringToNullableInt_ReturnsFalse()
    {
        var transformer = new NullableWrapTransformer();
        transformer.CanTransform(typeof(string), typeof(int?)).Should().BeFalse();
    }

    [Fact]
    public void Wrap_CanTransform_DateTimeToNullableDateTime_ReturnsTrue()
    {
        var transformer = new NullableWrapTransformer();
        transformer.CanTransform(typeof(DateTime), typeof(DateTime?)).Should().BeTrue();
    }

    [Fact]
    public void Wrap_IntToNullableInt_WrapsValue()
    {
        var transformer = new NullableWrapTransformer();

        var result = transformer.Transform(42, _scope);

        result.Should().Be(42);
    }

    [Fact]
    public void Wrap_DateTimeToNullableDateTime_WrapsValue()
    {
        var transformer = new NullableWrapTransformer();
        var dt = new DateTime(2024, 1, 1);

        var result = transformer.Transform(dt, _scope);

        result.Should().Be(dt);
    }

    // ── NullableUnwrap ───────────────────────────────────────────────

    [Fact]
    public void Unwrap_CanTransform_NullableIntToInt_ReturnsTrue()
    {
        var transformer = new NullableUnwrapTransformer();
        transformer.CanTransform(typeof(int?), typeof(int)).Should().BeTrue();
    }

    [Fact]
    public void Unwrap_CanTransform_IntToInt_ReturnsFalse()
    {
        var transformer = new NullableUnwrapTransformer();
        transformer.CanTransform(typeof(int), typeof(int)).Should().BeFalse();
    }

    [Fact]
    public void Unwrap_NullableIntWithValue_ReturnsValue()
    {
        var transformer = new NullableUnwrapTransformer();
        int? value = 42;

        var result = transformer.Transform(value, typeof(int), _scope);

        result.Should().Be(42);
    }

    [Fact]
    public void Unwrap_NullableInt_Null_ReturnsDefault()
    {
        var transformer = new NullableUnwrapTransformer();

        var result = transformer.Transform(null, typeof(int), _scope);

        result.Should().Be(0);
    }

    [Fact]
    public void Unwrap_NullableDateTime_Null_ReturnsDefault()
    {
        var transformer = new NullableUnwrapTransformer();

        var result = transformer.Transform(null, typeof(DateTime), _scope);

        result.Should().Be(default(DateTime));
    }

    [Fact]
    public void Unwrap_NullableDateTimeWithValue_ReturnsValue()
    {
        var transformer = new NullableUnwrapTransformer();
        DateTime? dt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        var result = transformer.Transform(dt, typeof(DateTime), _scope);

        result.Should().Be(new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc));
    }
}
