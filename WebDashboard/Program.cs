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
using WebDashboard;

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

    var solutionDir = Directory.GetParent(app.Environment.ContentRootPath)?.FullName ?? app.Environment.ContentRootPath;
    string apiDir = "";
    bool apiMigrated = false;
    try
    {
        var (legacyProjName, _) = WebApiScaffolder.FindLegacyProject(solutionDir);
        apiDir = Path.Combine(solutionDir, legacyProjName + ".Api");
        apiMigrated = Directory.Exists(apiDir);
    }
    catch { }

    if (!File.Exists(nodesPath) || !File.Exists(relsPath))
    {
        return Results.Ok(new { generated = false, apiMigrated });
    }

    var nodesInfo = new FileInfo(nodesPath);
    var relsInfo = new FileInfo(relsPath);

    return Results.Ok(new
    {
        generated = true,
        apiMigrated,
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
        // 1. Locate LegacyProject dynamically
        var contentRoot = app.Environment.ContentRootPath;
        var solutionDir = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;
        var (_, legacyProjectPath) = WebApiScaffolder.FindLegacyProject(solutionDir);

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

System.Diagnostics.Process? apiProcess = null;

app.MapPost("/api/graph/migrate", async (HttpContext context) =>
{
    try
    {
        var contentRoot = app.Environment.ContentRootPath;
        var solutionDir = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(contentRoot, "wwwroot");
        var outputDir = Path.Combine(webRoot, "output");

        // Run scaffolding
        var apiProjDir = await WebApiScaffolder.MigrateAsync(solutionDir, outputDir);

        // Kill previously running api process if any
        if (apiProcess != null && !apiProcess.HasExited)
        {
            try 
            { 
                apiProcess.Kill(true); 
                await apiProcess.WaitForExitAsync();
            } 
            catch { }
        }

        // Start the newly generated Web API project
        apiProcess = new System.Diagnostics.Process();
        apiProcess.StartInfo.FileName = "dotnet";
        apiProcess.StartInfo.Arguments = "run";
        apiProcess.StartInfo.WorkingDirectory = apiProjDir;
        apiProcess.StartInfo.UseShellExecute = false;
        apiProcess.Start();

        // Give it a second to spin up
        await Task.Delay(1500);

        return Results.Ok(new
        {
            success = true,
            swaggerUrl = "http://localhost:5002/swagger/index.html"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Migration Generation Failed",
            detail: ex.ToString(),
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.MapPost("/api/graph/delete-api", async (HttpContext context) =>
{
    try
    {
        // Kill previously running api process if any
        if (apiProcess != null && !apiProcess.HasExited)
        {
            try 
            { 
                apiProcess.Kill(true); 
                await apiProcess.WaitForExitAsync();
            } 
            catch { }
        }

        var contentRoot = app.Environment.ContentRootPath;
        var solutionDir = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;
        var (legacyProjName, _) = WebApiScaffolder.FindLegacyProject(solutionDir);
        var apiProjDir = Path.Combine(solutionDir, legacyProjName + ".Api");

        if (Directory.Exists(apiProjDir))
        {
            await Task.Delay(500); // Wait for file locks to clear
            try
            {
                Directory.Delete(apiProjDir, true);
            }
            catch
            {
                await Task.Delay(1000); // Retry once after 1s
                Directory.Delete(apiProjDir, true);
            }
        }

        return Results.Ok(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Failed to delete Web API project",
            detail: ex.ToString(),
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
});

app.Run();
