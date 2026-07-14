namespace KnowledgeGraphEngine.Query;

/// <summary>
/// Summary statistics about the Software Knowledge Graph.
/// </summary>
public class GraphStatistics
{
    public int TotalNodes         { get; set; }
    public int TotalRelationships { get; set; }
    public int ClassCount         { get; set; }
    public int InterfaceCount     { get; set; }
    public int EnumCount          { get; set; }
    public int MethodCount        { get; set; }
    public int PropertyCount      { get; set; }
    public int FieldCount         { get; set; }
    public int ConstructorCount   { get; set; }
    public int NamespaceCount     { get; set; }
}
