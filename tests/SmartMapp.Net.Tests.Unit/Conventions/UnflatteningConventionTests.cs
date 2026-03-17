using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class UnflatteningConventionTests
{
    private readonly TypeModelCache _cache = new();
    private readonly UnflatteningConvention _convention;

    public UnflatteningConventionTests()
    {
        _convention = new UnflatteningConvention(_cache);
    }

    [Fact]
    public void Priority_Is350()
    {
        _convention.Priority.Should().Be(350);
    }

    [Fact]
    public void TryLink_OneLevel_UnflattensCustomer()
    {
        var origin = _cache.GetOrAdd<UnflatSource>();
        var target = typeof(UnflatTarget).GetProperty(nameof(UnflatTarget.Customer))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<UnflatteningValueProvider>();

        var source = new UnflatSource
        {
            CustomerFirstName = "Alice",
            CustomerLastName = "Smith",
        };

        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().NotBeNull();
        var customer = result.Should().BeOfType<FlatteningCustomer>().Subject;
        customer.FirstName.Should().Be("Alice");
        customer.LastName.Should().Be("Smith");
    }

    [Fact]
    public void TryLink_TwoLevel_UnflattensCustomerAddress()
    {
        var origin = _cache.GetOrAdd<UnflatSource>();
        var target = typeof(UnflatTarget).GetProperty(nameof(UnflatTarget.Customer))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();

        var source = new UnflatSource
        {
            CustomerFirstName = "Bob",
            CustomerAddressCity = "Seattle",
        };

        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().NotBeNull();
        var customer = result.Should().BeOfType<FlatteningCustomer>().Subject;
        customer.FirstName.Should().Be("Bob");
        customer.Address.Should().NotBeNull();
        customer.Address.City.Should().Be("Seattle");
    }

    [Fact]
    public void TryLink_SimpleType_ReturnsFalse()
    {
        // "Id" is an int — a simple type, not unflattenable
        var origin = _cache.GetOrAdd<UnflatSource>();
        var target = typeof(UnflatTarget).GetProperty(nameof(UnflatTarget.Id))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_NoMatchingOriginPrefix_ReturnsFalse()
    {
        var origin = _cache.GetOrAdd<ExactSource>();
        var target = typeof(UnflatTarget).GetProperty(nameof(UnflatTarget.Customer))!;

        // ExactSource has no "Customer*" properties
        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
    }

    [Fact]
    public void TryLink_CreatesIntermediateObjects()
    {
        var origin = _cache.GetOrAdd<UnflatSource>();
        var target = typeof(UnflatTarget).GetProperty(nameof(UnflatTarget.Customer))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();

        var source = new UnflatSource { CustomerAddressCity = "Portland" };
        var result = provider!.Provide(source, null!, "", new MappingScope());

        result.Should().NotBeNull();
        var customer = result.Should().BeOfType<FlatteningCustomer>().Subject;
        customer.Address.Should().NotBeNull();
        customer.Address.City.Should().Be("Portland");
    }

    [Fact]
    public void TryLink_ThreeLevel_UnflattensCustomerAddressAndName()
    {
        var origin = _cache.GetOrAdd<Unflatten3LevelSource>();
        var target = typeof(Unflatten3LevelTarget).GetProperty(nameof(Unflatten3LevelTarget.Customer))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();

        var source = new Unflatten3LevelSource
        {
            CustomerFirstName = "Carol",
            CustomerAddressCity = "Denver",
            CustomerAddressStreet = "Elm St",
        };

        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().NotBeNull();
        var customer = result.Should().BeOfType<FlatteningCustomer>().Subject;
        customer.FirstName.Should().Be("Carol");
        customer.Address.Should().NotBeNull();
        customer.Address.City.Should().Be("Denver");
        customer.Address.Street.Should().Be("Elm St");
    }

    [Fact]
    public void TryLink_ReadOnlyIntermediate_ReturnsFalse()
    {
        // ReadOnlyIntermediate has no writable members → convention should return false
        var origin = _cache.GetOrAdd<ReadOnlyIntermediateSource>();
        var target = typeof(ReadOnlyIntermediateTarget).GetProperty(nameof(ReadOnlyIntermediateTarget.Data))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_ReusesExistingIntermediate_WhenMultipleMappings()
    {
        // When multiple origin members map into the same intermediate,
        // the UnflatteningValueProvider creates the intermediate once and populates all members
        var origin = _cache.GetOrAdd<UnflatSource>();
        var target = typeof(UnflatTarget).GetProperty(nameof(UnflatTarget.Customer))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();

        var source = new UnflatSource
        {
            CustomerFirstName = "Dan",
            CustomerLastName = "Jones",
            CustomerAddressCity = "Austin",
        };

        var result = provider!.Provide(source, null!, "", new MappingScope());
        var customer = result.Should().BeOfType<FlatteningCustomer>().Subject;
        // All three origin members should be set on the same Customer instance
        customer.FirstName.Should().Be("Dan");
        customer.LastName.Should().Be("Jones");
        customer.Address.City.Should().Be("Austin");
    }
}
