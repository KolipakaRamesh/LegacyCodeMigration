namespace KnowledgeGraphEngine.Graph;

/// <summary>
/// Defines all supported relationship types in the Software Knowledge Graph.
/// Add new types here as the graph evolves — no other code changes are required.
/// </summary>
public enum RelationshipType
{
    /// <summary>A method calls another method.</summary>
    CALLS,

    /// <summary>A class uses (depends on) another class or interface.</summary>
    USES,

    /// <summary>A class implements an interface.</summary>
    IMPLEMENTS,

    /// <summary>A class inherits from a base class.</summary>
    INHERITS,

    /// <summary>A class instantiates another class via new.</summary>
    CREATES,

    /// <summary>A class or interface owns a method.</summary>
    HAS_METHOD,

    /// <summary>A class or interface owns a property.</summary>
    HAS_PROPERTY,

    /// <summary>A class owns a field.</summary>
    HAS_FIELD,

    /// <summary>A class owns a constructor.</summary>
    HAS_CONSTRUCTOR,

    /// <summary>A type or namespace belongs to a namespace.</summary>
    BELONGS_TO_NAMESPACE,

    /// <summary>A type or namespace belongs to the analysed project.</summary>
    BELONGS_TO_PROJECT,

    /// <summary>A class references another type (e.g., via a property type).</summary>
    REFERENCES
}
