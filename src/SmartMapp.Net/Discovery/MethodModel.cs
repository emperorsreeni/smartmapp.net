using System.Reflection;

namespace SmartMapp.Net.Discovery;

/// <summary>
/// Cached reflection wrapper for a parameterless method that looks like a property getter
/// (e.g., <c>GetFullName()</c>). Used by <c>MethodToPropertyConvention</c> in Sprint 2.
/// </summary>
public sealed class MethodModel
{
    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.MethodInfo"/>.
    /// </summary>
    public MethodInfo MethodInfo { get; }

    /// <summary>
    /// Gets the method name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the return type of the method.
    /// </summary>
    public Type ReturnType { get; }

    /// <summary>
    /// Gets the inferred property name (e.g., "FullName" from "GetFullName").
    /// </summary>
    public string InferredPropertyName { get; }

    internal MethodModel(MethodInfo method)
    {
        MethodInfo = method;
        Name = method.Name;
        ReturnType = method.ReturnType;

        // Strip "Get" prefix if present to infer property name
        if (Name.StartsWith("Get", StringComparison.Ordinal) && Name.Length > 3)
            InferredPropertyName = Name.Substring(3);
        else
            InferredPropertyName = Name;
    }

    /// <inheritdoc />
    public override string ToString() => $"{ReturnType.Name} {Name}()";
}
