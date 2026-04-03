using FluentAssertions;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Compilation;

public class ComplexTypeDetectorTests
{
    [Theory]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(bool), false)]
    [InlineData(typeof(double), false)]
    [InlineData(typeof(byte), false)]
    [InlineData(typeof(char), false)]
    public void Primitives_AreNotComplex(Type type, bool expected)
    {
        ComplexTypeDetector.IsComplexType(type).Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(DateTimeOffset), false)]
    [InlineData(typeof(TimeSpan), false)]
    [InlineData(typeof(Guid), false)]
    [InlineData(typeof(decimal), false)]
    [InlineData(typeof(Uri), false)]
    public void SimpleTypes_AreNotComplex(Type type, bool expected)
    {
        ComplexTypeDetector.IsComplexType(type).Should().Be(expected);
    }

    [Fact]
    public void Enum_IsNotComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(DayOfWeek)).Should().BeFalse();
    }

    [Fact]
    public void NullableEnum_IsNotComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(DayOfWeek?)).Should().BeFalse();
    }

    [Fact]
    public void NullableInt_IsNotComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(int?)).Should().BeFalse();
    }

    [Fact]
    public void Collection_IsNotComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(List<int>)).Should().BeFalse();
        ComplexTypeDetector.IsComplexType(typeof(int[])).Should().BeFalse();
        ComplexTypeDetector.IsComplexType(typeof(Dictionary<string, int>)).Should().BeFalse();
    }

    [Fact]
    public void Class_IsComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(FlatOrder)).Should().BeTrue();
        ComplexTypeDetector.IsComplexType(typeof(NestedCustomer)).Should().BeTrue();
    }

    [Fact]
    public void Record_IsComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(RecordOrderDto)).Should().BeTrue();
    }

    [Fact]
    public void PlainStruct_IsComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(PointStruct)).Should().BeTrue();
    }

    [Fact]
    public void ByteArray_IsNotComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(byte[])).Should().BeFalse();
    }

    [Fact]
    public void Interface_IsNotComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(IHasName)).Should().BeFalse();
    }

    [Fact]
    public void Abstract_IsNotComplex()
    {
        ComplexTypeDetector.IsComplexType(typeof(BaseEntity)).Should().BeFalse();
    }
}
