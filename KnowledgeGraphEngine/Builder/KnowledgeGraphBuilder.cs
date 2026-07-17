using KnowledgeGraphEngine.Graph;
using KnowledgeGraphEngine.Roslyn.DTOs;

namespace KnowledgeGraphEngine.Builder;

/// <summary>
/// Converts a <see cref="ProjectMetadata"/> (produced by Roslyn analysis)
/// into a fully populated <see cref="SoftwareKnowledgeGraph"/>.
///
/// Design rules:
/// • No node or relationship is ever hardcoded — everything is derived from metadata.
/// • Generic base types (e.g. BaseRepository&lt;T&gt;) are matched by short name.
/// • Duplicate relationships are silently dropped by the graph itself.
/// </summary>
public class KnowledgeGraphBuilder
{
    /// <summary>
    /// Builds and returns the Software Knowledge Graph from extracted Roslyn metadata.
    /// </summary>
    public SoftwareKnowledgeGraph Build(ProjectMetadata project)
    {
        var graph = new SoftwareKnowledgeGraph();

        // ── Project node ──────────────────────────────────────────────────────
        var projectId = $"project:{project.Name}";
        graph.GetOrAddNode(projectId, project.Name, NodeTypes.Project);

        // ── Namespace nodes ───────────────────────────────────────────────────
        foreach (var ns in project.Namespaces)
        {
            var nsId = $"namespace:{ns}";
            graph.GetOrAddNode(nsId, ns, NodeTypes.Namespace);
            graph.AddRelationship(nsId, projectId, RelationshipType.BELONGS_TO_PROJECT);
        }

        // ── Interface nodes ───────────────────────────────────────────────────
        foreach (var iface in project.Interfaces)
        {
            var ifaceId = $"interface:{iface.FullyQualifiedName}";
            graph.GetOrAddNode(ifaceId, iface.Name, NodeTypes.Interface, iface.Namespace);

            LinkToNamespace(graph, ifaceId, iface.Namespace);
            graph.AddRelationship(ifaceId, projectId, RelationshipType.BELONGS_TO_PROJECT);

            // Methods on interface
            foreach (var method in iface.Methods)
            {
                var methodId = $"method:{iface.FullyQualifiedName}.{method.Name}";
                var mn = graph.GetOrAddNode(methodId, method.Name, NodeTypes.Method, iface.Namespace);
                SetMethodMetadata(mn, method);
                graph.AddRelationship(ifaceId, methodId, RelationshipType.HAS_METHOD);
            }

            // Properties on interface
            foreach (var prop in iface.Properties)
            {
                var propId = $"property:{iface.FullyQualifiedName}.{prop.Name}";
                var pn = graph.GetOrAddNode(propId, prop.Name, NodeTypes.Property, iface.Namespace);
                pn.Metadata["Type"] = prop.Type;
                graph.AddRelationship(ifaceId, propId, RelationshipType.HAS_PROPERTY);
            }
        }

        // ── Class nodes ───────────────────────────────────────────────────────
        foreach (var cls in project.Classes)
        {
            var classId = $"class:{cls.FullyQualifiedName}";
            var classNode = graph.GetOrAddNode(classId, cls.Name, NodeTypes.Class, cls.Namespace);
            classNode.Metadata["IsAbstract"] = cls.IsAbstract.ToString();
            classNode.Metadata["IsStatic"]   = cls.IsStatic.ToString();
            classNode.Metadata["IsSealed"]   = cls.IsSealed.ToString();

            LinkToNamespace(graph, classId, cls.Namespace);
            graph.AddRelationship(classId, projectId, RelationshipType.BELONGS_TO_PROJECT);

            // ── Inheritance ───────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(cls.BaseClass))
            {
                var baseNode = FindByName(graph, cls.BaseClass, NodeTypes.Class)
                    ?? graph.GetOrAddNode($"class:{cls.BaseClass}", cls.BaseClass, NodeTypes.Class);
                graph.AddRelationship(classId, baseNode.Id, RelationshipType.INHERITS);
            }

            // ── Interface implementations ─────────────────────────────────────
            foreach (var ifaceName in cls.ImplementedInterfaces)
            {
                var ifaceNode = FindByName(graph, ifaceName, NodeTypes.Interface)
                    ?? graph.GetOrAddNode($"interface:{ifaceName}", ifaceName, NodeTypes.Interface);
                graph.AddRelationship(classId, ifaceNode.Id, RelationshipType.IMPLEMENTS);
            }

            // ── Constructors → USES relationships (DI inference) ──────────────
            foreach (var ctor in cls.Constructors)
            {
                var ctorId = $"constructor:{cls.FullyQualifiedName}..ctor";
                graph.GetOrAddNode(ctorId, $"{cls.Name}()", NodeTypes.Constructor, cls.Namespace);
                graph.AddRelationship(classId, ctorId, RelationshipType.HAS_CONSTRUCTOR);

                foreach (var param in ctor.Parameters)
                {
                    var depNode = FindByName(graph, param.Type, NodeTypes.Interface)
                        ?? FindByName(graph, param.Type, NodeTypes.Class);
                    if (depNode != null)
                        graph.AddRelationship(classId, depNode.Id, RelationshipType.USES);
                }
            }

            // ── Methods ───────────────────────────────────────────────────────
            foreach (var method in cls.Methods)
            {
                var methodId = $"method:{cls.FullyQualifiedName}.{method.Name}";
                var mn = graph.GetOrAddNode(methodId, method.Name, NodeTypes.Method, cls.Namespace);
                SetMethodMetadata(mn, method);
                graph.AddRelationship(classId, methodId, RelationshipType.HAS_METHOD);

                // Method invocations → CALLS
                foreach (var invoked in method.InvokedMethods.Distinct())
                {
                    if (string.IsNullOrWhiteSpace(invoked)) continue;

                    var parts = invoked.Split('.');
                    var targetTypeName  = parts.Length >= 2 ? parts[^2] : string.Empty;
                    var targetMethodName = parts.Last();

                    // Try to resolve the invoked method node
                    var invokedMethodNode = FindMethodByShortName(graph, targetTypeName, targetMethodName);
                    if (invokedMethodNode != null)
                    {
                        graph.AddRelationship(methodId, invokedMethodNode.Id, RelationshipType.CALLS);

                        // Class USES the class that owns the called method
                        var ownerClass = GetOwningClass(graph, invokedMethodNode.Id);
                        if (ownerClass != null && ownerClass.Id != classId)
                            graph.AddRelationship(classId, ownerClass.Id, RelationshipType.USES);
                    }
                    else if (!string.IsNullOrWhiteSpace(targetTypeName))
                    {
                        // At least record a USES dependency to the target class/type
                        var targetNode = FindByName(graph, targetTypeName, NodeTypes.Class)
                            ?? FindByName(graph, targetTypeName, NodeTypes.Interface);
                        if (targetNode != null && targetNode.Id != classId)
                            graph.AddRelationship(classId, targetNode.Id, RelationshipType.USES);
                    }
                }

                // Object creations → CREATES
                foreach (var created in method.CreatedTypes.Distinct())
                {
                    if (string.IsNullOrWhiteSpace(created)) continue;
                    var createdNode = FindByName(graph, created, NodeTypes.Class);
                    if (createdNode != null && createdNode.Id != classId)
                        graph.AddRelationship(classId, createdNode.Id, RelationshipType.CREATES);
                }
            }

            // ── Properties ────────────────────────────────────────────────────
            foreach (var prop in cls.Properties)
            {
                var propId = $"property:{cls.FullyQualifiedName}.{prop.Name}";
                var pn = graph.GetOrAddNode(propId, prop.Name, NodeTypes.Property, cls.Namespace);
                pn.Metadata["Type"]     = prop.Type;
                pn.Metadata["IsStatic"] = prop.IsStatic.ToString();
                graph.AddRelationship(classId, propId, RelationshipType.HAS_PROPERTY);

                // If the property type is a known class or interface, add REFERENCES
                var propTypeNode = FindByName(graph, StripGenerics(prop.Type), NodeTypes.Class)
                    ?? FindByName(graph, StripGenerics(prop.Type), NodeTypes.Interface);
                if (propTypeNode != null && propTypeNode.Id != classId)
                    graph.AddRelationship(classId, propTypeNode.Id, RelationshipType.REFERENCES);
            }

            // ── Fields ────────────────────────────────────────────────────────
            foreach (var field in cls.Fields)
            {
                var fieldId = $"field:{cls.FullyQualifiedName}.{field.Name}";
                var fn = graph.GetOrAddNode(fieldId, field.Name, NodeTypes.Field, cls.Namespace);
                fn.Metadata["Type"]       = field.Type;
                fn.Metadata["IsReadonly"] = field.IsReadonly.ToString();
                fn.Metadata["IsStatic"]   = field.IsStatic.ToString();
                fn.Metadata["IsConst"]    = field.IsConst.ToString();
                graph.AddRelationship(classId, fieldId, RelationshipType.HAS_FIELD);
            }
        }

        // ── Enum nodes ────────────────────────────────────────────────────────
        foreach (var en in project.Enums)
        {
            var enumId = $"enum:{en.FullyQualifiedName}";
            var en_node = graph.GetOrAddNode(enumId, en.Name, NodeTypes.Enum, en.Namespace);
            en_node.Metadata["Members"] = string.Join(", ", en.Members);

            LinkToNamespace(graph, enumId, en.Namespace);
            graph.AddRelationship(enumId, projectId, RelationshipType.BELONGS_TO_PROJECT);
        }

        return graph;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void LinkToNamespace(SoftwareKnowledgeGraph graph, string nodeId, string ns)
    {
        var nsId = $"namespace:{ns}";
        if (graph.ContainsNode(nsId))
            graph.AddRelationship(nodeId, nsId, RelationshipType.BELONGS_TO_NAMESPACE);
    }

    private static void SetMethodMetadata(GraphNode node, Roslyn.DTOs.MethodMetadata m)
    {
        node.Metadata["ReturnType"]  = m.ReturnType;
        node.Metadata["IsAsync"]     = m.IsAsync.ToString();
        node.Metadata["IsStatic"]    = m.IsStatic.ToString();
        node.Metadata["IsOverride"]  = m.IsOverride.ToString();
        node.Metadata["IsVirtual"]   = m.IsVirtual.ToString();
        node.Metadata["IsAbstract"]  = m.IsAbstract.ToString();
        node.Metadata["IsPublic"]    = m.IsPublic.ToString();
        node.Metadata["Parameters"]  = string.Join(", ",
            m.Parameters.Select(p => $"{p.Type} {p.Name}"));
    }

    /// <summary>
    /// Finds a node by short name and type.
    /// Performs a case-sensitive exact match on <see cref="GraphNode.Name"/>.
    /// </summary>
    private static GraphNode? FindByName(SoftwareKnowledgeGraph graph, string name, string type)
    {
        return graph.Nodes.Values.FirstOrDefault(n =>
            n.Type == type &&
            n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds a method node where the owning class short-name matches
    /// <paramref name="ownerHint"/> and the method name matches <paramref name="methodName"/>.
    /// </summary>
    private static GraphNode? FindMethodByShortName(
        SoftwareKnowledgeGraph graph, string ownerHint, string methodName)
    {
        return graph.Nodes.Values.FirstOrDefault(n =>
            n.Type == NodeTypes.Method &&
            n.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(ownerHint) ||
             n.Id.Contains(ownerHint, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Returns the class node that owns a given method node via a HAS_METHOD relationship.
    /// </summary>
    private static GraphNode? GetOwningClass(SoftwareKnowledgeGraph graph, string methodNodeId)
    {
        var rel = graph.Relationships.FirstOrDefault(r =>
            r.ToNodeId         == methodNodeId &&
            r.RelationshipType == RelationshipType.HAS_METHOD);
        return rel != null ? graph.FindNode(rel.FromNodeId) : null;
    }

    private static string StripGenerics(string typeName)
    {
        var idx = typeName.IndexOf('<');
        return idx >= 0 ? typeName[..idx] : typeName;
    }
}
