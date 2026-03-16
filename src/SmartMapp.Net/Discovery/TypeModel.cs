using System.Collections;
using System.Reflection;

namespace SmartMapp.Net.Discovery;

/// <summary>
/// Cached reflection wrapper over <see cref="System.Type"/>. Pre-computes and caches all
/// reflection data needed by conventions, expression compiler, and IL emitter.
/// Immutable after construction — safe to cache and share across threads.
/// </summary>
public sealed class TypeModel
{
    private static readonly BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    private readonly Dictionary<string, MemberModel> _memberLookup;

    /// <summary>
    /// Gets the underlying CLR <see cref="System.Type"/>.
    /// </summary>
    public Type ClrType { get; }

    /// <summary>
    /// Gets all readable members (properties with public getters + public fields).
    /// </summary>
    public IReadOnlyList<MemberModel> ReadableMembers { get; }

    /// <summary>
    /// Gets all writable members (properties with setters including init-only + non-readonly public fields).
    /// </summary>
    public IReadOnlyList<MemberModel> WritableMembers { get; }

    /// <summary>
    /// Gets all public constructors, sorted by parameter count descending.
    /// </summary>
    public IReadOnlyList<ConstructorModel> Constructors { get; }

    /// <summary>
    /// Gets the primary constructor (record positional ctor, only ctor, or best-match ctor).
    /// May be <c>null</c> for interfaces or abstract classes.
    /// </summary>
    public ConstructorModel? PrimaryConstructor { get; }

    /// <summary>
    /// Gets parameterless public methods with non-void return types that look like property getters
    /// (e.g., <c>GetFullName()</c>). Excludes <c>GetType()</c>, <c>GetHashCode()</c>, <c>ToString()</c>.
    /// </summary>
    public IReadOnlyList<MethodModel> ParameterlessValueMethods { get; }

    /// <summary>
    /// Gets a value indicating whether this type is a C# <c>record</c> (class or struct).
    /// Detected via the compiler-generated <c>&lt;Clone&gt;$</c> method.
    /// </summary>
    public bool IsRecord { get; }

    /// <summary>
    /// Gets a value indicating whether this type has a public parameterless constructor.
    /// </summary>
    public bool HasParameterlessConstructor { get; }

    /// <summary>
    /// Gets a value indicating whether this type is abstract.
    /// </summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// Gets a value indicating whether this type is an interface.
    /// </summary>
    public bool IsInterface { get; }

    /// <summary>
    /// Gets a value indicating whether this type is a generic type.
    /// </summary>
    public bool IsGenericType { get; }

    /// <summary>
    /// Gets a value indicating whether this type implements <see cref="IEnumerable"/> (excluding <see cref="string"/>).
    /// </summary>
    public bool IsCollection { get; }

    /// <summary>
    /// Gets a value indicating whether this type implements <c>IDictionary&lt;,&gt;</c>.
    /// </summary>
    public bool IsDictionary { get; }

    /// <summary>
    /// Gets a value indicating whether this type is <see cref="Nullable{T}"/>.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets the underlying type <c>T</c> from <see cref="Nullable{T}"/>, or <c>null</c> if not nullable.
    /// </summary>
    public Type? UnderlyingNullableType { get; }

    /// <summary>
    /// Gets the element type <c>T</c> from <c>IEnumerable&lt;T&gt;</c>, or <c>null</c> if not a generic collection.
    /// </summary>
    public Type? CollectionElementType { get; }

    /// <summary>
    /// Gets the key type from <c>IDictionary&lt;TKey, TValue&gt;</c>, or <c>null</c> if not a dictionary.
    /// </summary>
    public Type? DictionaryKeyType { get; }

    /// <summary>
    /// Gets the value type from <c>IDictionary&lt;TKey, TValue&gt;</c>, or <c>null</c> if not a dictionary.
    /// </summary>
    public Type? DictionaryValueType { get; }

    /// <summary>
    /// Gets the inheritance chain from this type up to (excluding) <see cref="object"/>.
    /// </summary>
    public IReadOnlyList<Type> InheritanceChain { get; }

