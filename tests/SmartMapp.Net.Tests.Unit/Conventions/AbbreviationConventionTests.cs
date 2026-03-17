using FluentAssertions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class AbbreviationConventionTests
{
    private readonly AbbreviationConvention _convention = new();

    [Fact]
    public void Priority_Is400()
    {
        _convention.Priority.Should().Be(400);
    }

    [Fact]
    public void TryLink_AddrToAddress()
    {
        var origin = new TypeModel(typeof(AbbreviatedSource));
        var target = typeof(ExpandedTarget).GetProperty(nameof(ExpandedTarget.Address))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new AbbreviatedSource { Addr = "123 Main St" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("123 Main St");
    }

    [Fact]
    public void TryLink_AddressToAddr_Reverse()
    {
        var origin = new TypeModel(typeof(ExpandedSource));
        var target = typeof(AbbreviatedTarget).GetProperty(nameof(AbbreviatedTarget.Addr))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new ExpandedSource { Address = "456 Oak Ave" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("456 Oak Ave");
    }

    [Fact]
    public void TryLink_QtyToQuantity()
    {
        var origin = new TypeModel(typeof(AbbreviatedSource));
        var target = typeof(ExpandedTarget).GetProperty(nameof(ExpandedTarget.Quantity))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new AbbreviatedSource { Qty = 10 };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be(10);
    }

    [Fact]
    public void TryLink_CustomAlias()
    {
        var custom = new AbbreviationConvention(new Dictionary<string, string>
        {
            ["Cust"] = "Customer"
        });

        var origin = new TypeModel(typeof(AbbreviatedSource));
        // No match for custom alias with this origin — just verify it doesn't crash
        var target = typeof(ExpandedTarget).GetProperty(nameof(ExpandedTarget.Address))!;
        custom.TryLink(target, origin, out _).Should().BeTrue();
    }

    [Fact]
    public void TryLink_NoMatch_ReturnsFalse()
    {
        var origin = new TypeModel(typeof(AbbreviatedSource));
        var target = typeof(NonFlatDto).GetProperty(nameof(NonFlatDto.FooBarBaz))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_SkipsExactMatch()
    {
        // If origin has same name as target, skip (ExactNameConvention handles it)
        var origin = new TypeModel(typeof(ExpandedSource));
        var target = typeof(ExpandedTarget).GetProperty(nameof(ExpandedTarget.Address))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
    }

    [Fact]
    public void TryLink_MultiSegment_CustAddrToCustomerAddress()
    {
        var custom = new AbbreviationConvention(new Dictionary<string, string>
        {
            ["Cust"] = "Customer"
        });

        var origin = new TypeModel(typeof(MultiSegmentAbbrSource));
        var target = typeof(MultiSegmentAbbrTarget).GetProperty(nameof(MultiSegmentAbbrTarget.CustomerAddress))!;

        custom.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new MultiSegmentAbbrSource { CustAddr = "789 Pine Rd" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("789 Pine Rd");
    }

    [Fact]
    public void TryLink_AmtToAmount()
    {
        var origin = new TypeModel(typeof(AbbreviatedSource));
        var target = typeof(ExpandedTarget).GetProperty(nameof(ExpandedTarget.Amount))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new AbbreviatedSource { Amt = 99.99m };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be(99.99m);
    }
}
