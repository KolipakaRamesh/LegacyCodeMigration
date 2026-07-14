namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>
/// Top-level metadata container for an analyzed C# project.
/// Holds all extracted classes, interfaces, enums, and namespace names.
/// </summary>
public class ProjectMetadata
{
    /// <summary>Assembly / project name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute path to the project root directory.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>All class declarations found in the project.</summary>
    public List<ClassMetadata> Classes { get; set; } = new();

    /// <summary>All interface declarations found in the project.</summary>
    public List<InterfaceMetadata> Interfaces { get; set; } = new();

    /// <summary>All enum declarations found in the project.</summary>
    public List<EnumMetadata> Enums { get; set; } = new();

    /// <summary>Distinct namespace names found across all source files.</summary>
    public List<string> Namespaces { get; set; } = new();
}
