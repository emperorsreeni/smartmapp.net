using System.Reflection.Emit;

namespace SmartMapp.Net.Engine.ILEmit.Internal;

/// <summary>
/// Centralised coercion table consulted by <see cref="EmitDiagnostics"/> (T00 capability probe)
/// and by the IL emitters in T02–T04. Encodes the subset of <c>(originType, targetType)</c>
/// pairs the Sprint 9 emit-fast-path can lower without delegating to the Expression Compiler.
/// </summary>
internal static class TypeCoercion
{
    /// <summary>
    /// Returns <c>true</c> when an assignment of a value of type
    /// <paramref name="origin"/> to a slot of type <paramref name="target"/> can be lowered to
    /// IL by the Sprint 9 emit pipeline.
    /// </summary>
    /// <param name="origin">The runtime type of the value being assigned.</param>
    /// <param name="target">The slot type receiving the value.</param>
    /// <param name="hasTransformer">When <c>true</c>, a registered transformer will bridge the
    /// pair so the coercion gate is bypassed (T04 transformer emit handles the conversion
    /// itself).</param>
    internal static bool IsCoercible(Type origin, Type target, bool hasTransformer)
    {
        if (hasTransformer) return true;
        if (origin == target) return true;

        // Nullable<T> -> T  (HasValue/GetValueOrDefault unwrap).
        var originUnderlying = Nullable.GetUnderlyingType(origin);
        if (originUnderlying is not null && originUnderlying == target) return true;

        // T -> Nullable<T>  (wrap via ctor).
        var targetUnderlying = Nullable.GetUnderlyingType(target);
        if (targetUnderlying is not null && targetUnderlying == origin) return true;

        // Reference identity / inheritance (covariant assignment).
        if (!origin.IsValueType && !target.IsValueType && target.IsAssignableFrom(origin))
            return true;

        // Widening numeric (no overflow check, mirrors C# implicit conversions).
        if (IsWideningNumeric(origin, target)) return true;

        return false;
    }

    /// <summary>
    /// Emits the IL op-code that widens a value of <paramref name="origin"/> on top of the
    /// evaluation stack to a value of <paramref name="target"/>. The caller is responsible for
    /// ensuring <see cref="IsCoercible"/> reported <c>true</c> for the pair.
    /// </summary>
    internal static void EmitWideningConversion(ILGenerator il, Type origin, Type target)
    {
        if (origin == target) return;

        var to = Type.GetTypeCode(target);
        switch (to)
        {
            case TypeCode.Int16: il.Emit(OpCodes.Conv_I2); return;
            case TypeCode.Int32: il.Emit(OpCodes.Conv_I4); return;
            case TypeCode.Int64: il.Emit(OpCodes.Conv_I8); return;
            case TypeCode.UInt16: il.Emit(OpCodes.Conv_U2); return;
            case TypeCode.UInt32: il.Emit(OpCodes.Conv_U4); return;
            case TypeCode.UInt64: il.Emit(OpCodes.Conv_U8); return;
            case TypeCode.Single: il.Emit(OpCodes.Conv_R4); return;
            case TypeCode.Double: il.Emit(OpCodes.Conv_R8); return;
        }
    }

    private static bool IsWideningNumeric(Type origin, Type target)
    {
        var from = Type.GetTypeCode(origin);
        var to = Type.GetTypeCode(target);

        // Order-preserving widening lattice per ECMA-335 / C# implicit conversion table.
        return (from, to) switch
        {
            (TypeCode.SByte, TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Byte, TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Int16, TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.UInt16, TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Int32, TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.UInt32, TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Int64, TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.UInt64, TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Char, TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal) => true,
            (TypeCode.Single, TypeCode.Double) => true,
            _ => false,
        };
    }
}
