namespace SmartMapp.Net.Tests.Unit.TestTypes;

// Simple POCO
public class SimpleClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// DTO counterpart
public class SimpleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Init-only properties
public class InitOnlyClass
{
    public int Id { get; init; }
    public string Label { get; init; } = string.Empty;
}

// Record with positional parameters
public record PersonRecord(string FirstName, string LastName, int Age);

// Record with init properties
public record PersonDto
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public int Age { get; init; }
}

// Nested object
public class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; } = new();
}

public class Customer
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public string GetFullName() => Name;
}

public class OrderDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}

// Abstract class
public abstract class BaseEntity
{
    public int Id { get; set; }
}

public class ConcreteEntity : BaseEntity
{
    public string Value { get; set; } = string.Empty;
}

// Interface
public interface IHasName
{
    string Name { get; }
}

// Generic class
public class Wrapper<T>
{
    public T Value { get; set; } = default!;
}

// Class with multiple constructors
public class MultiCtorClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public MultiCtorClass() { }
    public MultiCtorClass(int id) { Id = id; }
    public MultiCtorClass(int id, string name) { Id = id; Name = name; }
    public MultiCtorClass(int id, string name, string description) { Id = id; Name = name; Description = description; }
}

// Class with public field
public class ClassWithField
{
    public int Id;
    public string Name { get; set; } = string.Empty;
}

// Class with value methods
public class ClassWithMethods
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public string GetFullName() => $"{FirstName} {LastName}";
    public int GetAge() => 25;
}

// Nullable property class
public class NullableClass
{
    public int? NullableInt { get; set; }
    public string? NullableString { get; set; }
}

// Required members class (C# 11+)
public class RequiredClass
{
    public required string Name { get; set; }
    public required int Code { get; set; }
    public string? Optional { get; set; }
}

// Nested address for flattening tests
public class Address
{
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
}

public class CustomerWithAddress
{
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
}

public class FlatCustomerDto
{
    public string Name { get; set; } = string.Empty;
    public string AddressCity { get; set; } = string.Empty;
    public string AddressStreet { get; set; } = string.Empty;
}

// Service for DI tests
public interface ITestService
{
    string GetValue();
}

public class TestService : ITestService
{
    public string GetValue() => "resolved";
}
