using KnowledgeGraphEngine.Builder;
using KnowledgeGraphEngine.Graph;

namespace KnowledgeGraphEngine.Query;

/// <summary>
/// Provides graph traversal and query operations over a <see cref="SoftwareKnowledgeGraph"/>.
/// All queries are pure in-memory — no external database is involved.
/// </summary>
public class GraphQueryEngine
{
    private readonly SoftwareKnowledgeGraph _graph;

    public GraphQueryEngine(SoftwareKnowledgeGraph graph)
    {
        _graph = graph;
    }

    // ── Discovery queries ─────────────────────────────────────────────────────

    /// <summary>Returns all class nodes in the graph.</summary>
    public IEnumerable<GraphNode> FindAllClasses() =>
        _graph.GetNodesOfType(NodeTypes.Class).OrderBy(n => n.Name);

    /// <summary>Returns all interface nodes in the graph.</summary>
    public IEnumerable<GraphNode> FindAllInterfaces() =>
        _graph.GetNodesOfType(NodeTypes.Interface).OrderBy(n => n.Name);

    /// <summary>Returns all enum nodes in the graph.</summary>
    public IEnumerable<GraphNode> FindAllEnums() =>
        _graph.GetNodesOfType(NodeTypes.Enum).OrderBy(n => n.Name);

    // ── Structural queries ────────────────────────────────────────────────────

    /// <summary>Returns all method nodes that belong to the named class.</summary>
    public IEnumerable<GraphNode> FindMethodsOfClass(string className)
    {
        var cls = FindClassByName(className);
        if (cls == null) return Enumerable.Empty<GraphNode>();

        return _graph.GetRelationshipsFrom(cls.Id)
            .Where(r => r.RelationshipType == RelationshipType.HAS_METHOD)
            .Select(r => _graph.FindNode(r.ToNodeId))
            .OfType<GraphNode>()
            .OrderBy(n => n.Name);
    }

    /// <summary>Returns all property nodes that belong to the named class.</summary>
    public IEnumerable<GraphNode> FindPropertiesOfClass(string className)
    {
        var cls = FindClassByName(className);
        if (cls == null) return Enumerable.Empty<GraphNode>();

        return _graph.GetRelationshipsFrom(cls.Id)
            .Where(r => r.RelationshipType == RelationshipType.HAS_PROPERTY)
            .Select(r => _graph.FindNode(r.ToNodeId))
            .OfType<GraphNode>()
            .OrderBy(n => n.Name);
    }

    /// <summary>Returns all field nodes that belong to the named class.</summary>
    public IEnumerable<GraphNode> FindFieldsOfClass(string className)
    {
        var cls = FindClassByName(className);
        if (cls == null) return Enumerable.Empty<GraphNode>();

        return _graph.GetRelationshipsFrom(cls.Id)
            .Where(r => r.RelationshipType == RelationshipType.HAS_FIELD)
            .Select(r => _graph.FindNode(r.ToNodeId))
            .OfType<GraphNode>()
            .OrderBy(n => n.Name);
    }

    // ── Relationship queries ──────────────────────────────────────────────────

    /// <summary>Returns all classes that implement the named interface.</summary>
    public IEnumerable<GraphNode> FindClassesImplementing(string interfaceName)
    {
        var iface = FindInterfaceByName(interfaceName);
        if (iface == null) return Enumerable.Empty<GraphNode>();

        return _graph.GetRelationshipsTo(iface.Id)
            .Where(r => r.RelationshipType == RelationshipType.IMPLEMENTS)
            .Select(r => _graph.FindNode(r.FromNodeId))
            .OfType<GraphNode>()
            .OrderBy(n => n.Name);
    }

    /// <summary>Returns all classes that directly inherit from the named base class.</summary>
    public IEnumerable<GraphNode> FindDerivedClasses(string baseClassName)
    {
        var baseNode = FindClassByName(baseClassName);
        if (baseNode == null) return Enumerable.Empty<GraphNode>();

        return _graph.GetRelationshipsTo(baseNode.Id)
            .Where(r => r.RelationshipType == RelationshipType.INHERITS)
            .Select(r => _graph.FindNode(r.FromNodeId))
            .OfType<GraphNode>()
            .OrderBy(n => n.Name);
    }

    /// <summary>
    /// Returns all nodes that the named class directly depends on
    /// via USES, INHERITS, IMPLEMENTS, CREATES, or REFERENCES relationships.
    /// </summary>
    public IEnumerable<GraphNode> FindDependenciesOfClass(string className)
    {
        var cls = FindClassByName(className);
        if (cls == null) return Enumerable.Empty<GraphNode>();

        var depTypes = new[]
        {
            RelationshipType.USES,
            RelationshipType.INHERITS,
            RelationshipType.IMPLEMENTS,
            RelationshipType.CREATES,
            RelationshipType.REFERENCES
        };

        return _graph.GetRelationshipsFrom(cls.Id)
            .Where(r => depTypes.Contains(r.RelationshipType))
            .Select(r => _graph.FindNode(r.ToNodeId))
            .OfType<GraphNode>()
            .Where(n => n.Type is NodeTypes.Class or NodeTypes.Interface)
            .DistinctBy(n => n.Id)
            .OrderBy(n => n.Name);
    }

