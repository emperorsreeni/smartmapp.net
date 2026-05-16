namespace SmartMapp.Net.Attributes;

/// <summary>
/// Declares that the decorated target member should never be mapped — it is skipped by every convention
/// and by attribute-driven linking regardless of origin availability.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class UnmappedAttribute : Attribute
{
}
