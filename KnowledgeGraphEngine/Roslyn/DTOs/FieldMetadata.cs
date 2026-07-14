namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>Extracted metadata for a C# field declaration.</summary>
public class FieldMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsReadonly { get; set; }
    public bool IsStatic { get; set; }
    public bool IsConst { get; set; }
}
