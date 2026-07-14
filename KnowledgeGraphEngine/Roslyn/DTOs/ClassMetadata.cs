namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>
/// Extracted metadata for a single C# class declaration.
/// </summary>
public class ClassMetadata
{
    /// <summary>Short class name (e.g., "OrderService").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Fully-qualified type name (e.g., "LegacyProject.Services.OrderService").</summary>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>The namespace this class belongs to.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>Short name of the direct base class, if any.</summary>
    public string? BaseClass { get; set; }

    /// <summary>Short names of all implemented interfaces.</summary>
    public List<string> ImplementedInterfaces { get; set; } = new();

    /// <summary>All method declarations inside this class.</summary>
    public List<MethodMetadata> Methods { get; set; } = new();

    /// <summary>All constructor declarations inside this class.</summary>
    public List<ConstructorMetadata> Constructors { get; set; } = new();

    /// <summary>All property declarations inside this class.</summary>
    public List<PropertyMetadata> Properties { get; set; } = new();

    /// <summary>All field declarations inside this class.</summary>
    public List<FieldMetadata> Fields { get; set; } = new();

    /// <summary>Method invocation targets found in the class body (e.g., "CustomerRepository.GetByIdAsync").</summary>
    public List<string> MethodInvocations { get; set; } = new();

    /// <summary>Types instantiated via object creation expressions (e.g., "new Order()").</summary>
    public List<string> ObjectCreations { get; set; } = new();

    /// <summary>Using directive namespaces from the file that contains this class.</summary>
    public List<string> UsingDirectives { get; set; } = new();

    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public bool IsSealed { get; set; }
}
