namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>Extracted metadata for a C# property declaration.</summary>
public class PropertyMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public bool IsStatic { get; set; }
}