    /// <summary>
    /// Gets all interfaces implemented by this type.
    /// </summary>
    public IReadOnlyList<Type> ImplementedInterfaces { get; }

    /// <summary>
    /// Initializes a new <see cref="TypeModel"/> by reflecting on the specified <paramref name="type"/>.
    /// All reflection is performed eagerly during construction.
    /// </summary>
    /// <param name="type">The CLR type to analyze.</param>
    public TypeModel(Type type)
    {
        ClrType = type;
        IsAbstract = type.IsAbstract;
        IsInterface = type.IsInterface;
        IsGenericType = type.IsGenericType;

        // Record detection: records have a compiler-generated <Clone>$ method
        IsRecord = type.GetMethod("<Clone>$", PublicInstance) is not null;

        // Nullable detection
        IsNullable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        UnderlyingNullableType = IsNullable ? Nullable.GetUnderlyingType(type) : null;

        // Members
        var readableMembers = new List<MemberModel>();
        var writableMembers = new List<MemberModel>();

        foreach (var prop in type.GetProperties(PublicInstance))
        {
            var model = new MemberModel(prop);
            if (model.IsReadable) readableMembers.Add(model);
            if (model.IsWritable) writableMembers.Add(model);
        }

        foreach (var field in type.GetFields(PublicInstance))
        {
            if (field.IsSpecialName) continue;
            var model = new MemberModel(field);
            if (model.IsReadable) readableMembers.Add(model);
            if (model.IsWritable) writableMembers.Add(model);
        }

        ReadableMembers = readableMembers;
        WritableMembers = writableMembers;

        // Build case-insensitive lookup
        _memberLookup = new Dictionary<string, MemberModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in readableMembers)
        {
            _memberLookup.TryAdd(m.Name, m);
        }
        foreach (var m in writableMembers)
        {
            _memberLookup.TryAdd(m.Name, m);
        }

        // Constructors — sorted by parameter count descending
        var ctors = type.GetConstructors(PublicInstance)
            .Select(c => new ConstructorModel(c))
            .OrderByDescending(c => c.ParameterCount)
            .ToList();

        Constructors = ctors;
        HasParameterlessConstructor = ctors.Any(c => c.ParameterCount == 0);

        // Primary constructor detection
        PrimaryConstructor = DetectPrimaryConstructor(ctors);

