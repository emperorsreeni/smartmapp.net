using System.Linq.Expressions;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Holds the discriminator-based dispatch configuration for a base type pair.
/// Configured via <c>.DiscriminateBy(expr).When(value, rule).Otherwise(rule)</c>.
/// </summary>
internal sealed class DiscriminatorConfig
{
    /// <summary>
    /// Gets the discriminator expression that extracts a value from the origin for dispatch.
    /// </summary>
    internal LambdaExpression DiscriminatorExpression { get; }

    /// <summary>
    /// Gets the When-clauses mapping discriminator values to target type pairs.
    /// </summary>
    internal IReadOnlyList<DiscriminatorWhenClause> WhenClauses => _whenClauses;

    /// <summary>
    /// Gets the Otherwise target type pair used when no When-clause matches.
    /// </summary>
    internal TypePair? OtherwisePair { get; set; }

    private readonly List<DiscriminatorWhenClause> _whenClauses = new();

    /// <summary>
    /// Initializes a new <see cref="DiscriminatorConfig"/> with the given discriminator expression.
    /// </summary>
    /// <param name="discriminatorExpression">An expression extracting the discriminator value from the origin.</param>
    internal DiscriminatorConfig(LambdaExpression discriminatorExpression)
    {
        DiscriminatorExpression = discriminatorExpression;
    }

    /// <summary>
    /// Adds a When-clause that maps a discriminator value to a derived type pair.
    /// </summary>
    /// <param name="value">The discriminator value to match.</param>
    /// <param name="targetPair">The type pair to dispatch to when matched.</param>
    internal void AddWhen(object value, TypePair targetPair)
    {
        _whenClauses.Add(new DiscriminatorWhenClause(value, targetPair));
    }
}

/// <summary>
/// A single When-clause in a discriminator configuration.
/// </summary>
internal sealed record DiscriminatorWhenClause(object Value, TypePair TargetPair);
