namespace SmartMapp.Net.Conventions;

/// <summary>
/// Contract for type-level conventions that discover which type pairs should be mapped.
/// Unlike <see cref="IPropertyConvention"/> which links individual members,
/// type conventions determine whether two types should be paired at all.
/// </summary>
public interface ITypeConvention
{
    /// <summary>
    /// Attempts to determine whether the specified origin and target types should be mapped.
    /// </summary>
    /// <param name="originType">The candidate origin type.</param>
    /// <param name="targetType">The candidate target type.</param>
    /// <param name="blueprint">When successful, an optional initial blueprint. May be <c>null</c> if
    /// the convention only determines pairing (links built later by <see cref="ConventionPipeline"/>).</param>
    /// <returns><c>true</c> if the types should be paired; otherwise <c>false</c>.</returns>
    bool TryBind(Type originType, Type targetType, out Blueprint? blueprint);
}
