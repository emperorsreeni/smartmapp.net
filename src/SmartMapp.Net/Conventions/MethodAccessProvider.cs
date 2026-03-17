using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// A value provider that invokes a parameterless method on the origin object
/// using a compiled delegate for high performance.
/// </summary>
public sealed class MethodAccessProvider : IValueProvider
{
    private readonly Func<object, object?> _invoker;

    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.MethodInfo"/> being invoked.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets the inferred property name derived from the method name
    /// (e.g., <c>"FullName"</c> from <c>"GetFullName"</c>).
    /// </summary>
    public string InferredPropertyName { get; }

    /// <summary>
    /// Initializes a new <see cref="MethodAccessProvider"/> for the specified method.
    /// </summary>
    /// <param name="method">The parameterless method to invoke on the origin.</param>
    public MethodAccessProvider(MethodInfo method)
    {
        Method = method;
        _invoker = BuildInvoker(method);

        if (method.Name.StartsWith("Get", StringComparison.Ordinal) && method.Name.Length > 3)
            InferredPropertyName = method.Name.Substring(3);
        else
            InferredPropertyName = method.Name;
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        if (origin is null) return null;
        return _invoker(origin);
    }

    /// <inheritdoc />
    public override string ToString() => $"MethodAccess({Method.Name}())";

    private static Func<object, object?> BuildInvoker(MethodInfo method)
    {
        // origin => (object?)((OriginType)origin).MethodName()
        var param = Expression.Parameter(typeof(object), "origin");
        var declaringType = method.DeclaringType ?? throw new InvalidOperationException(
            $"Method '{method.Name}' has no declaring type.");

        var cast = Expression.Convert(param, declaringType);
        var call = Expression.Call(cast, method);
        var boxed = Expression.Convert(call, typeof(object));

        return Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
    }
}
