namespace KnowledgeGraphEngine.Graph;

/// <summary>
/// Represents a vertex in the Software Knowledge Graph.
/// A node can be a Project, Namespace, Class, Interface, Enum,
/// Method, Constructor, Property, or Field.
/// </summary>
public class GraphNode
{
    /// <summary>Unique stable identifier for this node (e.g., "class:LegacyProject.Services.OrderService").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Short display name (e.g., "OrderService").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Node category — one of the values in <see cref="NodeTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The containing namespace, if applicable.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Flexible dictionary for additional type-specific properties
    /// (e.g., "IsAbstract", "ReturnType", "IsStatic").
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    public override string ToString() => $"[{Type}] {Name}  (id={Id})";
}
