namespace KnowledgeGraphEngine.Roslyn.DTOs;

/// <summary>Extracted metadata for a single method or method signature.</summary>
public class MethodMetadata
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
    public bool IsAsync { get; set; }
    public bool IsStatic { get; set; }
    public bool IsOverride { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsPublic { get; set; }

    /// <summary>
    /// Method invocations found in the method body.
    /// Format: "TargetType.MethodName" when resolved, or raw expression otherwise.
    /// </summary>
    public List<string> InvokedMethods { get; set; } = new();

    /// <summary>Types instantiated via <c>new</c> expressions in the method body.</summary>
    public List<string> CreatedTypes { get; set; } = new();
}

/// <summary>Represents a method or constructor parameter.</summary>
public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
