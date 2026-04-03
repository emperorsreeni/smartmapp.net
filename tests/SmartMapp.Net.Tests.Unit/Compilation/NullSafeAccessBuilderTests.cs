using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Compilation;

public class NullSafeAccessBuilderTests
{
    [Fact]
    public void SingleLevel_NoNullCheck_ReturnsValue()
    {
        var param = Expression.Parameter(typeof(FlatOrder), "origin");
        var member = typeof(FlatOrder).GetProperty("Name")!;

        var expr = NullSafeAccessBuilder.BuildNullSafeAccess(
            param, new MemberInfo[] { member }, typeof(string));

        var lambda = Expression.Lambda<Func<FlatOrder, string>>(expr, param).Compile();
        var result = lambda(new FlatOrder { Name = "Test" });

        result.Should().Be("Test");
    }

    [Fact]
    public void TwoLevel_NullIntermediate_ReturnsDefault()
    {
        var param = Expression.Parameter(typeof(NestedOrder), "origin");
        var customerProp = typeof(NestedOrder).GetProperty("Customer")!;
        var nameProp = typeof(NestedCustomer).GetProperty("Name")!;

        var expr = NullSafeAccessBuilder.BuildNullSafeAccess(
            param, new MemberInfo[] { customerProp, nameProp }, typeof(string));

        var lambda = Expression.Lambda<Func<NestedOrder, string>>(expr, param).Compile();

        // Non-null path
        var result1 = lambda(new NestedOrder { Customer = new NestedCustomer { Name = "Alice" } });
        result1.Should().Be("Alice");

        // Null intermediate
        var result2 = lambda(new NestedOrder { Customer = null! });
        result2.Should().BeNull();
    }

    [Fact]
    public void ThreeLevel_AllNull_ReturnsDefault()
    {
        var param = Expression.Parameter(typeof(NestedOrder), "origin");
        var customerProp = typeof(NestedOrder).GetProperty("Customer")!;
        var addressProp = typeof(NestedCustomer).GetProperty("Address")!;
        var cityProp = typeof(NestedAddress).GetProperty("City")!;

        var expr = NullSafeAccessBuilder.BuildNullSafeAccess(
            param, new MemberInfo[] { customerProp, addressProp, cityProp }, typeof(string));

        var lambda = Expression.Lambda<Func<NestedOrder, string>>(expr, param).Compile();

        // All present
        var order = new NestedOrder
        {
            Customer = new NestedCustomer
            {
                Address = new NestedAddress { City = "NYC" }
            }
        };
        lambda(order).Should().Be("NYC");

        // Null customer
        lambda(new NestedOrder { Customer = null! }).Should().BeNull();
    }

    [Fact]
    public void ThreeLevel_AllNonNull_ReturnsValue()
    {
        var param = Expression.Parameter(typeof(NestedOrder), "origin");
        var customerProp = typeof(NestedOrder).GetProperty("Customer")!;
        var addressProp = typeof(NestedCustomer).GetProperty("Address")!;
        var streetProp = typeof(NestedAddress).GetProperty("Street")!;

        var expr = NullSafeAccessBuilder.BuildNullSafeAccess(
            param, new MemberInfo[] { customerProp, addressProp, streetProp }, typeof(string));

        var lambda = Expression.Lambda<Func<NestedOrder, string>>(expr, param).Compile();

        var order = new NestedOrder
        {
            Customer = new NestedCustomer
            {
                Address = new NestedAddress { Street = "5th Ave" }
            }
        };
        lambda(order).Should().Be("5th Ave");
    }

    [Fact]
    public void ValueTypeIntermediate_NoNullCheck()
    {
        // DateTime.Year is int — value type, no null check needed
        var param = Expression.Parameter(typeof(FlatOrder), "origin");
        var createdAtProp = typeof(FlatOrder).GetProperty("CreatedAt")!;
        // Accessing Year on DateTime (value type) — should work without null checks
        var yearProp = typeof(DateTime).GetProperty("Year")!;

        var expr = NullSafeAccessBuilder.BuildNullSafeAccess(
            param, new MemberInfo[] { createdAtProp, yearProp }, typeof(int));

        var lambda = Expression.Lambda<Func<FlatOrder, int>>(expr, param).Compile();
        var result = lambda(new FlatOrder { CreatedAt = new DateTime(2024, 6, 15) });

        result.Should().Be(2024);
    }

    [Fact]
    public void NullSafeSingleAccess_RootRefType_NullChecked()
    {
        var param = Expression.Parameter(typeof(NestedCustomer), "customer");
        var nameProp = typeof(NestedCustomer).GetProperty("Name")!;

        var expr = NullSafeAccessBuilder.BuildNullSafeSingleAccess(param, nameProp, typeof(string));

        var lambda = Expression.Lambda<Func<NestedCustomer, string>>(expr, param).Compile();

        lambda(new NestedCustomer { Name = "Bob" }).Should().Be("Bob");
        lambda(null!).Should().BeNull();
    }

    [Fact]
    public void NullableIntermediate_HasValue_ReturnsValue()
    {
        // Chain: NullableChainOrigin.Inner.Score (int? intermediate)
        var param = Expression.Parameter(typeof(NullableChainInner), "inner");
        var scoreProp = typeof(NullableChainInner).GetProperty("Score")!;

        var expr = NullSafeAccessBuilder.BuildNullSafeAccess(
            param, new MemberInfo[] { scoreProp }, typeof(int?));

        var lambda = Expression.Lambda<Func<NullableChainInner, int?>>(expr, param).Compile();

        lambda(new NullableChainInner { Score = 42 }).Should().Be(42);
        lambda(new NullableChainInner { Score = null }).Should().BeNull();
    }
}
