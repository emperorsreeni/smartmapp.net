using System.Reflection;

namespace SmartMapp.Net.Discovery;

/// <summary>
/// Cached reflection wrapper for a constructor.
/// Pre-computes parameter information and primary constructor detection.
/// </summary>
public sealed class ConstructorModel
{
    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.ConstructorInfo"/>.
    /// </summary>
    public ConstructorInfo ConstructorInfo { get; }

    /// <summary>
    /// Gets the constructor parameters.
    /// </summary>
    public IReadOnlyList<ParameterInfo> Parameters { get; }

    /// <summary>
    /// Gets the number of parameters.
    /// </summary>
    public int ParameterCount { get; }

    /// <summary>
    /// Gets a value indicating whether this is the primary constructor
    /// (only constructor, record positional ctor, or explicitly marked).
    /// </summary>
    public bool IsPrimary { get; internal set; }

    internal ConstructorModel(ConstructorInfo constructorInfo)
    {
        ConstructorInfo = constructorInfo;
        Parameters = constructorInfo.GetParameters();
        ParameterCount = Parameters.Count;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $".ctor({string.Join(", ", Parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))})";
}
