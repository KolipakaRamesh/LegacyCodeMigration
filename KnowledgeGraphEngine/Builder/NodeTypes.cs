namespace KnowledgeGraphEngine.Builder;

/// <summary>
/// String constants for every graph node type.
/// Centralised here so that Builder, Query, and Storage components all use the same values.
/// </summary>
public static class NodeTypes
{
    public const string Project     = "Project";
    public const string Namespace   = "Namespace";
    public const string Class       = "Class";
    public const string Interface   = "Interface";
    public const string Enum        = "Enum";
    public const string Method      = "Method";
    public const string Constructor = "Constructor";
    public const string Property    = "Property";
    public const string Field       = "Field";
}
