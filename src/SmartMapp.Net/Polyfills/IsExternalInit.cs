#if NETSTANDARD2_1
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill to enable init-only setters on netstandard2.1.
    /// </summary>
    internal static class IsExternalInit { }
}
#endif
