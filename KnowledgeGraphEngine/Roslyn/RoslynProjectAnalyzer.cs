using KnowledgeGraphEngine.Roslyn.DTOs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace KnowledgeGraphEngine.Roslyn;

/// <summary>
/// Loads a C# project from its source files, creates an in-memory compilation,
/// and delegates per-file syntax/semantic analysis to <see cref="RoslynSyntaxWalker"/>.
/// </summary>
public class RoslynProjectAnalyzer
{
    /// <summary>
    /// Analyzes all C# source files under the given project path.
    /// </summary>
    /// <param name="projectPath">
    /// Path to the project directory or a .csproj file.
    /// </param>
    /// <returns>A <see cref="ProjectMetadata"/> containing everything extracted.</returns>
    public async Task<ProjectMetadata> AnalyzeProjectAsync(string projectPath)
    {
        // ── Resolve the project directory ────────────────────────────────────
        string projectDir;
        if (Directory.Exists(projectPath))
        {
            projectDir = projectPath;
        }
        else if (File.Exists(projectPath) &&
                 Path.GetExtension(projectPath).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            projectDir = Path.GetDirectoryName(projectPath)!;
        }
        else
        {
            throw new ArgumentException(
                $"'{projectPath}' is not a valid project directory or .csproj file.");
        }

        var csprojFiles = Directory.GetFiles(projectDir, "*.csproj");
        var projectName = csprojFiles.Length > 0
            ? Path.GetFileNameWithoutExtension(csprojFiles[0])
            : Path.GetFileName(projectDir);

        Console.WriteLine($"  Project directory : {projectDir}");
        Console.WriteLine($"  Project name      : {projectName}");

        // ── Collect all .cs source files ─────────────────────────────────────
        var csFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .ToArray();

        Console.WriteLine($"  Source files found: {csFiles.Length}");

        // ── Parse each file into a syntax tree ───────────────────────────────
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in csFiles)
        {
            var source = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(
                source,
                CSharpParseOptions.Default,
                path: file);
            syntaxTrees.Add(tree);
        }

        // ── Build a compilation so semantic models are available ──────────────
        var references = BuildMetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: projectName,
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // ── Walk each syntax tree ─────────────────────────────────────────────
        var metadata = new ProjectMetadata
        {
            Name = projectName,
            FilePath = projectDir
        };

        foreach (var tree in syntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = await tree.GetRootAsync();
            var walker = new RoslynSyntaxWalker(semanticModel);
            walker.Visit(root);

            metadata.Classes.AddRange(walker.Classes);
            metadata.Interfaces.AddRange(walker.Interfaces);
            metadata.Enums.AddRange(walker.Enums);
        }

        // ── Deduplicate and collect namespaces ────────────────────────────────
        metadata.Namespaces = metadata.Classes
            .Select(c => c.Namespace)
            .Concat(metadata.Interfaces.Select(i => i.Namespace))
            .Concat(metadata.Enums.Select(e => e.Namespace))
            .Where(ns => !string.IsNullOrWhiteSpace(ns))
            .Distinct()
            .OrderBy(ns => ns)
            .ToList();

        return metadata;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Collects metadata references from the currently loaded assemblies so that
    /// the in-memory compilation can resolve BCL types.
    /// </summary>
    private static IReadOnlyList<MetadataReference> BuildMetadataReferences()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();
    }
}
