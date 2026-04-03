namespace SmartMapp.Net.Collections;

/// <summary>
/// Classifies the kind of collection a type represents, used by <see cref="CollectionMapper"/>
/// to dispatch to the correct mapping handler.
/// </summary>
internal enum CollectionCategory
{
    /// <summary>Not a recognized collection type.</summary>
    Unknown = 0,

    /// <summary><c>T[]</c> — array of elements.</summary>
    Array,

    /// <summary><c>List&lt;T&gt;</c> or <c>IList&lt;T&gt;</c>.</summary>
    List,

    /// <summary><c>IEnumerable&lt;T&gt;</c> — deferred or materialized sequence.</summary>
    Enumerable,

    /// <summary><c>ICollection&lt;T&gt;</c>.</summary>
    Collection,

    /// <summary><c>IReadOnlyList&lt;T&gt;</c>.</summary>
    ReadOnlyList,

    /// <summary><c>IReadOnlyCollection&lt;T&gt;</c>.</summary>
    ReadOnlyCollection,

    /// <summary><c>HashSet&lt;T&gt;</c> or <c>ISet&lt;T&gt;</c>.</summary>
    HashSet,

    /// <summary><c>Dictionary&lt;K,V&gt;</c>, <c>IDictionary&lt;K,V&gt;</c>, or <c>IReadOnlyDictionary&lt;K,V&gt;</c>.</summary>
    Dictionary,

    /// <summary><c>ImmutableList&lt;T&gt;</c> or <c>IImmutableList&lt;T&gt;</c>.</summary>
    ImmutableList,

    /// <summary><c>ImmutableArray&lt;T&gt;</c>.</summary>
    ImmutableArray,

    /// <summary><c>ObservableCollection&lt;T&gt;</c>.</summary>
    ObservableCollection,

    /// <summary><c>ReadOnlyCollection&lt;T&gt;</c> (concrete <c>System.Collections.ObjectModel</c>).</summary>
    ReadOnlyCollectionConcrete,
}
