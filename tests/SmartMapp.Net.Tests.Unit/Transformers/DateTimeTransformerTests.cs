using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class DateTimeTransformerTests
{
    private readonly MappingScope _scope = new();

    [Fact]
    public void DateTimeToDateTimeOffset_Utc_OffsetIsZero()
    {
        var dt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc);
        var transformer = new DateTimeToDateTimeOffsetTransformer();

        var result = transformer.Transform(dt, _scope);

        result.Offset.Should().Be(TimeSpan.Zero);
        result.DateTime.Should().Be(dt);
    }

    [Fact]
    public void DateTimeToDateTimeOffset_Local_HasLocalOffset()
    {
        var dt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Local);
        var transformer = new DateTimeToDateTimeOffsetTransformer();

        var result = transformer.Transform(dt, _scope);

        result.Offset.Should().Be(TimeZoneInfo.Local.GetUtcOffset(dt));
    }

    [Fact]
    public void DateTimeToDateTimeOffset_Unspecified_TreatedAsUtc()
    {
        var dt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Unspecified);
        var transformer = new DateTimeToDateTimeOffsetTransformer();

        var result = transformer.Transform(dt, _scope);

        result.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void DateTimeOffsetToDateTime_ExtractsUtc()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 10, 0, 0, TimeSpan.FromHours(5));
        var transformer = new DateTimeOffsetToDateTimeTransformer();

        var result = transformer.Transform(dto, _scope);

        result.Should().Be(new DateTime(2024, 6, 15, 5, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void DateTimeToDateTimeOffset_MinValue()
    {
        var transformer = new DateTimeToDateTimeOffsetTransformer();

        var result = transformer.Transform(DateTime.MinValue, _scope);

        result.DateTime.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void DateTimeToDateOnly_ExtractsDate()
    {
        var dt = new DateTime(2024, 6, 15, 10, 30, 0);
        var transformer = new DateTimeToDateOnlyTransformer();

        var result = transformer.Transform(dt, _scope);

        result.Should().Be(new DateOnly(2024, 6, 15));
    }

    [Fact]
    public void DateTimeToTimeOnly_ExtractsTime()
    {
        var dt = new DateTime(2024, 6, 15, 10, 30, 45);
        var transformer = new DateTimeToTimeOnlyTransformer();

        var result = transformer.Transform(dt, _scope);

        result.Should().Be(new TimeOnly(10, 30, 45));
    }

    [Fact]
    public void DateOnlyToDateTime_CombinesWithMinTime()
    {
        var d = new DateOnly(2024, 6, 15);
        var transformer = new DateOnlyToDateTimeTransformer();

        var result = transformer.Transform(d, _scope);

        result.Should().Be(new DateTime(2024, 6, 15, 0, 0, 0));
    }

    [Fact]
    public void TimeOnlyToTimeSpan_Converts()
    {
        var t = new TimeOnly(14, 30, 0);
        var transformer = new TimeOnlyToTimeSpanTransformer();

        var result = transformer.Transform(t, _scope);

        result.Should().Be(new TimeSpan(14, 30, 0));
    }

    [Fact]
    public void StringToDateTime_ValidIso_Parses()
    {
        var transformer = new StringToDateTimeTransformer();

        var result = transformer.Transform("2024-06-15T10:30:00Z", _scope);

        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
        result.Day.Should().Be(15);
    }

    [Fact]
    public void StringToDateTime_Invalid_Throws()
    {
        var transformer = new StringToDateTimeTransformer();

        var act = () => transformer.Transform("not-a-date", _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void StringToDateTime_Null_Throws()
    {
        var transformer = new StringToDateTimeTransformer();

        var act = () => transformer.Transform(null!, _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void DateTimeToDateTimeOffset_MaxValue()
    {
        var transformer = new DateTimeToDateTimeOffsetTransformer();

        var result = transformer.Transform(DateTime.MaxValue, _scope);

        result.DateTime.Should().Be(DateTime.MaxValue);
    }
}
