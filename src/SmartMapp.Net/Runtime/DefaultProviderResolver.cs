using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Default <see cref="IProviderResolver"/> used when no DI container is wired into the sculptor.
/// Prefers <see cref="IServiceProvider.GetService(Type)"/> when a provider is supplied and falls
/// back to <see cref="Activator.CreateInstance(Type)"/> for types exposing a parameterless
/// constructor — the exact behaviour Sprint 7 exhibited before the DI package existed.
/// </summary>
/// <remarks>
/// <para>
/// Stateless; exposed as a singleton via <see cref="Instance"/> and safe to share across all
/// sculptors and threads.
/// </para>
/// <para>
/// When the service provider is <c>null</c> or returns <c>null</c> for the requested type and
/// the type has no parameterless constructor, the resolver throws
/// <see cref="InvalidOperationException"/> with a diagnostic message that distinguishes the
/// two failure modes so users can tell "no DI wired" from "DI wired but this type needs
/// registration".
/// </para>
/// </remarks>
public sealed class DefaultProviderResolver : IProviderResolver
{
    /// <summary>
    /// Gets the shared singleton instance.
    /// </summary>
    public static DefaultProviderResolver Instance { get; } = new();

    private DefaultProviderResolver() { }

    /// <inheritdoc />
    public object Resolve(Type type, IServiceProvider? serviceProvider)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));

        // 1. DI-first when a service provider is supplied.
        if (serviceProvider is not null)
        {
            var fromDi = serviceProvider.GetService(type);
            if (fromDi is not null) return fromDi;
        }

        // 2. Activator fallback for types with a public parameterless constructor.
        if (type.IsAbstract || type.IsInterface)
        {
            throw new InvalidOperationException(
                $"Cannot instantiate '{type.FullName}': type is abstract or an interface. " +
                (serviceProvider is null
                    ? "No IServiceProvider was supplied — either register the concrete implementation in DI or set SculptorOptions.ProviderResolver to a resolver that can construct the type."
                    : "The supplied IServiceProvider does not have a registration for this type."));
        }

        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new InvalidOperationException(
                $"Cannot activate '{type.FullName}': the type has no public parameterless constructor and " +
                (serviceProvider is null
                    ? "no IServiceProvider was supplied. Wire up the SmartMapp.Net.DependencyInjection package or add a parameterless constructor."
                    : "the supplied IServiceProvider did not resolve the type. Register it in DI (e.g. services.AddTransient<" + type.Name + ">()) so ActivatorUtilities.CreateInstance can satisfy its constructor dependencies."));
        }

        try
        {
            return Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Activator.CreateInstance failed for '{type.FullName}'. See inner exception for details.", ex);
        }
    }
}
