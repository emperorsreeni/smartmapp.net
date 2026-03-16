namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Non-generic marker interface for value providers.
/// A value provider extracts or computes a value from the origin object to populate a target member.
/// </summary>
public interface IValueProvider
{
    /// <summary>
    /// Provides a value for the specified target member.
    /// </summary>
    /// <param name="origin">The source object.</param>
    /// <param name="target">The target object being populated.</param>
    /// <param name="targetMemberName">The name of the target member being set.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The value to assign to the target member.</returns>
    object? Provide(object origin, object target, string targetMemberName, MappingScope scope);
}

/// <summary>
/// Strongly-typed interface for extracting or computing a value from the origin object.
/// </summary>
/// <typeparam name="TOrigin">The source object type (contravariant).</typeparam>
/// <typeparam name="TTarget">The target object type (contravariant).</typeparam>
/// <typeparam name="TMember">The target member type (covariant).</typeparam>
public interface IValueProvider<in TOrigin, in TTarget, out TMember> : IValueProvider
{
    /// <summary>
    /// Provides a typed value for the specified target member.
    /// </summary>
    /// <param name="origin">The source object.</param>
    /// <param name="target">The target object being populated.</param>
    /// <param name="targetMemberName">The name of the target member being set.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The typed value to assign to the target member.</returns>
    TMember Provide(TOrigin origin, TTarget target, string targetMemberName, MappingScope scope);
}
