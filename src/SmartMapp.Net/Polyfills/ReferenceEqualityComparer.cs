#if NETSTANDARD2_1
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace System.Collections.Generic
{
    /// <summary>
    /// Polyfill for <see cref="ReferenceEqualityComparer"/> on netstandard2.1.
    /// Compares objects by reference identity.
    /// </summary>
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object?>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        private ReferenceEqualityComparer() { }

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object? obj) => RuntimeHelpers.GetHashCode(obj!);
    }
}
#endif
