namespace KnowledgeGraphEngine.Graph;

/// <summary>
/// The in-memory Software Knowledge Graph.
/// Maintains a dictionary of nodes (keyed by ID) and a list of directed relationships.
/// All mutation is done through type-safe methods that prevent duplicates.
/// </summary>
public class SoftwareKnowledgeGraph
{
    private readonly Dictionary<string, GraphNode>    _nodes         = new();
    private readonly List<GraphRelationship>          _relationships = new();

    // ── Node access ───────────────────────────────────────────────────────────

    /// <summary>All nodes in the graph, keyed by their ID.</summary>
    public IReadOnlyDictionary<string, GraphNode> Nodes => _nodes;

    /// <summary>All directed relationships in the graph.</summary>
    public IReadOnlyList<GraphRelationship> Relationships => _relationships;

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>Adds or overwrites a node with the given ID.</summary>
    public GraphNode AddNode(GraphNode node)
    {
        _nodes[node.Id] = node;
        return node;
    }

    /// <summary>
    /// Returns the existing node if one with <paramref name="id"/> already exists;
    /// otherwise creates and stores a new one.
    /// </summary>
    public GraphNode GetOrAddNode(string id, string name, string type, string ns = "")
    {
        if (!_nodes.TryGetValue(id, out var node))
        {
            node = new GraphNode { Id = id, Name = name, Type = type, Namespace = ns };
            _nodes[id] = node;
        }
        return node;
    }

    /// <summary>
    /// Adds a directed relationship between two nodes.
    /// Silently ignores exact duplicates (same from/to/type triple).
    /// </summary>
    public void AddRelationship(string fromId, string toId, RelationshipType type)
    {
        if (fromId == toId) return;   // No self-loops

        var exists = _relationships.Any(r =>
            r.FromNodeId == fromId &&
            r.ToNodeId   == toId   &&
            r.RelationshipType == type);

        if (!exists)
        {
            _relationships.Add(new GraphRelationship
            {
                FromNodeId       = fromId,
                ToNodeId         = toId,
                RelationshipType = type
            });
        }
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>Returns true if a node with the given ID exists.</summary>
    public bool ContainsNode(string id) => _nodes.ContainsKey(id);

    /// <summary>Finds a node by ID, or null if not found.</summary>
    public GraphNode? FindNode(string id) =>
        _nodes.TryGetValue(id, out var n) ? n : null;

    /// <summary>Returns all nodes of a given type.</summary>
    public IEnumerable<GraphNode> GetNodesOfType(string type) =>
        _nodes.Values.Where(n => n.Type == type);

    /// <summary>Returns all outgoing relationships from the specified node.</summary>
    public IEnumerable<GraphRelationship> GetRelationshipsFrom(string nodeId) =>
        _relationships.Where(r => r.FromNodeId == nodeId);

    /// <summary>Returns all incoming relationships to the specified node.</summary>
    public IEnumerable<GraphRelationship> GetRelationshipsTo(string nodeId) =>
        _relationships.Where(r => r.ToNodeId == nodeId);

    /// <summary>Returns all relationships of the specified type.</summary>
    public IEnumerable<GraphRelationship> GetRelationshipsOfType(RelationshipType type) =>
        _relationships.Where(r => r.RelationshipType == type);
}
