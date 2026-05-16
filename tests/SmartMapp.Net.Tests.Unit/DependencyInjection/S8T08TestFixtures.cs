namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

// S8-T08 fixtures — shapes for multi-origin composition per spec §8.11.

public class S8T08User
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class S8T08OrderSummary
{
    public int OpenOrders { get; set; }
    public decimal LifetimeValue { get; set; }
}

public class S8T08Company
{
    public string CompanyName { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
}

public class S8T08DashboardViewModel
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OpenOrders { get; set; }
    public decimal LifetimeValue { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
}

// Single-origin parity fixture — used to prove sculptor.Compose<T>(user) == sculptor.Map<User, T>(user).
public class S8T08UserDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

// Fixtures for collision/override semantics: both origins define DisplayName; declaration order
// (User first, then Company) means Company wins on DisplayName per spec §S8-T08 Constraints "last-origin-wins".
public class S8T08OverrideDto
{
    public string DisplayName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
}

public class S8T08OverrideUser
{
    public string DisplayName { get; set; } = string.Empty;
    public int UserId { get; set; }
}

public class S8T08OverrideCompany
{
    public string DisplayName { get; set; } = string.Empty;  // overrides User.DisplayName when declared last
    public string CompanyName { get; set; } = string.Empty;
}

// Fixture for the 4-origin test — combines Dashboard with a Preferences slot to cover spec
// §S8-T08 Unit-Tests "2, 3, 4 origin dispatch".
public class S8T08Preferences
{
    public string Locale { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
}

public class S8T08RichDashboardViewModel
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int OpenOrders { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
}
