using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Collections;

/// <summary>
/// Central dispatcher for collection mapping. Detects source/target collection types,
/// selects the appropriate mapping strategy, and builds expression tree fragments
/// that perform the collection mapping at runtime.
/// </summary>
internal static class CollectionMapper
{
    /// <summary>
    /// Builds an expression that maps a source collection expression to the target collection type.
    /// </summary>
    /// <param name="sourceExpr">Expression producing the source collection value.</param>
    /// <param name="sourceType">The CLR type of the source collection.</param>
    /// <param name="targetType">The CLR type of the target collection.</param>
    /// <param name="scopeParam">The <see cref="MappingScope"/> parameter expression.</param>
    /// <param name="elementMapper">
    /// Delegate that builds a nested mapping expression for a single element.
    /// Signature: (originExpr, originType, targetType, scopeParam) → mappedExpr.
    /// </param>
    /// <returns>An expression that produces the mapped target collection.</returns>
    internal static Expression BuildCollectionExpression(
        Expression sourceExpr,
        Type sourceType,
        Type targetType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper)
    {
        var targetCategory = CollectionCategoryResolver.Resolve(targetType);

        // Dictionary mapping
        if (targetCategory == CollectionCategory.Dictionary)
            return BuildDictionaryMapping(sourceExpr, sourceType, targetType, scopeParam, elementMapper);

        // Element types
        var originElementType = CollectionExpressionHelpers.GetGenericElementType(sourceType);
        var targetElementType = CollectionExpressionHelpers.GetGenericElementType(targetType);

        if (originElementType is null || targetElementType is null)
        {
            throw new MappingCompilationException(
                $"Cannot determine element types for collection mapping '{sourceType.Name}' → '{targetType.Name}'.");
        }

        var needsElementMapping = !targetElementType.IsAssignableFrom(originElementType);

        Expression body = targetCategory switch
        {
            CollectionCategory.Array => BuildArrayMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.List => BuildListMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.Enumerable => BuildListMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.Collection => BuildListMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.ReadOnlyList => BuildListMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.ReadOnlyCollection => BuildListMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.HashSet => BuildHashSetMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.ImmutableList => BuildImmutableListMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.ImmutableArray => BuildImmutableArrayMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.ObservableCollection => BuildObservableCollectionMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            CollectionCategory.ReadOnlyCollectionConcrete => BuildReadOnlyCollectionMapping(
                sourceExpr, sourceType, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping),
            _ => throw new MappingCompilationException(
                $"Unsupported target collection category '{targetCategory}' for type '{targetType.Name}'.")
        };

        // Cast to the exact target type if needed (e.g., List<T> → IReadOnlyList<T>)
        if (body.Type != targetType && targetType.IsAssignableFrom(body.Type))
            body = Expression.Convert(body, targetType);

        return CollectionExpressionHelpers.WrapWithNullCheck(sourceExpr, body, targetType);
    }

    // ──────────────────────── Array ────────────────────────

    private static Expression BuildArrayMapping(
        Expression sourceExpr, Type sourceType,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        var targetArrayType = targetElementType.MakeArrayType();

        // Fast-path: same element type → Array.Copy
        if (!needsElementMapping && sourceType.IsArray)
        {
            return BuildArrayCopyFastPath(sourceExpr, originElementType);
        }

        // Check if the source has a Count/Length property for pre-allocation
        if (HasCountProperty(sourceType))
        {
            var countExpr = CollectionExpressionHelpers.GetCountExpression(sourceExpr);
            var resultVar = Expression.Variable(targetArrayType, "result");
            var allocate = Expression.Assign(resultVar, Expression.NewArrayBounds(targetElementType, countExpr));

            Expression loopBody;
            if (CanUseIndexedAccess(sourceType))
            {
                // Indexed access: source[i]
                loopBody = CollectionExpressionHelpers.BuildForLoop(countExpr, indexVar =>
                {
                    var sourceElement = sourceType.IsArray
                        ? Expression.ArrayIndex(sourceExpr, indexVar)
                        : (Expression)Expression.Property(sourceExpr, "Item", indexVar);
                    var mapped = needsElementMapping
                        ? CollectionExpressionHelpers.BuildElementMappingCall(sourceElement, originElementType, targetElementType, scopeParam, elementMapper)
                        : (Expression)Expression.Convert(sourceElement, targetElementType);
                    var targetElement = Expression.ArrayAccess(resultVar, indexVar);
                    return Expression.Assign(targetElement, mapped);
                });
            }
            else
            {
                // Foreach enumeration with index counter
                var indexVar = Expression.Variable(typeof(int), "idx");
                loopBody = Expression.Block(
                    new[] { indexVar },
                    Expression.Assign(indexVar, Expression.Constant(0)),
                    CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
                    {
                        var mapped = needsElementMapping
                            ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                            : (Expression)Expression.Convert(currentExpr, targetElementType);
                        return Expression.Block(
                            Expression.Assign(Expression.ArrayAccess(resultVar, indexVar), mapped),
                            Expression.PostIncrementAssign(indexVar));
                    }));
            }

            return Expression.Block(
                new[] { resultVar },
                allocate,
                loopBody,
                resultVar);
        }

