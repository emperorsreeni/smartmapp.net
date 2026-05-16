using System.Text.Json.Serialization;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// A directed graph of every registered blueprint. Nodes are .NET types; edges are
/// <see cref="SmartMapp.Net.TypePair"/>s annotated with mapping strategy and link count.
/// Exposed via <c>ISculptor.GetMappingAtlas()</c> and consumed by the Sprint 16 ASP.NET Core
/// diagnostics endpoints — the instance is JSON-serializable via <c>System.Text.Json</c>.
/// </summary>
public sealed record MappingAtlas
{
    /// <summary>
    /// Gets all registered blueprints. Excluded from JSON serialization because
    /// <see cref="Blueprint"/> transitively references <see cref="System.Type"/>,
    /// <see cref="System.Reflection.MemberInfo"/>, and other non-JSON-friendly types.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<Blueprint> Blueprints { get; init; } = Array.Empty<Blueprint>();

    /// <summary>
    /// Gets the distinct participating types (nodes).
    /// </summary>
    public IReadOnlyList<MappingAtlasNode> Nodes { get; init; } = Array.Empty<MappingAtlasNode>();

    /// <summary>
    /// Gets the mapping edges (one per blueprint).
    /// </summary>
    public IReadOnlyList<MappingAtlasEdge> Edges { get; init; } = Array.Empty<MappingAtlasEdge>();

    /// <summary>
    /// Builds a <see cref="MappingAtlas"/> from the supplied blueprints.
    /// </summary>
    /// <param name="blueprints">The blueprints to graph.</param>
    /// <returns>A populated atlas.</returns>
    public static MappingAtlas Build(IReadOnlyList<Blueprint> blueprints)
    {
        if (blueprints is null) throw new ArgumentNullException(nameof(blueprints));

        var nodeByType = new Dictionary<Type, MappingAtlasNode>();
        var edges = new List<MappingAtlasEdge>(blueprints.Count);

        foreach (var bp in blueprints)
        {
            if (!nodeByType.ContainsKey(bp.OriginType))
                nodeByType[bp.OriginType] = new MappingAtlasNode
                {
                    ClrType = bp.OriginType,
                    Label = FormatTypeName(bp.OriginType),
                };

            if (!nodeByType.ContainsKey(bp.TargetType))
                nodeByType[bp.TargetType] = new MappingAtlasNode
                {
                    ClrType = bp.TargetType,
                    Label = FormatTypeName(bp.TargetType),
                };

            edges.Add(new MappingAtlasEdge
            {
                Pair = bp.TypePair,
                Strategy = bp.Strategy,
                LinkCount = bp.Links.Count,
            });
        }

        return new MappingAtlas
        {
            Blueprints = blueprints,
            Nodes = nodeByType.Values.ToArray(),
            Edges = edges,
        };
    }

    /// <summary>
    /// Returns the direct outgoing edges for the specified type.
    /// </summary>
    /// <param name="originType">The origin type.</param>
    /// <returns>Edges whose origin equals <paramref name="originType"/>.</returns>
    public IEnumerable<MappingAtlasEdge> GetOutgoing(Type originType)
    {
        foreach (var e in Edges)
            if (e.Pair.OriginType == originType) yield return e;
    }

    /// <summary>
    /// Returns the direct incoming edges for the specified type.
    /// </summary>
    /// <param name="targetType">The target type.</param>
    /// <returns>Edges whose target equals <paramref name="targetType"/>.</returns>
    public IEnumerable<MappingAtlasEdge> GetIncoming(Type targetType)
    {
        foreach (var e in Edges)
            if (e.Pair.TargetType == targetType) yield return e;
    }

    /// <summary>
    /// Returns the direct neighbour edges for the specified type (in + out).
    /// </summary>
    /// <param name="type">The type to query.</param>
    /// <returns>All edges touching <paramref name="type"/>.</returns>
    public IEnumerable<MappingAtlasEdge> GetNeighbors(Type type)
    {
        foreach (var e in Edges)
            if (e.Pair.OriginType == type || e.Pair.TargetType == type) yield return e;
    }

    /// <summary>
    /// Converts the atlas to Graphviz DOT graph format for visualization.
    /// </summary>
    /// <returns>A string in DOT format.</returns>
    public string ToDotFormat() => DotFormatWriter.Write(Nodes, Edges);

    private static string FormatTypeName(Type type)
    {
        if (!type.IsGenericType) return type.Name;

        var baseName = type.Name;
        var tick = baseName.IndexOf('`');
        if (tick > 0) baseName = baseName.Substring(0, tick);

        var args = type.GetGenericArguments();
        var inner = new string[args.Length];
        for (var i = 0; i < args.Length; i++) inner[i] = FormatTypeName(args[i]);
        return $"{baseName}<{string.Join(", ", inner)}>";
    }
}
