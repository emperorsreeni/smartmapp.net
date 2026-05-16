using System.Reflection;
using System.Reflection.Emit;

namespace SmartMapp.Net.Engine.ILEmit.Internal;

/// <summary>
/// Lightweight extension helpers consumed by the IL emitters in T02–T04. Centralised here so
/// every emitter shares identical op-code selection (e.g. <c>Call</c> vs <c>Callvirt</c>) and so
/// the IL byte-sequence assertions in <c>FlatPropertyEmitterTests</c> have one place to drift.
/// </summary>
internal static class ILGeneratorExtensions
{
    /// <summary>
    /// Emits the cheapest dispatch op-code for <paramref name="method"/>. Uses <c>Call</c> when
    /// the method is non-virtual or declared on a sealed type, otherwise <c>Callvirt</c>.
    /// </summary>
    internal static void EmitCallSmart(this ILGenerator il, MethodInfo method)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));

        var declaring = method.DeclaringType;
        var canSkipCallvirt = method.IsStatic
            || !method.IsVirtual
            || (declaring is not null && (declaring.IsSealed || declaring.IsValueType));

        il.Emit(canSkipCallvirt ? OpCodes.Call : OpCodes.Callvirt, method);
    }

    /// <summary>
    /// Pushes <see langword="default"/> of <paramref name="type"/> on the evaluation stack —
    /// <c>Ldnull</c> for reference types, <c>Initobj</c> via a local for value types.
    /// </summary>
    internal static void EmitDefault(this ILGenerator il, Type type)
    {
        if (!type.IsValueType)
        {
            il.Emit(OpCodes.Ldnull);
            return;
        }

        var local = il.DeclareLocal(type);
        il.Emit(OpCodes.Ldloca, local);
        il.Emit(OpCodes.Initobj, type);
        il.Emit(OpCodes.Ldloc, local);
    }
}