        // No Count available (e.g., IEnumerable<T>): materialize to List<T> then call .ToArray()
        return BuildListThenToArray(sourceExpr, originElementType, targetElementType, scopeParam, elementMapper, needsElementMapping);
    }

    private static Expression BuildListThenToArray(
        Expression sourceExpr,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        // Materialize to List<T> via foreach, then call .ToArray()
        var listType = typeof(List<>).MakeGenericType(targetElementType);
        var listVar = Expression.Variable(listType, "tempList");
        var addMethod = listType.GetMethod("Add")!;
        var toArrayMethod = listType.GetMethod("ToArray")!;

        var allocate = Expression.Assign(listVar, Expression.New(listType));

        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
        {
            var mapped = needsElementMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                : EnsureType(currentExpr, targetElementType);
            return Expression.Call(listVar, addMethod, mapped);
        });

        return Expression.Block(
            new[] { listVar },
            allocate,
            loop,
            Expression.Call(listVar, toArrayMethod));
    }

    private static Expression BuildArrayCopyFastPath(Expression sourceExpr, Type elementType)
    {
        var lengthExpr = Expression.ArrayLength(sourceExpr);
        var targetArrayType = elementType.MakeArrayType();
        var resultVar = Expression.Variable(targetArrayType, "result");
        var allocate = Expression.Assign(resultVar, Expression.NewArrayBounds(elementType, lengthExpr));

        var arrayCopyMethod = typeof(Array).GetMethod("Copy", new[] { typeof(Array), typeof(Array), typeof(int) })!;
        var copy = Expression.Call(arrayCopyMethod, sourceExpr, resultVar, lengthExpr);

        return Expression.Block(
            new[] { resultVar },
            allocate,
            copy,
            resultVar);
    }

    // ──────────────────────── List ────────────────────────

    private static Expression BuildListMapping(
        Expression sourceExpr, Type sourceType,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        var listType = typeof(List<>).MakeGenericType(targetElementType);
        var resultVar = Expression.Variable(listType, "result");
        var addMethod = listType.GetMethod("Add")!;

        // Pre-sized allocation
        Expression allocate;
        if (HasCountProperty(sourceType))
        {
            var countExpr = CollectionExpressionHelpers.GetCountExpression(sourceExpr);
            var ctor = listType.GetConstructor(new[] { typeof(int) })!;
            allocate = Expression.Assign(resultVar, Expression.New(ctor, countExpr));
        }
        else
        {
            var ctor = listType.GetConstructor(Type.EmptyTypes)!;
            allocate = Expression.Assign(resultVar, Expression.New(ctor));
        }

        // Populate via foreach
        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
        {
            var mapped = needsElementMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                : EnsureType(currentExpr, targetElementType);
            return Expression.Call(resultVar, addMethod, mapped);
        });

        return Expression.Block(
            new[] { resultVar },
            allocate,
            loop,
            resultVar);
    }

    // ──────────────────────── HashSet ────────────────────────

    private static Expression BuildHashSetMapping(
        Expression sourceExpr, Type sourceType,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        var setType = typeof(HashSet<>).MakeGenericType(targetElementType);
        var resultVar = Expression.Variable(setType, "result");
        var addMethod = setType.GetMethod("Add")!;

        // Pre-sized allocation (capacity ctor available .NET Standard 2.1+)
        Expression allocate;
        if (HasCountProperty(sourceType))
        {
            var countExpr = CollectionExpressionHelpers.GetCountExpression(sourceExpr);
            var ctor = setType.GetConstructor(new[] { typeof(int) });
            allocate = ctor is not null
                ? Expression.Assign(resultVar, Expression.New(ctor, countExpr))
                : Expression.Assign(resultVar, Expression.New(setType));
        }
        else
        {
            allocate = Expression.Assign(resultVar, Expression.New(setType));
        }

        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
        {
            var mapped = needsElementMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                : EnsureType(currentExpr, targetElementType);
            return Expression.Call(resultVar, addMethod, mapped);
        });

        return Expression.Block(
            new[] { resultVar },
            allocate,
            loop,
            resultVar);
    }

    // ──────────────────────── Dictionary ────────────────────────

    private static Expression BuildDictionaryMapping(
        Expression sourceExpr, Type sourceType, Type targetType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper)
    {
        var sourceKv = CollectionExpressionHelpers.GetDictionaryTypes(sourceType);
        var targetKv = CollectionExpressionHelpers.GetDictionaryTypes(targetType);

        if (sourceKv is null || targetKv is null)
        {
            throw new MappingCompilationException(
                $"Cannot determine key/value types for dictionary mapping '{sourceType.Name}' → '{targetType.Name}'.");
        }

        var (sourceKeyType, sourceValueType) = sourceKv.Value;
        var (targetKeyType, targetValueType) = targetKv.Value;

        var dictType = typeof(Dictionary<,>).MakeGenericType(targetKeyType, targetValueType);
        var resultVar = Expression.Variable(dictType, "result");

        // Pre-sized allocation
        Expression allocate;
        if (HasCountProperty(sourceType))
        {
            var countExpr = CollectionExpressionHelpers.GetCountExpression(sourceExpr);
            var ctor = dictType.GetConstructor(new[] { typeof(int) })!;
            allocate = Expression.Assign(resultVar, Expression.New(ctor, countExpr));
        }
        else
        {
            allocate = Expression.Assign(resultVar, Expression.New(dictType));
        }

        // Add method
        var addMethod = dictType.GetMethod("Add", new[] { targetKeyType, targetValueType })!;

        // Iterate source as IEnumerable<KeyValuePair<K,V>>
        var kvpType = typeof(KeyValuePair<,>).MakeGenericType(sourceKeyType, sourceValueType);
        var keyProp = kvpType.GetProperty("Key")!;
        var valueProp = kvpType.GetProperty("Value")!;

        var needsKeyMapping = !targetKeyType.IsAssignableFrom(sourceKeyType);
        var needsValueMapping = !targetValueType.IsAssignableFrom(sourceValueType);

        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, kvpType, currentExpr =>
        {
            var keyExpr = Expression.Property(currentExpr, keyProp);
            var valueExpr = Expression.Property(currentExpr, valueProp);

            Expression mappedKey = needsKeyMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(keyExpr, sourceKeyType, targetKeyType, scopeParam, elementMapper)
                : EnsureType(keyExpr, targetKeyType);

            Expression mappedValue = needsValueMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(valueExpr, sourceValueType, targetValueType, scopeParam, elementMapper)
                : EnsureType(valueExpr, targetValueType);

            return Expression.Call(resultVar, addMethod, mappedKey, mappedValue);
        });

        var body = Expression.Block(
            new[] { resultVar },
            allocate,
            loop,
            resultVar);

        // Cast if target is interface (IDictionary, IReadOnlyDictionary)
        Expression result = body;
        if (result.Type != targetType && targetType.IsAssignableFrom(result.Type))
            result = Expression.Block(
                new[] { resultVar },
                allocate,
                loop,
                Expression.Convert(resultVar, targetType));

        return CollectionExpressionHelpers.WrapWithNullCheck(sourceExpr, body, targetType);
    }

    // ──────────────────────── ImmutableList ────────────────────────

    private static Expression BuildImmutableListMapping(
        Expression sourceExpr, Type sourceType,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        // Use ImmutableList.CreateBuilder<T>() — get the closed builder type from the method return type
        var createBuilderMethod = typeof(ImmutableList).GetMethod("CreateBuilder")!
            .MakeGenericMethod(targetElementType);
        var builderType = createBuilderMethod.ReturnType;
        var addMethod = builderType.GetMethod("Add")!;
        var toImmutableMethod = builderType.GetMethod("ToImmutable")!;

        var builderVar = Expression.Variable(builderType, "builder");
        var createBuilder = Expression.Assign(builderVar, Expression.Call(createBuilderMethod));

        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
        {
            var mapped = needsElementMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                : EnsureType(currentExpr, targetElementType);
            return Expression.Call(builderVar, addMethod, mapped);
        });

        var toImmutable = Expression.Call(builderVar, toImmutableMethod);

        return Expression.Block(
            new[] { builderVar },
            createBuilder,
            loop,
            toImmutable);
    }

    // ──────────────────────── ImmutableArray ────────────────────────

    private static Expression BuildImmutableArrayMapping(
        Expression sourceExpr, Type sourceType,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        // Use ImmutableArray.CreateBuilder<T>() — get the closed builder type from the method return type
        var createBuilderMethod = typeof(ImmutableArray).GetMethods()
            .First(m => m.Name == "CreateBuilder" && m.GetParameters().Length == 0)
            .MakeGenericMethod(targetElementType);

        var builderType = createBuilderMethod.ReturnType;
        var addMethod = builderType.GetMethod("Add")!;
        var toImmutableMethod = builderType.GetMethod("ToImmutable")!;

        var builderVar = Expression.Variable(builderType, "builder");
        var createBuilder = Expression.Assign(builderVar, Expression.Call(createBuilderMethod));

        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
        {
            var mapped = needsElementMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                : EnsureType(currentExpr, targetElementType);
            return Expression.Call(builderVar, addMethod, mapped);
        });

        var toImmutable = Expression.Call(builderVar, toImmutableMethod);

        return Expression.Block(
            new[] { builderVar },
            createBuilder,
            loop,
            toImmutable);
    }

    // ──────────────────────── ObservableCollection ────────────────────────

    private static Expression BuildObservableCollectionMapping(
        Expression sourceExpr, Type sourceType,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        var ocType = typeof(ObservableCollection<>).MakeGenericType(targetElementType);
        var resultVar = Expression.Variable(ocType, "result");
        var ctor = ocType.GetConstructor(Type.EmptyTypes)!;
        var addMethod = ocType.GetMethod("Add")!;

        var allocate = Expression.Assign(resultVar, Expression.New(ctor));

        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
        {
            var mapped = needsElementMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                : EnsureType(currentExpr, targetElementType);
            return Expression.Call(resultVar, addMethod, mapped);
        });

        return Expression.Block(
            new[] { resultVar },
            allocate,
            loop,
            resultVar);
    }

    // ──────────────────────── ReadOnlyCollection ────────────────────────

    private static Expression BuildReadOnlyCollectionMapping(
        Expression sourceExpr, Type sourceType,
        Type originElementType, Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper,
        bool needsElementMapping)
    {
        // Build a List<T> first, then wrap in ReadOnlyCollection<T>
        var listType = typeof(List<>).MakeGenericType(targetElementType);
        var listVar = Expression.Variable(listType, "innerList");
        var addMethod = listType.GetMethod("Add")!;

        Expression listAllocate;
        if (HasCountProperty(sourceType))
        {
            var countExpr = CollectionExpressionHelpers.GetCountExpression(sourceExpr);
            var ctor = listType.GetConstructor(new[] { typeof(int) })!;
            listAllocate = Expression.Assign(listVar, Expression.New(ctor, countExpr));
        }
        else
        {
            listAllocate = Expression.Assign(listVar, Expression.New(listType));
        }

        var loop = CollectionExpressionHelpers.BuildForEachLoop(sourceExpr, originElementType, currentExpr =>
        {
            var mapped = needsElementMapping
                ? CollectionExpressionHelpers.BuildElementMappingCall(currentExpr, originElementType, targetElementType, scopeParam, elementMapper)
                : EnsureType(currentExpr, targetElementType);
            return Expression.Call(listVar, addMethod, mapped);
        });

        var rocType = typeof(ReadOnlyCollection<>).MakeGenericType(targetElementType);
        var ilistType = typeof(IList<>).MakeGenericType(targetElementType);
        var rocCtor = rocType.GetConstructor(new[] { ilistType })!;
        var wrapExpr = Expression.New(rocCtor, Expression.Convert(listVar, ilistType));

        return Expression.Block(
            new[] { listVar },
            listAllocate,
            loop,
            wrapExpr);
    }

    // ──────────────────────── Helpers ────────────────────────

    private static bool CanUseIndexedAccess(Type type)
    {
        if (type.IsArray) return true;
        // List<T> and IList<T> have indexers
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(IReadOnlyList<>))
                return true;
        }
        return type.GetProperty("Item", new[] { typeof(int) }) is not null;
    }

    private static bool HasCountProperty(Type type)
    {
        if (type.IsArray) return true;
        return type.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance) is not null
            || ImplementsCollectionInterface(type);
    }

    private static bool ImplementsCollectionInterface(Type type)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType)
            {
                var def = iface.GetGenericTypeDefinition();
                if (def == typeof(ICollection<>) || def == typeof(IReadOnlyCollection<>))
                    return true;
            }
        }
        return false;
    }

    private static Expression EnsureType(Expression expr, Type targetType)
    {
        if (expr.Type == targetType) return expr;
        return Expression.Convert(expr, targetType);
    }
}
