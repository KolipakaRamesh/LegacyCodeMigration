using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeGraphEngine.Graph;

namespace KnowledgeGraphEngine.Storage;

/// <summary>
/// Implements <see cref="IGraphStorage"/> by writing two JSON files:
/// <list type="bullet">
///   <item><description><b>nodes.json</b> — all graph nodes</description></item>
///   <item><description><b>relationships.json</b> — all directed relationships</description></item>
/// </list>
/// This implementation is deliberately thin so the data is easy to consume
/// from any external tool. To add a Neo4j backend, implement <see cref="IGraphStorage"/>
/// in a new class — the rest of the engine remains unchanged.
/// </summary>
public class JsonGraphStorage : IGraphStorage
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented    = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters       = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc/>
    public async Task SaveAsync(SoftwareKnowledgeGraph graph, GraphStorageOptions options)
    {
        Directory.CreateDirectory(options.OutputDirectory);

        var nodesPath = Path.Combine(options.OutputDirectory, options.NodesFileName);
        var relsPath  = Path.Combine(options.OutputDirectory, options.RelationshipsFileName);

        // ── Write nodes ───────────────────────────────────────────────────────
        var nodes = graph.Nodes.Values
            .OrderBy(n => n.Type)
            .ThenBy(n => n.Name)
            .ToList();

        var nodesJson = JsonSerializer.Serialize(nodes, _options);
        await File.WriteAllTextAsync(nodesPath, nodesJson);

        // ── Write relationships ────────────────────────────────────────────────
        var relationships = graph.Relationships
            .OrderBy(r => r.RelationshipType.ToString())
            .ThenBy(r => r.FromNodeId)
            .ToList();

        var relsJson = JsonSerializer.Serialize(relationships, _options);
        await File.WriteAllTextAsync(relsPath, relsJson);

        Console.WriteLine($"    nodes.json         → {nodesPath}  ({nodes.Count} nodes)");
        Console.WriteLine($"    relationships.json  → {relsPath}  ({relationships.Count} relationships)");
    }
}
