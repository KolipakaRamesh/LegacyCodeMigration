using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KnowledgeGraphEngine.Roslyn;
using KnowledgeGraphEngine.Builder;
using KnowledgeGraphEngine.Query;
using KnowledgeGraphEngine.Storage;

var builder = WebApplication.CreateBuilder(args);

// Enable static file serving (HTML, CSS, JS)
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// ── GET /api/graph/files ───────────────────────────────────────────────────
app.MapGet("/api/graph/files", (HttpContext context) =>
{
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var outputDir = Path.Combine(webRoot, "output");
    var nodesPath = Path.Combine(outputDir, "nodes.json");
    var relsPath = Path.Combine(outputDir, "relationships.json");

    if (!File.Exists(nodesPath) || !File.Exists(relsPath))
    {
        return Results.Ok(new { generated = false });
    }

    var nodesInfo = new FileInfo(nodesPath);
    var relsInfo = new FileInfo(relsPath);

    return Results.Ok(new
    {
        generated = true,
        files = new[]
        {
            new
            {
                name = "nodes.json",
                path = nodesInfo.FullName,
                sizeBytes = nodesInfo.Length,
                generatedAt = nodesInfo.LastWriteTimeUtc
            },
            new
            {
                name = "relationships.json",
                path = relsInfo.FullName,
                sizeBytes = relsInfo.Length,
                generatedAt = relsInfo.LastWriteTimeUtc
            }
        }
    });
});

// ── POST /api/graph/generate ───────────────────────────────────────────────
app.MapPost("/api/graph/generate", async (HttpContext context) =>
{
    try
    {
        // 1. Locate LegacyProject
        var contentRoot = app.Environment.ContentRootPath;
        var legacyProjectPath = FindLegacyProjectPath(contentRoot);

        // 2. Roslyn Analysis
        var analyzer = new RoslynProjectAnalyzer();
        var metadata = await analyzer.AnalyzeProjectAsync(legacyProjectPath);

        // 3. Build Knowledge Graph
        var graphBuilder = new KnowledgeGraphBuilder();
        var graph = graphBuilder.Build(metadata);

        var queryEngine = new GraphQueryEngine(graph);
        var stats = queryEngine.GetStatistics();

        // 4. Save to output directory inside web root so it's servable
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(contentRoot, "wwwroot");
        var outputDir = Path.Combine(webRoot, "output");

        IGraphStorage storage = new JsonGraphStorage();
        await storage.SaveAsync(graph, new GraphStorageOptions { OutputDirectory = outputDir });

        var nodesInfo = new FileInfo(Path.Combine(outputDir, "nodes.json"));
        var relsInfo = new FileInfo(Path.Combine(outputDir, "relationships.json"));

        return Results.Ok(new
        {
            success = true,
            statistics = new
            {
                totalNodes = stats.TotalNodes,
                totalRelationships = stats.TotalRelationships,
                classCount = stats.ClassCount,
                interfaceCount = stats.InterfaceCount,
                enumCount = stats.EnumCount,
                methodCount = stats.MethodCount,
                propertyCount = stats.PropertyCount,
                fieldCount = stats.FieldCount,
                constructorCount = stats.ConstructorCount,
                namespaceCount = stats.NamespaceCount
            },
            files = new[]
            {
                new
                {
                    name = "nodes.json",
                    path = nodesInfo.FullName,
                    sizeBytes = nodesInfo.Length,
                    generatedAt = nodesInfo.LastWriteTimeUtc
                },
                new
                {
                    name = "relationships.json",
                    path = relsInfo.FullName,
                    sizeBytes = relsInfo.Length,
                    generatedAt = relsInfo.LastWriteTimeUtc
                }
            }
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Analysis Generation Failed",
            detail: ex.ToString(),
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.Run();

// ── Helper ──────────────────────────────────────────────────────────────────
static string FindLegacyProjectPath(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, "LegacyProject");
        if (Directory.Exists(candidate) &&
            Directory.GetFiles(candidate, "*.csproj").Length > 0)
            return candidate;
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the LegacyProject directory. Ensure structural paths are valid.");
}
