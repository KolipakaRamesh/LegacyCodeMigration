namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>Extracted metadata for a constructor declaration.</summary>
public class ConstructorMetadata
{
    /// <summary>Name of the containing class.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Parameters declared on the constructor (used to infer DI dependencies).</summary>
    public List<ParameterInfo> Parameters { get; set; } = new();
}
