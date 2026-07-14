namespace KnowledgeGraphEngine.Storage;

/// <summary>
/// Configuration options for the graph storage layer.
/// </summary>
public class GraphStorageOptions
{
    /// <summary>Absolute or relative path to the directory where files will be written.</summary>
    public string OutputDirectory { get; set; } = "output";

    /// <summary>Name of the nodes JSON file.</summary>
    public string NodesFileName { get; set; } = "nodes.json";

    /// <summary>Name of the relationships JSON file.</summary>
    public string RelationshipsFileName { get; set; } = "relationships.json";

    /// <summary>Whether to pretty-print the JSON output.</summary>
    public bool Indented { get; set; } = true;
}
