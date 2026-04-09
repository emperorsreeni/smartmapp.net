using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net;

/// <summary>
/// Abstract base class for user-defined mapping configurations.
/// Subclass and override <see cref="Design"/> to define type bindings using the fluent API.
/// <para>
/// Example:
/// <code>
/// public class OrderBlueprint : MappingBlueprint
/// {
///     public override void Design(IBlueprintBuilder plan)
///     {
///         plan.Bind&lt;Order, OrderDto&gt;()
///             .Property(d => d.Total, p => p.From(s => s.Lines.Sum(l => l.Qty * l.Price)));
///     }
/// }
/// </code>
/// </para>
/// </summary>
public abstract class MappingBlueprint
{
    /// <summary>
    /// Defines the mapping configuration using the provided builder.
    /// Called exactly once during the build/forge phase.
    /// </summary>
    /// <param name="plan">The builder used to register type bindings.</param>
    public abstract void Design(IBlueprintBuilder plan);
}
