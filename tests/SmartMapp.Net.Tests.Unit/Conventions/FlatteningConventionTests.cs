using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class FlatteningConventionTests
{
    private readonly TypeModelCache _cache = new();
    private readonly FlatteningConvention _convention;

    public FlatteningConventionTests()
    {
        _convention = new FlatteningConvention(_cache);
    }

    [Fact]
    public void Priority_Is300()
    {
        _convention.Priority.Should().Be(300);
    }

    [Fact]
    public void TryLink_OneLevel_CustomerFirstName()
    {
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto1).GetProperty(nameof(FlatOrderDto1.CustomerFirstName))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<ChainedPropertyAccessProvider>();

        var cpap = (ChainedPropertyAccessProvider)provider!;
        cpap.MemberPath.Should().Be("Customer.FirstName");
        cpap.Chain.Should().HaveCount(2);

        var order = new FlatteningOrder { Customer = new FlatteningCustomer { FirstName = "Alice" } };
        var result = provider.Provide(order, null!, "", new MappingScope());
        result.Should().Be("Alice");
    }

    [Fact]
    public void TryLink_TwoLevel_CustomerAddressCity()
    {
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto2).GetProperty(nameof(FlatOrderDto2.CustomerAddressCity))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<ChainedPropertyAccessProvider>();

        var cpap = (ChainedPropertyAccessProvider)provider!;
        cpap.MemberPath.Should().Be("Customer.Address.City");
        cpap.Chain.Should().HaveCount(3);

        var order = new FlatteningOrder
        {
            Customer = new FlatteningCustomer
            {
                Address = new FlatteningAddress { City = "Seattle" }
            }
        };
        var result = provider.Provide(order, null!, "", new MappingScope());
        result.Should().Be("Seattle");
    }

    [Fact]
    public void TryLink_ThreeLevel_CustomerAddressStreetDetailLine1()
    {
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto3).GetProperty(nameof(FlatOrderDto3.CustomerAddressStreetDetailLine1))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<ChainedPropertyAccessProvider>();

        var cpap = (ChainedPropertyAccessProvider)provider!;
        cpap.MemberPath.Should().Be("Customer.Address.StreetDetail.Line1");
        cpap.Chain.Should().HaveCount(4);
    }

    [Fact]
    public void TryLink_NoMatch_ReturnsFalse()
    {
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(NonFlatDto).GetProperty(nameof(NonFlatDto.FooBarBaz))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_SingleMember_ReturnsFalse_DefersToExactName()
    {
        // "Id" is a single member, not a flattened chain
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto1).GetProperty(nameof(FlatOrderDto1.Id))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
    }

    [Fact]
    public void TryLink_NullIntermediate_ReturnsNull()
    {
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto1).GetProperty(nameof(FlatOrderDto1.CustomerFirstName))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();

        // Customer is null
        var order = new FlatteningOrder { Customer = null! };
        var result = provider!.Provide(order, null!, "", new MappingScope());
        result.Should().BeNull();
    }

    [Fact]
    public void TryLink_CaseInsensitivePrefix()
    {
        // The prefix match is case-insensitive
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto2).GetProperty(nameof(FlatOrderDto2.CustomerAddressStreet))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void ChainedProvider_MemberPath_IsCorrectDottedString()
    {
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto2).GetProperty(nameof(FlatOrderDto2.CustomerAddressCity))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();

        var cpap = (ChainedPropertyAccessProvider)provider!;
        cpap.MemberPath.Should().Be("Customer.Address.City");
    }

    [Fact]
    public void ChainedProvider_NavigatesFullChain()
    {
        var origin = _cache.GetOrAdd<FlatteningOrder>();
        var target = typeof(FlatOrderDto2).GetProperty(nameof(FlatOrderDto2.CustomerAddressStreet))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();

        var order = new FlatteningOrder
        {
            Customer = new FlatteningCustomer
            {
                Address = new FlatteningAddress { Street = "123 Main St" }
            }
        };
        var result = provider!.Provide(order, null!, "", new MappingScope());
        result.Should().Be("123 Main St");
    }

    [Fact]
    public void TryLink_Backtracking_StatusCode()
    {
        // "StatusCode" requires backtracking: tries "S" first, fails, then "Status" → "Code"
        var origin = _cache.GetOrAdd<BacktrackSource>();
        var target = typeof(BacktrackFlatDto).GetProperty(nameof(BacktrackFlatDto.StatusCode))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<ChainedPropertyAccessProvider>();

        var cpap = (ChainedPropertyAccessProvider)provider!;
        cpap.MemberPath.Should().Be("Status.Code");

        var source = new BacktrackSource { Status = new BacktrackStatus { Code = 200 } };
        var result = provider.Provide(source, null!, "", new MappingScope());
        result.Should().Be(200);
    }

    [Fact]
    public void TryLink_Backtracking_StatusText()
    {
        var origin = _cache.GetOrAdd<BacktrackSource>();
        var target = typeof(BacktrackFlatDto).GetProperty(nameof(BacktrackFlatDto.StatusText))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        var cpap = (ChainedPropertyAccessProvider)provider!;
        cpap.MemberPath.Should().Be("Status.Text");
    }

    [Fact]
    public void TryLink_MaxDepthExceeded_ReturnsFalse()
    {
        // DepthLevel1 → L2 → L3 → L4 → L5 → L6 → L7 → Value = 7 levels, exceeds MaxDepth=5
        var origin = _cache.GetOrAdd<DepthLevel1>();
        var target = typeof(DepthFlatDto).GetProperty(nameof(DepthFlatDto.L2L3L4L5L6L7Value))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_CaseInsensitivePrefix_MatchesLowercaseOrigin()
    {
        // Origin has lowercase "customer" property, target has "CustomerName" — case-insensitive prefix
        var origin = _cache.GetOrAdd<CaseInsensitiveFlatSource>();
        var target = typeof(CaseInsensitiveFlatTarget).GetProperty(nameof(CaseInsensitiveFlatTarget.CustomerName))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<ChainedPropertyAccessProvider>();

        var cpap = (ChainedPropertyAccessProvider)provider!;
        cpap.MemberPath.Should().Be("customer.Name");

        var source = new CaseInsensitiveFlatSource
        {
            customer = new CaseInsensitiveFlatNested { Name = "Alice" }
        };
        var result = provider.Provide(source, null!, "", new MappingScope());
        result.Should().Be("Alice");
    }
}
