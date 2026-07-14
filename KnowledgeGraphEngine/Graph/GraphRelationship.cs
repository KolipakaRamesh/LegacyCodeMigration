namespace KnowledgeGraphEngine.Graph;

/// <summary>
/// Represents a directed edge between two nodes in the Software Knowledge Graph.
/// </summary>
public class GraphRelationship
{
    /// <summary>The node ID at the source end of the relationship.</summary>
    public string FromNodeId { get; set; } = string.Empty;

    /// <summary>The node ID at the target end of the relationship.</summary>
    public string ToNodeId { get; set; } = string.Empty;

    /// <summary>The semantic type of this relationship.</summary>
    public RelationshipType RelationshipType { get; set; }

    /// <summary>Optional additional metadata on the relationship itself.</summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    public override string ToString() =>
        $"{FromNodeId}  --[{RelationshipType}]-->  {ToNodeId}";
}
