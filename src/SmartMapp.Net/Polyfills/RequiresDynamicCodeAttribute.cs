#if NETSTANDARD2_1
// ReSharper disable once CheckNamespace
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill for <c>RequiresDynamicCodeAttribute</c> on <c>netstandard2.1</c>.
    /// Marks a member as requiring runtime code generation (e.g. <c>System.Reflection.Emit</c>),
    /// which is unsupported under NativeAOT publishing. On <c>net8.0</c>+ the BCL-provided
    /// attribute is used instead and is honoured by the AOT analyzer (IL3050).
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method
            | AttributeTargets.Constructor
            | AttributeTargets.Class
            | AttributeTargets.Struct
            | AttributeTargets.Interface
            | AttributeTargets.Property
            | AttributeTargets.Event,
        Inherited = false,
        AllowMultiple = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        public RequiresDynamicCodeAttribute(string message) => Message = message;
        public string Message { get; }
        public string? Url { get; set; }
    }
}
#endif
