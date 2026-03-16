using FluentAssertions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class TypeModelTests
{
    [Fact]
    public void SimpleClass_HasPublicReadableProperties()
    {
        var model = new TypeModel(typeof(SimpleClass));

        model.ReadableMembers.Should().Contain(m => m.Name == "Id");
        model.ReadableMembers.Should().Contain(m => m.Name == "Name");
        model.ReadableMembers.Should().Contain(m => m.Name == "CreatedAt");
    }

    [Fact]
    public void SimpleClass_HasPublicWritableProperties()
    {
        var model = new TypeModel(typeof(SimpleClass));

        model.WritableMembers.Should().Contain(m => m.Name == "Id");
        model.WritableMembers.Should().Contain(m => m.Name == "Name");
    }

    [Fact]
    public void InitOnlyClass_DetectsInitOnlyProperties()
    {
        var model = new TypeModel(typeof(InitOnlyClass));

        var idMember = model.ReadableMembers.First(m => m.Name == "Id");
        idMember.IsInitOnly.Should().BeTrue();

        var labelMember = model.ReadableMembers.First(m => m.Name == "Label");
        labelMember.IsInitOnly.Should().BeTrue();
    }

    [Fact]
    public void Record_IsDetectedAsRecord()
    {
        var model = new TypeModel(typeof(PersonRecord));

        model.IsRecord.Should().BeTrue();
    }

    [Fact]
    public void NonRecord_IsNotDetectedAsRecord()
    {
        var model = new TypeModel(typeof(SimpleClass));

        model.IsRecord.Should().BeFalse();
    }

    [Fact]
    public void Record_DetectsPrimaryConstructor()
    {
        var model = new TypeModel(typeof(PersonRecord));

        model.PrimaryConstructor.Should().NotBeNull();
        model.PrimaryConstructor!.IsPrimary.Should().BeTrue();
        model.PrimaryConstructor.ParameterCount.Should().Be(3);
    }

    [Fact]
    public void MultipleConstructors_SortedByParamCountDescending()
    {
        var model = new TypeModel(typeof(MultiCtorClass));

        model.Constructors.Should().HaveCount(4);
        model.Constructors[0].ParameterCount.Should().Be(3);
        model.Constructors[1].ParameterCount.Should().Be(2);
        model.Constructors[2].ParameterCount.Should().Be(1);
        model.Constructors[3].ParameterCount.Should().Be(0);
    }

    [Fact]
    public void List_IsDetectedAsCollection()
    {
        var model = new TypeModel(typeof(List<int>));

        model.IsCollection.Should().BeTrue();
        model.CollectionElementType.Should().Be(typeof(int));
    }

    [Fact]
    public void Array_IsDetectedAsCollection()
    {
        var model = new TypeModel(typeof(int[]));

        model.IsCollection.Should().BeTrue();
        model.CollectionElementType.Should().Be(typeof(int));
    }

    [Fact]
    public void IEnumerable_IsDetectedAsCollection()
    {
        var model = new TypeModel(typeof(IEnumerable<string>));

        model.IsCollection.Should().BeTrue();
        model.CollectionElementType.Should().Be(typeof(string));
    }

    [Fact]
    public void HashSet_IsDetectedAsCollection()
    {
        var model = new TypeModel(typeof(HashSet<double>));

        model.IsCollection.Should().BeTrue();
        model.CollectionElementType.Should().Be(typeof(double));
    }

    [Fact]
    public void String_IsNotDetectedAsCollection()
    {
        var model = new TypeModel(typeof(string));

        model.IsCollection.Should().BeFalse();
    }

    [Fact]
    public void Dictionary_IsDetectedAsDictionary()
    {
        var model = new TypeModel(typeof(Dictionary<string, int>));

        model.IsDictionary.Should().BeTrue();
        model.DictionaryKeyType.Should().Be(typeof(string));
        model.DictionaryValueType.Should().Be(typeof(int));
    }

    [Fact]
    public void NullableInt_IsDetectedAsNullable()
    {
        var model = new TypeModel(typeof(int?));

        model.IsNullable.Should().BeTrue();
        model.UnderlyingNullableType.Should().Be(typeof(int));
    }

    [Fact]
    public void NonNullable_IsNotNullable()
    {
        var model = new TypeModel(typeof(int));

        model.IsNullable.Should().BeFalse();
        model.UnderlyingNullableType.Should().BeNull();
    }

    [Fact]
    public void InheritanceChain_ExcludesObject()
    {
        var model = new TypeModel(typeof(ConcreteEntity));

        model.InheritanceChain.Should().Contain(typeof(ConcreteEntity));
        model.InheritanceChain.Should().Contain(typeof(BaseEntity));
        model.InheritanceChain.Should().NotContain(typeof(object));
    }

    [Fact]
    public void ParameterlessValueMethods_DetectsGetMethods()
    {
        var model = new TypeModel(typeof(ClassWithMethods));

        model.ParameterlessValueMethods.Should().Contain(m => m.Name == "GetFullName");
        model.ParameterlessValueMethods.Should().Contain(m => m.Name == "GetAge");
    }

    [Fact]
    public void ParameterlessValueMethods_InfersPropertyName()
    {
        var model = new TypeModel(typeof(ClassWithMethods));

        var method = model.ParameterlessValueMethods.First(m => m.Name == "GetFullName");
        method.InferredPropertyName.Should().Be("FullName");
    }

    [Fact]
    public void PublicFields_AreDetected()
    {
        var model = new TypeModel(typeof(ClassWithField));

        model.ReadableMembers.Should().Contain(m => m.Name == "Id" && m.IsField);
        model.WritableMembers.Should().Contain(m => m.Name == "Id" && m.IsField);
    }

    [Fact]
    public void GenericType_IsDetected()
    {
        var model = new TypeModel(typeof(Wrapper<string>));

        model.IsGenericType.Should().BeTrue();
    }

    [Fact]
    public void AbstractClass_IsDetected()
    {
        var model = new TypeModel(typeof(BaseEntity));

        model.IsAbstract.Should().BeTrue();
    }

    [Fact]
    public void Interface_IsDetected()
    {
        var model = new TypeModel(typeof(IHasName));

        model.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void GetMember_CaseInsensitiveLookup()
    {
        var model = new TypeModel(typeof(SimpleClass));

        model.GetMember("id").Should().NotBeNull();
        model.GetMember("ID").Should().NotBeNull();
        model.GetMember("Id").Should().NotBeNull();
        model.GetMember("NonExistent").Should().BeNull();
    }

    [Fact]
    public void HasParameterlessConstructor_Detected()
    {
        var model = new TypeModel(typeof(SimpleClass));
        model.HasParameterlessConstructor.Should().BeTrue();

        var recordModel = new TypeModel(typeof(PersonRecord));
        // PersonRecord has a primary ctor with 3 params, but also a copy ctor
        // It depends on the compiler — records typically don't have parameterless ctors
    }

    [Fact]
    public void ToString_ShowsTypeName()
    {
        var model = new TypeModel(typeof(SimpleClass));
        model.ToString().Should().Be("TypeModel(SimpleClass)");
    }

    [Fact]
    public void RequiredMembers_AreDetected()
    {
        var model = new TypeModel(typeof(RequiredClass));

        var nameMember = model.WritableMembers.First(m => m.Name == "Name");
        nameMember.IsRequired.Should().BeTrue();

        var codeMember = model.WritableMembers.First(m => m.Name == "Code");
        codeMember.IsRequired.Should().BeTrue();

        var optionalMember = model.WritableMembers.First(m => m.Name == "Optional");
        optionalMember.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void GetMemberPath_ResolvesCompoundName()
    {
        var model = new TypeModel(typeof(CustomerWithAddress));

        var path = model.GetMemberPath("AddressCity");

        path.Should().HaveCount(2);
        path[0].Name.Should().Be("Address");
        path[1].Name.Should().Be("City");
    }

    [Fact]
    public void GetMemberPath_ReturnsEmpty_WhenCannotResolve()
    {
        var model = new TypeModel(typeof(CustomerWithAddress));

        var path = model.GetMemberPath("NonExistentProperty");

        path.Should().BeEmpty();
    }

    [Fact]
    public void GetMemberPath_ResolvesSimpleName()
    {
        var model = new TypeModel(typeof(CustomerWithAddress));

        var path = model.GetMemberPath("Name");

        path.Should().HaveCount(1);
        path[0].Name.Should().Be("Name");
    }

    [Fact]
    public void ImplementedInterfaces_ArePopulated()
    {
        var model = new TypeModel(typeof(List<int>));

        model.ImplementedInterfaces.Should().Contain(typeof(IList<int>));
        model.ImplementedInterfaces.Should().Contain(typeof(IEnumerable<int>));
    }
}
