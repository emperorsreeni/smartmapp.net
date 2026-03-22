using System.ComponentModel;

namespace SmartMapp.Net.Tests.Unit.TestTypes;

/// <summary>
/// Test type with implicit and explicit conversion operators for operator transformer tests.
/// </summary>
public class Money
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";

    public static implicit operator decimal(Money m) => m.Amount;
    public static explicit operator Money(decimal d) => new() { Amount = d, Currency = "USD" };

    public override string ToString() => $"{Currency} {Amount:N2}";
}

/// <summary>
/// Test type with no conversion operators.
/// </summary>
public class NoOperatorType
{
    public int Value { get; set; }
}

/// <summary>
/// Test enum for enum transformer tests — source.
/// </summary>
public enum OrderStatus
{
    [Description("Order is pending")]
    Pending = 0,

    [Description("Order is processing")]
    Processing = 1,

    [Description("Order has shipped")]
    Shipped = 2,

    [Description("Order is cancelled")]
    Cancelled = 3,
}

/// <summary>
/// Test enum for enum transformer tests — target (overlapping members).
/// </summary>
public enum OrderStatusDto
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Cancelled = 3,
    Unknown = 99,
}

/// <summary>
/// Test enum with non-overlapping values for edge case testing.
/// </summary>
public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    Refunded = 2,
}

/// <summary>
/// Test flags enum.
/// </summary>
[Flags]
public enum FilePermissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
}

/// <summary>
/// Test flags enum — target.
/// </summary>
[Flags]
public enum FilePermissionsDto
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
}

/// <summary>
/// Type with both implicit and explicit operators to the same target type,
/// used to verify implicit preference over explicit.
/// </summary>
public class Temperature
{
    public double Celsius { get; set; }

    public static implicit operator double(Temperature t) => t.Celsius;
    public static explicit operator Temperature(double d) => new() { Celsius = d };
}

/// <summary>
/// Type with an implicit operator defined on the TARGET type (converts from int).
/// Used to verify operator detection scans the target type, not just the origin type.
/// </summary>
public class Percentage
{
    public double Value { get; set; }

    public static implicit operator Percentage(int value) => new() { Value = value };
}

/// <summary>
/// Simple POCO for JsonElement serialization tests.
/// </summary>
public class JsonTestDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>
/// Source fixture class exercising all Sprint 3 transformer property types.
/// Reusable for Sprint 4 expression compiler integration tests.
/// </summary>
public class TransformerPropertySource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Uri? Website { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateOnly BirthDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public byte[] Avatar { get; set; } = [];
    public int? Score { get; set; }
    public bool IsActive { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>
/// Target fixture class with compatible but different types for all Sprint 3 transformers.
/// Reusable for Sprint 4 expression compiler integration tests.
/// </summary>
public class TransformerPropertyTarget
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Website { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime BirthDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public string Avatar { get; set; } = "";
    public int Score { get; set; }
    public int IsActive { get; set; }
    public string Status { get; set; } = "";
    public decimal Balance { get; set; }
}
