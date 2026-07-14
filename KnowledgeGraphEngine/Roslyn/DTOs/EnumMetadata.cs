namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>Extracted metadata for a C# enum declaration.</summary>
public class EnumMetadata
{
    public string Name { get; set; } = string.Empty;
    public string FullyQualifiedName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;

    /// <summary>All enum member names.</summary>
    public List<string> Members { get; set; } = new();
}
