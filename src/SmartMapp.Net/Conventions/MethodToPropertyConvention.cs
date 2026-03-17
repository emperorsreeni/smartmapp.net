using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Convention that links target properties to parameterless origin methods that look like
/// property getters (e.g., <c>GetFullName()</c> → <c>FullName</c>).
/// Uses <see cref="TypeModel.ParameterlessValueMethods"/> from Sprint 1.
/// </summary>
public sealed class MethodToPropertyConvention : IPropertyConvention
{
    /// <inheritdoc />
    public int Priority => 275;

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        var targetName = targetMember.Name;
        var methods = originModel.ParameterlessValueMethods;

        for (var i = 0; i < methods.Count; i++)
        {
            var method = methods[i];

            // Match inferred property name (e.g., "GetFullName" → "FullName")
            if (string.Equals(method.InferredPropertyName, targetName, StringComparison.OrdinalIgnoreCase))
            {
                provider = new MethodAccessProvider(method.MethodInfo);
                return true;
            }

            // Also match exact method name (e.g., method "FullName()" → target "FullName")
            if (string.Equals(method.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                provider = new MethodAccessProvider(method.MethodInfo);
                return true;
            }
        }

        provider = null;
        return false;
    }
}