    /// <summary>
    /// Returns all classes that call at least one method with the given name.
    /// </summary>
    public IEnumerable<GraphNode> FindClassesCallingMethod(string methodName)
    {
        // Find all method nodes with this name
        var matchingMethods = _graph.GetNodesOfType(NodeTypes.Method)
            .Where(n => n.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var callerClassIds = new HashSet<string>();
        foreach (var method in matchingMethods)
        {
            // Who calls this method node?
            var callerMethods = _graph.GetRelationshipsTo(method.Id)
                .Where(r => r.RelationshipType == RelationshipType.CALLS)
                .Select(r => _graph.FindNode(r.FromNodeId))
                .OfType<GraphNode>();

            foreach (var callerMethod in callerMethods)
            {
                var ownerClass = GetOwningClass(callerMethod.Id);
                if (ownerClass != null)
                    callerClassIds.Add(ownerClass.Id);
            }
        }

        return callerClassIds
            .Select(id => _graph.FindNode(id))
            .OfType<GraphNode>()
            .OrderBy(n => n.Name);
    }

    /// <summary>Returns all classes that USES or REFERENCES the named class.</summary>
    public IEnumerable<GraphNode> FindClassesUsing(string targetClassName)
    {
        var target = FindClassByName(targetClassName);
        if (target == null) return Enumerable.Empty<GraphNode>();

        return _graph.GetRelationshipsTo(target.Id)
            .Where(r => r.RelationshipType is RelationshipType.USES or RelationshipType.REFERENCES)
            .Select(r => _graph.FindNode(r.FromNodeId))
            .OfType<GraphNode>()
            .Where(n => n.Type == NodeTypes.Class)
            .DistinctBy(n => n.Id)
            .OrderBy(n => n.Name);
    }

    // ── Traversal ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a formatted dependency chain for the given class using breadth-first traversal.
    /// Stops at <paramref name="maxDepth"/> levels or when all dependencies are visited.
    /// </summary>
    public IEnumerable<string> GetDependencyChain(string className, int maxDepth = 6)
    {
        var start = FindClassByName(className);
        if (start == null)
        {
            yield return $"(Class '{className}' not found in graph)";
            yield break;
        }

        var visited = new HashSet<string>();
        var queue   = new Queue<(GraphNode Node, int Depth, string Prefix)>();
        queue.Enqueue((start, 0, string.Empty));

        while (queue.Count > 0)
        {
            var (node, depth, prefix) = queue.Dequeue();
            if (visited.Contains(node.Id)) continue;
            visited.Add(node.Id);

            yield return $"{prefix}{node.Name}  [{node.Type}]";

            if (depth >= maxDepth) continue;

            var deps = FindDependenciesOfClass(node.Name)
                .Where(d => !visited.Contains(d.Id))
                .ToList();

            foreach (var dep in deps)
                queue.Enqueue((dep, depth + 1, new string(' ', (depth + 1) * 4) + "↳ "));
        }
    }

    // ── Statistics ────────────────────────────────────────────────────────────

    /// <summary>Returns aggregate statistics about the graph.</summary>
    public GraphStatistics GetStatistics() => new()
    {
        TotalNodes         = _graph.Nodes.Count,
        TotalRelationships = _graph.Relationships.Count,
        ClassCount         = _graph.GetNodesOfType(NodeTypes.Class).Count(),
        InterfaceCount     = _graph.GetNodesOfType(NodeTypes.Interface).Count(),
        EnumCount          = _graph.GetNodesOfType(NodeTypes.Enum).Count(),
        MethodCount        = _graph.GetNodesOfType(NodeTypes.Method).Count(),
        PropertyCount      = _graph.GetNodesOfType(NodeTypes.Property).Count(),
        FieldCount         = _graph.GetNodesOfType(NodeTypes.Field).Count(),
        ConstructorCount   = _graph.GetNodesOfType(NodeTypes.Constructor).Count(),
        NamespaceCount     = _graph.GetNodesOfType(NodeTypes.Namespace).Count()
    };

    // ── Private helpers ───────────────────────────────────────────────────────

    private GraphNode? FindClassByName(string name) =>
        _graph.Nodes.Values.FirstOrDefault(n =>
            n.Type == NodeTypes.Class &&
            n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private GraphNode? FindInterfaceByName(string name) =>
        _graph.Nodes.Values.FirstOrDefault(n =>
            n.Type == NodeTypes.Interface &&
            n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private GraphNode? GetOwningClass(string methodNodeId)
    {
        var rel = _graph.Relationships.FirstOrDefault(r =>
            r.ToNodeId         == methodNodeId &&
            r.RelationshipType == RelationshipType.HAS_METHOD);
        return rel != null ? _graph.FindNode(rel.FromNodeId) : null;
    }
}