        // Parameterless value methods (Get*() with no params and non-void return)
        var valueMethods = new List<MethodModel>();
        var excludedMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "GetType", "GetHashCode", "ToString", "GetEnumerator"
        };

        foreach (var method in type.GetMethods(PublicInstance))
        {
            if (method.IsSpecialName) continue;
            if (method.GetParameters().Length != 0) continue;
            if (method.ReturnType == typeof(void)) continue;
            if (excludedMethods.Contains(method.Name)) continue;
            if (method.DeclaringType == typeof(object)) continue;

            valueMethods.Add(new MethodModel(method));
        }

        ParameterlessValueMethods = valueMethods;

        // Collection/Dictionary detection
        ImplementedInterfaces = type.GetInterfaces();

        if (type != typeof(string))
        {
            // Check if the type itself is a generic IEnumerable<T> or IDictionary<K,V>
            if (type.IsGenericType)
            {
                var typeDef = type.GetGenericTypeDefinition();
                if (typeDef == typeof(IDictionary<,>))
                {
                    IsDictionary = true;
                    var args = type.GetGenericArguments();
                    DictionaryKeyType = args[0];
                    DictionaryValueType = args[1];
                }
                else if (typeDef == typeof(IEnumerable<>))
                {
                    CollectionElementType = type.GetGenericArguments()[0];
                }
            }

            foreach (var iface in ImplementedInterfaces)
            {
                if (iface.IsGenericType)
                {
                    var def = iface.GetGenericTypeDefinition();
                    if (def == typeof(IDictionary<,>) && !IsDictionary)
                    {
                        IsDictionary = true;
                        var args = iface.GetGenericArguments();
                        DictionaryKeyType = args[0];
                        DictionaryValueType = args[1];
                    }
                    else if (def == typeof(IEnumerable<>) && CollectionElementType is null)
                    {
                        CollectionElementType = iface.GetGenericArguments()[0];
                    }
                }
            }

            IsCollection = CollectionElementType is not null || typeof(IEnumerable).IsAssignableFrom(type);

            // For arrays, extract element type directly
            if (type.IsArray && CollectionElementType is null)
            {
                CollectionElementType = type.GetElementType();
                IsCollection = true;
            }
        }

        // Inheritance chain (excluding object)
        var chain = new List<Type>();
        var current = type;
        while (current is not null && current != typeof(object))
        {
            chain.Add(current);
            current = current.BaseType;
        }
        InheritanceChain = chain;
    }

    /// <summary>
    /// Looks up a member by name (case-insensitive).
    /// </summary>
    /// <param name="name">The member name to look up.</param>
    /// <returns>The matching <see cref="MemberModel"/>, or <c>null</c> if not found.</returns>
    public MemberModel? GetMember(string name)
    {
        _memberLookup.TryGetValue(name, out var member);
        return member;
    }

    /// <summary>
    /// Resolves a compound member path such as <c>"CustomerAddressCity"</c> into a chain
    /// of <see cref="MemberModel"/> instances by greedily matching property names from left to right.
    /// Used by the flattening convention in Sprint 2.
    /// </summary>
    /// <param name="compoundName">The compound member name (e.g., "CustomerAddressCity").</param>
    /// <param name="cacheProvider">
    /// An optional function that returns a <see cref="TypeModel"/> for a given <see cref="Type"/>.
    /// When <c>null</c>, a new <see cref="TypeModel"/> is constructed inline (useful for tests).
    /// </param>
    /// <returns>
    /// An ordered list of <see cref="MemberModel"/> representing the navigation path,
    /// or an empty list if the path cannot be fully resolved.
    /// </returns>
    public IReadOnlyList<MemberModel> GetMemberPath(string compoundName, Func<Type, TypeModel>? cacheProvider = null)
    {
        var result = new List<MemberModel>();
        var remaining = compoundName;
        var currentModel = this;

        while (remaining.Length > 0)
        {
            MemberModel? bestMatch = null;
            var bestLength = 0;

            // Greedy: try longest prefix first
            foreach (var member in currentModel.ReadableMembers)
            {
                if (remaining.StartsWith(member.Name, StringComparison.OrdinalIgnoreCase)
                    && member.Name.Length > bestLength)
                {
                    bestMatch = member;
                    bestLength = member.Name.Length;
                }
            }

            if (bestMatch is null)
                return Array.Empty<MemberModel>();

            result.Add(bestMatch);
            remaining = remaining.Substring(bestLength);

            if (remaining.Length > 0)
            {
                // Navigate into the member's type
                currentModel = cacheProvider is not null
                    ? cacheProvider(bestMatch.MemberType)
                    : new TypeModel(bestMatch.MemberType);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => $"TypeModel({ClrType.Name})";

    private ConstructorModel? DetectPrimaryConstructor(List<ConstructorModel> ctors)
    {
        if (ctors.Count == 0) return null;
        if (ctors.Count == 1) { ctors[0].IsPrimary = true; return ctors[0]; }

        // For records: the primary ctor typically has parameters matching the readable members
        if (IsRecord)
        {
            // The record primary constructor is the one with the most parameters that match property names
            foreach (var ctor in ctors)
            {
                var paramNames = ctor.Parameters.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var readableNames = ReadableMembers.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (paramNames.Count > 0 && paramNames.IsSubsetOf(readableNames))
                {
                    ctor.IsPrimary = true;
                    return ctor;
                }
            }
        }

        // Fallback: prefer the constructor with the most parameters
        var primary = ctors[0]; // already sorted descending by param count
        primary.IsPrimary = true;
        return primary;
    }
}
