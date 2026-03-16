using System.Reflection;

namespace SmartMapp.Net.Discovery;

/// <summary>
/// Cached reflection wrapper for a property or field member.
/// Pre-computes readability, writability, init-only, and required status.
/// </summary>
public sealed class MemberModel
{
    /// <summary>
    /// Gets the underlying <see cref="System.Reflection.MemberInfo"/> (either <see cref="PropertyInfo"/> or <see cref="FieldInfo"/>).
    /// </summary>
    public MemberInfo MemberInfo { get; }

    /// <summary>
    /// Gets the member name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the return type of the member (property type or field type).
    /// </summary>
    public Type MemberType { get; }

    /// <summary>
    /// Gets a value indicating whether this member can be read (has a getter or is a field).
    /// </summary>
    public bool IsReadable { get; }

    /// <summary>
    /// Gets a value indicating whether this member can be written (has a setter or is a non-readonly field).
    /// </summary>
    public bool IsWritable { get; }

    /// <summary>
    /// Gets a value indicating whether this member has an <c>init</c>-only setter.
    /// </summary>
    public bool IsInitOnly { get; }

    /// <summary>
    /// Gets a value indicating whether this member is marked with the <c>required</c> keyword.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets a value indicating whether this member is a field (as opposed to a property).
    /// </summary>
    public bool IsField { get; }

    /// <summary>
    /// Gets the custom attributes declared on this member.
    /// </summary>
    public IReadOnlyList<Attribute> CustomAttributes { get; }

    internal MemberModel(PropertyInfo property)
    {
        MemberInfo = property;
        Name = property.Name;
        MemberType = property.PropertyType;
        IsReadable = property.CanRead && property.GetMethod!.IsPublic;
        IsField = false;

        var setter = property.SetMethod;
        if (setter is not null && setter.IsPublic)
        {
            IsWritable = true;
            IsInitOnly = IsSetterInitOnly(setter);
        }
        else if (setter is not null && IsSetterInitOnly(setter))
        {
            // init-only setters may not be public but are still writable via init
            IsWritable = true;
            IsInitOnly = true;
        }

        IsRequired = HasRequiredAttribute(property);
        CustomAttributes = Attribute.GetCustomAttributes(property);
    }

    internal MemberModel(FieldInfo field)
    {
        MemberInfo = field;
        Name = field.Name;
        MemberType = field.FieldType;
        IsReadable = true;
        IsWritable = !field.IsInitOnly;
        IsInitOnly = false;
        IsField = true;
        IsRequired = HasRequiredAttribute(field);
        CustomAttributes = Attribute.GetCustomAttributes(field);
    }

    private static bool IsSetterInitOnly(MethodInfo setter)
    {
        // init-only setters have IsExternalInit as a required custom modifier on the return parameter
        var returnParam = setter.ReturnParameter;
        if (returnParam is null) return false;

        var modifiers = returnParam.GetRequiredCustomModifiers();
        foreach (var modifier in modifiers)
        {
            if (modifier.Name == "IsExternalInit")
                return true;
        }
        return false;
    }

    private static bool HasRequiredAttribute(MemberInfo member)
    {
        // C# 11+ 'required' keyword emits RequiredMemberAttribute on the containing type
        // and individual members get it too. Check for the attribute on the member itself.
        var attributes = member.CustomAttributes;
        foreach (var attr in attributes)
        {
            if (attr.AttributeType.Name == "RequiredMemberAttribute"
                && attr.AttributeType.Namespace == "System.Runtime.CompilerServices")
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => $"{MemberType.Name} {Name}";
}
