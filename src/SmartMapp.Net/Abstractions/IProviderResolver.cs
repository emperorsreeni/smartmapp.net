namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Strategy for resolving <see cref="IValueProvider"/> and <see cref="ITypeTransformer"/>
/// instances at mapping time. A resolver consumes an optional <see cref="IServiceProvider"/>
/// (the DI container's scope, when available) and a CLR <see cref="Type"/> and returns an
/// instance of that type — using DI-registered services when present and falling back to
/// <see cref="Activator.CreateInstance(Type)"/> when the service is not registered or no
/// service provider is supplied.
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Sprint 8 · S8-T04 per spec §11.4 so third-party containers (Autofac, Lamar, …)
/// can plug in their own resolution strategies via <c>SculptorOptions.ProviderResolver</c>. The
/// default implementation is <see cref="Runtime.DefaultProviderResolver"/>; the MS DI integration
/// ships <c>DependencyInjectionProviderFactory</c> in <c>SmartMapp.Net.DependencyInjection</c>
/// which prefers <c>ActivatorUtilities.CreateInstance</c> for rich constructor injection.
/// </para>
/// <para>
/// Implementations MUST be thread-safe — a single resolver instance is consulted concurrently
/// on every mapping invocation.
/// </para>
/// </remarks>
public interface IProviderResolver
{
    /// <summary>
    /// Resolves an instance of <paramref name="type"/> using the optional
    /// <paramref name="serviceProvider"/>. Implementations typically try DI first and fall
    /// back to activator construction when the type is not registered or no provider is
    /// supplied.
    /// </summary>
    /// <param name="type">The concrete CLR type to resolve. Never <c>null</c>.</param>
    /// <param name="serviceProvider">
    /// The active <see cref="IServiceProvider"/> (request scope, root scope, or <c>null</c>
    /// when <c>Sculptor.Map</c> is invoked outside any container context).
    /// </param>
    /// <returns>A non-null instance of <paramref name="type"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the resolver cannot produce an instance — e.g. <paramref name="type"/> is
    /// abstract and unregistered, or has required constructor dependencies that cannot be
    /// satisfied by activator fallback.
    /// </exception>
    object Resolve(Type type, IServiceProvider? serviceProvider);
}
