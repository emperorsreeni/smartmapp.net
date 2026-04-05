using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class InterfaceMappingTests
{
    [Fact]
    public void ResolveConcreteType_ConcreteType_ReturnsSameType()
    {
        var resolver = new InheritanceResolver();
        var materializer = new InterfaceMaterializer(resolver);
        var pair = TypePair.Of<PersonSource, PersonDtoImpl>();

        var result = materializer.ResolveConcreteType(pair);
        result.Should().Be(typeof(PersonDtoImpl));
    }

    [Fact]
    public void ResolveConcreteType_InterfaceWithMaterialize_ReturnsMaterialized()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<PersonSource, IPersonDto>();
        resolver.RegisterMaterializeType(pair, typeof(PersonDtoImpl));

        var materializer = new InterfaceMaterializer(resolver);
        var result = materializer.ResolveConcreteType(pair);
        result.Should().Be(typeof(PersonDtoImpl));
    }

    [Fact]
    public void ResolveConcreteType_InterfaceWithoutMaterialize_ReturnsProxyType()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<PersonSource, IPersonDto>();

        var materializer = new InterfaceMaterializer(resolver);
        var result = materializer.ResolveConcreteType(pair);
        result.Should().Be(typeof(PropertyBackedProxy));
    }

    [Fact]
    public void ResolveConcreteType_AbstractWithMaterialize_ReturnsMaterialized()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<AnimalSource, AbstractAnimal>();
        resolver.RegisterMaterializeType(pair, typeof(ConcreteAnimal));

        var materializer = new InterfaceMaterializer(resolver);
        var result = materializer.ResolveConcreteType(pair);
        result.Should().Be(typeof(ConcreteAnimal));
    }

    [Fact]
    public void ResolveConcreteType_AbstractWithoutMaterialize_Throws()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<AnimalSource, AbstractAnimal>();

        var materializer = new InterfaceMaterializer(resolver);
        var act = () => materializer.ResolveConcreteType(pair);
        act.Should().Throw<MappingCompilationException>()
            .WithMessage("*Cannot map to abstract type*");
    }

    [Fact]
    public void ResolveConcreteType_InvalidMaterializeType_Throws()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<PersonSource, IPersonDto>();
        resolver.RegisterMaterializeType(pair, typeof(SimpleClass)); // Does not implement IPersonDto

        var materializer = new InterfaceMaterializer(resolver);
        var act = () => materializer.ResolveConcreteType(pair);
        act.Should().Throw<MappingCompilationException>()
            .WithMessage("*does not implement*");
    }

    [Fact]
    public void ResolveConcreteType_AbstractMaterializeType_Throws()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<AnimalSource, AbstractAnimal>();
        resolver.RegisterMaterializeType(pair, typeof(AbstractAnimal)); // Still abstract

        var materializer = new InterfaceMaterializer(resolver);
        var act = () => materializer.ResolveConcreteType(pair);
        act.Should().Throw<MappingCompilationException>()
            .WithMessage("*concrete*");
    }

    [Fact]
    public void CreateProxy_ReturnsWorkingProxy()
    {
        var proxy = InterfaceMaterializer.CreateProxy(typeof(IPersonDto));
        proxy.Should().NotBeNull();

        var person = (IPersonDto)proxy;
        person.FirstName = "John";
        person.LastName = "Doe";
        person.Age = 30;

        person.FirstName.Should().Be("John");
        person.LastName.Should().Be("Doe");
        person.Age.Should().Be(30);
    }

    [Fact]
    public void PropertyBackedProxy_ToString_ReturnsPropertyCount()
    {
        var proxy = (IPersonDto)InterfaceMaterializer.CreateProxy(typeof(IPersonDto));
        proxy.FirstName = "Jane";

        var str = proxy.ToString();
        str.Should().Contain("Proxy");
    }

    [Fact]
    public void PropertyBackedProxy_Equals_SameInstanceIsTrue()
    {
        var proxy1 = (IPersonDto)InterfaceMaterializer.CreateProxy(typeof(IPersonDto));
        proxy1.FirstName = "A";
        proxy1.Age = 10;

        // DispatchProxy Equals delegates to PropertyBackedProxy.Invoke
        // which compares internal dictionaries — same instance should always be equal
        proxy1.Equals(proxy1).Should().BeTrue();
    }

    [Fact]
    public void PropertyBackedProxy_GetHashCode_IsConsistent()
    {
        var proxy = (IPersonDto)InterfaceMaterializer.CreateProxy(typeof(IPersonDto));
        proxy.FirstName = "A";
        proxy.Age = 10;

        var hash1 = proxy.GetHashCode();
        var hash2 = proxy.GetHashCode();
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Materialize_FluentApi_SetsMaterializeType()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<PersonSource, IPersonDto>()
            .Materialize<PersonDtoImpl>();

        builder.Bindings[0].MaterializeType.Should().Be(typeof(PersonDtoImpl));
    }
}
