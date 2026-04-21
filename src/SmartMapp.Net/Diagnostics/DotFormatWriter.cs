using System.Text;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Internal writer that emits a <see cref="MappingAtlas"/> in Graphviz DOT format.
/// Properly escapes type names containing generic brackets, commas, and quotes.
/// </summary>
internal static class DotFormatWriter
{
    internal static string Write(
        IReadOnlyList<MappingAtlasNode> nodes,
        IReadOnlyList<MappingAtlasEdge> edges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph SmartMappNet {");
        sb.AppendLine("  rankdir=LR;");
        sb.AppendLine("  node [shape=box, fontname=\"Consolas\"];");

        foreach (var node in nodes)
        {
            sb.Append("  ").Append(Quote(NodeId(node.ClrType)))
              .Append(" [label=").Append(Quote(node.Label)).AppendLine("];");
        }

        foreach (var edge in edges)
        {
            var label = $"{edge.Strategy}, {edge.LinkCount} link" + (edge.LinkCount == 1 ? string.Empty : "s");
            sb.Append("  ")
              .Append(Quote(NodeId(edge.Pair.OriginType)))
              .Append(" -> ")
              .Append(Quote(NodeId(edge.Pair.TargetType)))
              .Append(" [label=").Append(Quote(label)).AppendLine("];");
        }

        sb.Append("}");
        return sb.ToString();
    }

    private static string NodeId(Type type) => type.FullName ?? type.Name;

    private static string Quote(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + escaped + "\"";
    }
}
