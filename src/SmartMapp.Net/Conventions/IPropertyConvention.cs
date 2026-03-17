using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Contract for property-level conventions that attempt to link a target member
/// to a value from the origin type. The convention pipeline iterates conventions
/// in <see cref="Priority"/> order (ascending) until one succeeds.
/// </summary>
public interface IPropertyConvention
{
    /// <summary>
    /// Gets the execution priority. Lower values execute first.
    /// Built-in conventions use 100–400; user conventions default to 500+.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Attempts to link a target member to a value from the origin type model.
    /// </summary>
    /// <param name="targetMember">The target member to find a value for.</param>
    /// <param name="originModel">The cached type model for the origin type.</param>
    /// <param name="provider">When successful, the value provider that extracts the value from the origin.</param>
    /// <returns><c>true</c> if a link was established; otherwise <c>false</c>.</returns>
    bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider);
}
