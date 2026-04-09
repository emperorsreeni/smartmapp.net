using System.Collections.Concurrent;
using System.Reflection;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Resolves concrete types for interface and abstract class targets.
/// Supports explicit <c>Materialize&lt;T&gt;()</c> configuration and runtime proxy generation
/// via <see cref="System.Reflection.DispatchProxy"/>.
/// </summary>
internal sealed class InterfaceMaterializer
{
    private readonly InheritanceResolver _resolver;
    private static readonly ConcurrentDictionary<Type, Type> ProxyTypeCache = new();

    /// <summary>
    /// Initializes a new <see cref="InterfaceMaterializer"/>.
    /// </summary>
    internal InterfaceMaterializer(InheritanceResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>
    /// Resolves the concrete type to use for constructing a target of the given type pair.
    /// Returns the original target type if it is already concrete.
    /// </summary>
    /// <param name="pair">The type pair.</param>
    /// <returns>The concrete type to instantiate.</returns>
    /// <exception cref="Compilation.MappingCompilationException">
    /// Thrown when the target is abstract and no <c>Materialize&lt;T&gt;()</c> is configured.
    /// </exception>
    internal Type ResolveConcreteType(TypePair pair)
    {
        var targetType = pair.TargetType;

        // Already concrete — return as-is
        if (!targetType.IsInterface && !targetType.IsAbstract)
            return targetType;

        // Check for explicit Materialize<T> configuration
        var materializeType = _resolver.GetMaterializeType(pair);
        if (materializeType is not null)
        {
            ValidateMaterializeType(materializeType, targetType);
            return materializeType;
        }

        // Interface targets can use DispatchProxy
        if (targetType.IsInterface)
        {
            return GetOrCreateProxyType(targetType);
        }

        // Abstract class without Materialize — error
        throw new Compilation.MappingCompilationException(
            $"Cannot map to abstract type '{targetType.Name}'. " +
            $"Use .Materialize<TConcrete>() to specify a concrete implementation, " +
            $"or derive from the abstract class.");
    }

    /// <summary>
    /// Validates that the materialization type implements/extends the target type.
    /// </summary>
    private static void ValidateMaterializeType(Type concreteType, Type targetType)
    {
        if (!targetType.IsAssignableFrom(concreteType))
        {
            throw new Compilation.MappingCompilationException(
                $"Materialize type '{concreteType.Name}' does not implement or inherit from target type '{targetType.Name}'.");
        }

        if (concreteType.IsAbstract || concreteType.IsInterface)
        {
            throw new Compilation.MappingCompilationException(
                $"Materialize type '{concreteType.Name}' must be a concrete (non-abstract, non-interface) type.");
        }
    }

    /// <summary>
    /// Gets or creates a <see cref="DispatchProxy"/>-based proxy type for the given interface.
    /// Validates that the proxy can be created by invoking the generic <c>DispatchProxy.Create</c> once.
    /// </summary>
    private static Type GetOrCreateProxyType(Type interfaceType)
    {
        return ProxyTypeCache.GetOrAdd(interfaceType, static iface =>
        {
            // Validate that a proxy can be created for this interface
            try
            {
                var createMethod = FindDispatchProxyCreateMethod()
                    .MakeGenericMethod(iface, typeof(PropertyBackedProxy));

                // Actually create a test instance to verify it works
                createMethod.Invoke(null, null);
            }
            catch (Exception ex) when (ex is not Compilation.MappingCompilationException)
            {
                throw new Compilation.MappingCompilationException(
                    $"Cannot create DispatchProxy for interface '{iface.Name}'. " +
                    $"Ensure the interface is public. Inner: {ex.InnerException?.Message ?? ex.Message}");
            }

            // Return a sentinel — actual proxy creation happens via CreateProxy() at mapping time
            return typeof(PropertyBackedProxy);
        });
    }

    /// <summary>
    /// Creates a proxy instance for the given interface type.
    /// </summary>
    internal static object CreateProxy(Type interfaceType)
    {
        var createMethod = FindDispatchProxyCreateMethod()
            .MakeGenericMethod(interfaceType, typeof(PropertyBackedProxy));

        return createMethod.Invoke(null, null)!;
    }

    /// <summary>
    /// Finds the generic <c>DispatchProxy.Create&lt;T, TProxy&gt;()</c> method,
    /// avoiding <see cref="AmbiguousMatchException"/> when multiple overloads exist.
    /// </summary>
    private static MethodInfo FindDispatchProxyCreateMethod()
    {
        return typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(DispatchProxy.Create)
                        && m.IsGenericMethodDefinition
                        && m.GetGenericArguments().Length == 2
                        && m.GetParameters().Length == 0);
    }
}

/// <summary>
/// A <see cref="DispatchProxy"/> implementation that backs interface property access
/// with an internal dictionary. Used for mapping to interface targets when no
/// <c>Materialize&lt;T&gt;()</c> is configured.
/// </summary>
public class PropertyBackedProxy : DispatchProxy
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
            return null;

        var name = targetMethod.Name;

        // Property getter: get_PropertyName
        if (name.StartsWith("get_", StringComparison.Ordinal) && (args is null || args.Length == 0))
        {
            var propName = name.Substring(4);
            _values.TryGetValue(propName, out var value);
            return value;
        }

        // Property setter: set_PropertyName
        if (name.StartsWith("set_", StringComparison.Ordinal) && args is { Length: 1 })
        {
            var propName = name.Substring(4);
            _values[propName] = args[0];
            return null;
        }

        // ToString
        if (name == nameof(ToString) && (args is null || args.Length == 0))
        {
            return $"Proxy({_values.Count} properties)";
        }

        // GetHashCode
        if (name == nameof(GetHashCode) && (args is null || args.Length == 0))
        {
            var hash = 17;
            foreach (var kv in _values)
            {
                hash = hash * 31 + (kv.Value?.GetHashCode() ?? 0);
            }
            return hash;
        }

        // Equals
        if (name == nameof(Equals) && args is { Length: 1 })
        {
            if (args[0] is PropertyBackedProxy other)
            {
                if (_values.Count != other._values.Count) return false;
                foreach (var kv in _values)
                {
                    if (!other._values.TryGetValue(kv.Key, out var otherVal) || !Equals(kv.Value, otherVal))
                        return false;
                }
                return true;
            }
            return false;
        }

        // Default: return default for the return type
        if (targetMethod.ReturnType == typeof(void))
            return null;

        return targetMethod.ReturnType.IsValueType
            ? Activator.CreateInstance(targetMethod.ReturnType)
            : null;
    }
}
