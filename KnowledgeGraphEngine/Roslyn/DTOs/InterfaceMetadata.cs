namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>Extracted metadata for a C# interface declaration.</summary>
public class InterfaceMetadata
{
    public string Name { get; set; } = string.Empty;
    public string FullyQualifiedName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Interfaces that this interface inherits from.</summary>
    public List<string> BaseInterfaces { get; set; } = new();

    /// <summary>Method signatures declared on this interface.</summary>
    public List<MethodMetadata> Methods { get; set; } = new();

    /// <summary>Properties declared on this interface.</summary>
    public List<PropertyMetadata> Properties { get; set; } = new();
}
