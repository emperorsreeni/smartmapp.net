using FluentAssertions;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Collections;

public sealed class DictionaryObjectMappingTests
{
    [Fact]
    public void DictionaryToObject_MapsAllProperties()
    {
        var dict = new Dictionary<string, object>
        {
            ["Id"] = 42,
            ["Name"] = "Alice",
            ["Age"] = 30,
        };

        var expr = DictionaryObjectMapper.BuildDictionaryToObject(
            System.Linq.Expressions.Expression.Parameter(typeof(Dictionary<string, object>), "source"),
            typeof(PersonForDict),
            System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope"));

        expr.Should().NotBeNull();
        expr.Type.Should().Be(typeof(PersonForDict));
    }

    [Fact]
    public void DictionaryToObject_CompiledDelegate_MapsCorrectly()
    {
        var dict = new Dictionary<string, object>
        {
            ["Id"] = 42,
            ["Name"] = "Alice",
            ["Age"] = 30,
        };

        var sourceParam = System.Linq.Expressions.Expression.Parameter(typeof(Dictionary<string, object>), "source");
        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");

        var body = DictionaryObjectMapper.BuildDictionaryToObject(sourceParam, typeof(PersonForDict), scopeParam);
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<Dictionary<string, object>, MappingScope, PersonForDict>>(
            body, sourceParam, scopeParam);
        var compiled = lambda.Compile();

        var result = compiled(dict, new MappingScope());

        result.Id.Should().Be(42);
        result.Name.Should().Be("Alice");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void DictionaryToObject_MissingKey_LeavesDefault()
    {
        var dict = new Dictionary<string, object>
        {
            ["Id"] = 99,
        };

        var sourceParam = System.Linq.Expressions.Expression.Parameter(typeof(Dictionary<string, object>), "source");
        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");

        var body = DictionaryObjectMapper.BuildDictionaryToObject(sourceParam, typeof(PersonForDict), scopeParam);
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<Dictionary<string, object>, MappingScope, PersonForDict>>(
            body, sourceParam, scopeParam);
        var compiled = lambda.Compile();

        var result = compiled(dict, new MappingScope());

        result.Id.Should().Be(99);
        result.Name.Should().Be(string.Empty); // default from class init
        result.Age.Should().Be(0);
    }

    [Fact]
    public void ObjectToDictionary_MapsAllProperties()
    {
        var person = new PersonForDict { Id = 7, Name = "Bob", Age = 25 };

        var sourceParam = System.Linq.Expressions.Expression.Parameter(typeof(PersonForDict), "source");
        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");

        var body = DictionaryObjectMapper.BuildObjectToDictionary(sourceParam, typeof(PersonForDict), scopeParam);
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<PersonForDict, MappingScope, Dictionary<string, object>>>(
            body, sourceParam, scopeParam);
        var compiled = lambda.Compile();

        var result = compiled(person, new MappingScope());

        result.Should().ContainKey("Id").WhoseValue.Should().Be(7);
        result.Should().ContainKey("Name").WhoseValue.Should().Be("Bob");
        result.Should().ContainKey("Age").WhoseValue.Should().Be(25);
    }

    [Fact]
    public void RoundTrip_DictToObjectAndBack_PreservesValues()
    {
        var original = new Dictionary<string, object>
        {
            ["Id"] = 123,
            ["Name"] = "Charlie",
            ["Age"] = 40,
        };

        // Dict → Object
        var sp1 = System.Linq.Expressions.Expression.Parameter(typeof(Dictionary<string, object>), "s");
        var sc1 = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var toObj = System.Linq.Expressions.Expression.Lambda<Func<Dictionary<string, object>, MappingScope, PersonForDict>>(
            DictionaryObjectMapper.BuildDictionaryToObject(sp1, typeof(PersonForDict), sc1), sp1, sc1).Compile();

        // Object → Dict
        var sp2 = System.Linq.Expressions.Expression.Parameter(typeof(PersonForDict), "s");
        var sc2 = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var toDict = System.Linq.Expressions.Expression.Lambda<Func<PersonForDict, MappingScope, Dictionary<string, object>>>(
            DictionaryObjectMapper.BuildObjectToDictionary(sp2, typeof(PersonForDict), sc2), sp2, sc2).Compile();

        var scope = new MappingScope();
        var obj = toObj(original, scope);
        var roundTripped = toDict(obj, scope);

        roundTripped["Id"].Should().Be(123);
        roundTripped["Name"].Should().Be("Charlie");
        roundTripped["Age"].Should().Be(40);
    }

    [Fact]
    public void IsDictionaryObjectMapping_DetectsCorrectly()
    {
        DictionaryObjectMapper.IsDictionaryObjectMapping(
            typeof(Dictionary<string, object>), typeof(PersonForDict)).Should().BeTrue();

        DictionaryObjectMapper.IsDictionaryObjectMapping(
            typeof(PersonForDict), typeof(Dictionary<string, object>)).Should().BeTrue();

        DictionaryObjectMapper.IsDictionaryObjectMapping(
            typeof(PersonForDict), typeof(PersonForDict)).Should().BeFalse();

        DictionaryObjectMapper.IsDictionaryObjectMapping(
            typeof(Dictionary<string, object>), typeof(Dictionary<string, object>)).Should().BeFalse();
    }
}
