using KnowledgeGraphEngine.Graph;

namespace KnowledgeGraphEngine.Storage;

/// <summary>
/// Defines the contract for persisting a <see cref="SoftwareKnowledgeGraph"/>.
/// Implement this interface to add Neo4j, GraphML, Mermaid, or any other backend.
/// </summary>
public interface IGraphStorage
{
    /// <summary>
    /// Persists the entire graph using the given options.
    /// </summary>
    /// <param name="graph">The graph to persist.</param>
    /// <param name="options">Storage-specific configuration (e.g., output path).</param>
    Task SaveAsync(SoftwareKnowledgeGraph graph, GraphStorageOptions options);
}
