using FluentAssertions;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Collections;

public sealed class ValueTupleMappingTests
{
    [Fact]
    public void IsValueTuple_DetectsCorrectly()
    {
        ValueTupleMapper.IsValueTuple(typeof((int, string))).Should().BeTrue();
        ValueTupleMapper.IsValueTuple(typeof((int, string, int))).Should().BeTrue();
        ValueTupleMapper.IsValueTuple(typeof(int)).Should().BeFalse();
        ValueTupleMapper.IsValueTuple(typeof(string)).Should().BeFalse();
        ValueTupleMapper.IsValueTuple(typeof(Tuple<int, string>)).Should().BeFalse();
    }

    [Fact]
    public void IsValueTupleObjectMapping_DetectsCorrectly()
    {
        ValueTupleMapper.IsValueTupleObjectMapping(
            typeof((int, string, int)), typeof(PersonForTuple)).Should().BeTrue();

        ValueTupleMapper.IsValueTupleObjectMapping(
            typeof(PersonForTuple), typeof((int, string, int))).Should().BeTrue();

        ValueTupleMapper.IsValueTupleObjectMapping(
            typeof(PersonForTuple), typeof(PersonForTuple)).Should().BeFalse();

        ValueTupleMapper.IsValueTupleObjectMapping(
            typeof((int, string)), typeof((int, string))).Should().BeFalse();
    }

    [Fact]
    public void TupleToObject_MapsFieldsByPosition()
    {
        var tupleType = typeof((int, string, int));
        var sourceParam = System.Linq.Expressions.Expression.Parameter(tupleType, "source");
        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");

        var body = ValueTupleMapper.BuildTupleToObject(sourceParam, typeof(PersonForTuple), scopeParam);
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<(int, string, int), MappingScope, PersonForTuple>>(
            body, sourceParam, scopeParam);
        var compiled = lambda.Compile();

        var result = compiled((42, "Alice", 30), new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be("Alice");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void ObjectToTuple_MapsPropertiesByPosition()
    {
        var tupleType = typeof((int, string, int));
        var sourceParam = System.Linq.Expressions.Expression.Parameter(typeof(PersonForTuple), "source");
        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");

        var body = ValueTupleMapper.BuildObjectToTuple(sourceParam, tupleType, scopeParam);
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<PersonForTuple, MappingScope, (int, string, int)>>(
            body, sourceParam, scopeParam);
        var compiled = lambda.Compile();

        var person = new PersonForTuple { Id = 7, Name = "Bob", Age = 25 };
        var result = compiled(person, new MappingScope());

        result.Item1.Should().Be(7);
        result.Item2.Should().Be("Bob");
        result.Item3.Should().Be(25);
    }

    [Fact]
    public void UnnamedTuple_MapsToObjectByPosition()
    {
        var tupleType = typeof((int, string));
        var sourceParam = System.Linq.Expressions.Expression.Parameter(tupleType, "source");
        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");

        var body = ValueTupleMapper.BuildTupleToObject(sourceParam, typeof(PersonForUnnamedTuple), scopeParam);
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<(int, string), MappingScope, PersonForUnnamedTuple>>(
            body, sourceParam, scopeParam);
        var compiled = lambda.Compile();

        var result = compiled((99, "Eve"), new MappingScope());

        result.Id.Should().Be(99);
        result.Name.Should().Be("Eve");
    }

    [Fact]
    public void ValueTuple_IsValueType_CannotBeNull()
    {
        // ValueTuple<...> is a struct — null source is not possible at the CLR level.
        // This test documents that the IsValueTuple check correctly identifies struct tuples,
        // confirming the S5-T10 AC "Null dictionary/tuple → null target" is N/A for tuples.
        var tupleType = typeof((int, string, int));
        tupleType.IsValueType.Should().BeTrue();
        ValueTupleMapper.IsValueTuple(tupleType).Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_TupleToObjectAndBack_PreservesValues()
    {
        var tupleType = typeof((int, string, int));

        // Tuple → Object
        var sp1 = System.Linq.Expressions.Expression.Parameter(tupleType, "s");
        var sc1 = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var toObj = System.Linq.Expressions.Expression.Lambda<Func<(int, string, int), MappingScope, PersonForTuple>>(
            ValueTupleMapper.BuildTupleToObject(sp1, typeof(PersonForTuple), sc1), sp1, sc1).Compile();

        // Object → Tuple
        var sp2 = System.Linq.Expressions.Expression.Parameter(typeof(PersonForTuple), "s");
        var sc2 = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var toTuple = System.Linq.Expressions.Expression.Lambda<Func<PersonForTuple, MappingScope, (int, string, int)>>(
            ValueTupleMapper.BuildObjectToTuple(sp2, tupleType, sc2), sp2, sc2).Compile();

        var scope = new MappingScope();
        var obj = toObj((123, "Charlie", 40), scope);
        var roundTripped = toTuple(obj, scope);

        roundTripped.Item1.Should().Be(123);
        roundTripped.Item2.Should().Be("Charlie");
        roundTripped.Item3.Should().Be(40);
    }
}
