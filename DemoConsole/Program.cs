using KnowledgeGraphEngine.Builder;
using KnowledgeGraphEngine.Query;
using KnowledgeGraphEngine.Roslyn;
using KnowledgeGraphEngine.Storage;

// ═══════════════════════════════════════════════════════════════════════════════
//  LegacyCodeMigration — Software Knowledge Graph POC
//  Demonstrates the complete end-to-end pipeline:
//    LegacyProject → Roslyn Analysis → Metadata → Graph Builder →
//    Knowledge Graph → JSON Export → Graph Queries → Console Output
// ═══════════════════════════════════════════════════════════════════════════════

PrintBanner("LegacyCodeMigration — Software Knowledge Graph POC");

// ── Step 1 — Locate LegacyProject ────────────────────────────────────────────
PrintStep(1, "Locating LegacyProject source files...");
var legacyProjectPath = FindLegacyProjectPath(AppContext.BaseDirectory);
Console.WriteLine($"    Path: {legacyProjectPath}");

// ── Step 2 — Roslyn Analysis ─────────────────────────────────────────────────
PrintStep(2, "Analyzing LegacyProject with Roslyn Compiler Platform...");
var analyzer = new RoslynProjectAnalyzer();
var metadata = await analyzer.AnalyzeProjectAsync(legacyProjectPath);

PrintSeparator();
Console.WriteLine($"    Project Name       : {metadata.Name}");
Console.WriteLine($"    Classes Found      : {metadata.Classes.Count}");
Console.WriteLine($"    Interfaces Found   : {metadata.Interfaces.Count}");
Console.WriteLine($"    Enums Found        : {metadata.Enums.Count}");
Console.WriteLine($"    Namespaces Found   : {metadata.Namespaces.Count}");
Console.WriteLine($"    Methods (total)    : {metadata.Classes.Sum(c => c.Methods.Count) + metadata.Interfaces.Sum(i => i.Methods.Count)}");
Console.WriteLine($"    Constructors       : {metadata.Classes.Sum(c => c.Constructors.Count)}");
Console.WriteLine($"    Properties         : {metadata.Classes.Sum(c => c.Properties.Count)}");
Console.WriteLine($"    Fields             : {metadata.Classes.Sum(c => c.Fields.Count)}");
PrintSeparator();

// ── Step 3 — Build Knowledge Graph ───────────────────────────────────────────
PrintStep(3, "Building Software Knowledge Graph...");
var builder = new KnowledgeGraphBuilder();
var graph   = builder.Build(metadata);

var queryEngine = new GraphQueryEngine(graph);
var stats = queryEngine.GetStatistics();
Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("    ┌─────────────────────────────────────────┐");
Console.WriteLine($"    │  Nodes             : {stats.TotalNodes,-6}                 │");
Console.WriteLine($"    │  Relationships     : {stats.TotalRelationships,-6}                 │");
Console.WriteLine($"    │  Classes           : {stats.ClassCount,-6}                 │");
Console.WriteLine($"    │  Interfaces        : {stats.InterfaceCount,-6}                 │");
Console.WriteLine($"    │  Enums             : {stats.EnumCount,-6}                 │");
Console.WriteLine($"    │  Methods           : {stats.MethodCount,-6}                 │");
Console.WriteLine($"    │  Properties        : {stats.PropertyCount,-6}                 │");
Console.WriteLine($"    │  Fields            : {stats.FieldCount,-6}                 │");
Console.WriteLine($"    │  Constructors      : {stats.ConstructorCount,-6}                 │");
Console.WriteLine($"    │  Namespaces        : {stats.NamespaceCount,-6}                 │");
Console.WriteLine("    └─────────────────────────────────────────┘");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine("    Knowledge Graph created successfully ✓");

// ── Step 4 — Export to JSON ───────────────────────────────────────────────────
PrintStep(4, "Exporting Knowledge Graph to JSON...");
var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
IGraphStorage storage = new JsonGraphStorage();
await storage.SaveAsync(graph, new GraphStorageOptions { OutputDirectory = outputDir });
Console.WriteLine("    JSON export complete ✓");

// ── Step 5 — Graph Queries ────────────────────────────────────────────────────
PrintStep(5, "Running Sample Graph Queries...");
var q = queryEngine;

// ─── Query 1: All Classes ─────────────────────────────────────────────────────
PrintQueryHeader("Query 1 — All Classes in LegacyProject");
var allClasses = q.FindAllClasses().ToList();
foreach (var c in allClasses)
{
    var ns = string.IsNullOrWhiteSpace(c.Namespace) ? "" : $"  ({c.Namespace})";
    var tag = c.Metadata.GetValueOrDefault("IsAbstract") == "True" ? " [abstract]" : "";
    Console.WriteLine($"    • {c.Name}{tag}{ns}");
}
Console.WriteLine($"    Total: {allClasses.Count} classes");

// ─── Query 2: All Interfaces ──────────────────────────────────────────────────
PrintQueryHeader("Query 2 — All Interfaces");
foreach (var i in q.FindAllInterfaces())
    Console.WriteLine($"    «interface» {i.Name}  ({i.Namespace})");

// ─── Query 3: Classes implementing IRepository ────────────────────────────────
PrintQueryHeader("Query 3 — Classes implementing IRepository");
var repoImpl = q.FindClassesImplementing("IRepository").ToList();
if (repoImpl.Any())
    foreach (var c in repoImpl) Console.WriteLine($"    ✓ {c.Name}");
else
    Console.WriteLine("    (IRepository<T> is generic — checking all repositories by inheritance)");

// ─── Query 4: Derived classes of BaseRepository ───────────────────────────────
PrintQueryHeader("Query 4 — Derived classes of BaseRepository");
var derived = q.FindDerivedClasses("BaseRepository").ToList();
if (derived.Any())
    foreach (var c in derived) Console.WriteLine($"    ↳ {c.Name}");
else
    Console.WriteLine("    (BaseRepository<T> is generic — no exact-name match found)");

// ─── Query 5: Dependencies of OrderService ────────────────────────────────────
PrintQueryHeader("Query 5 — Direct Dependencies of OrderService");
var orderDeps = q.FindDependenciesOfClass("OrderService").ToList();
if (orderDeps.Any())
    foreach (var d in orderDeps) Console.WriteLine($"    → {d.Name}  [{d.Type}]");
else
    Console.WriteLine("    (No dependencies resolved)");

// ─── Query 6: Dependency Chain for OrderService ───────────────────────────────
PrintQueryHeader("Query 6 — Dependency Chain: OrderService");
foreach (var line in q.GetDependencyChain("OrderService", maxDepth: 3))
    Console.WriteLine($"    {line}");

// ─── Query 7: Methods of InvoiceService ──────────────────────────────────────
PrintQueryHeader("Query 7 — Methods of InvoiceService");
var methods = q.FindMethodsOfClass("InvoiceService").ToList();
if (methods.Any())
    foreach (var m in methods)
    {
        var ret   = m.Metadata.GetValueOrDefault("ReturnType", "?");
        var async = m.Metadata.GetValueOrDefault("IsAsync") == "True" ? " [async]" : "";
        Console.WriteLine($"    ⚙  {m.Name}()  : {ret}{async}");
    }
else
    Console.WriteLine("    (No methods found)");

// ─── Query 8: Classes using CustomerRepository ────────────────────────────────
PrintQueryHeader("Query 8 — Classes using CustomerRepository");
var usersOfRepo = q.FindClassesUsing("CustomerRepository").ToList();
if (usersOfRepo.Any())
    foreach (var c in usersOfRepo) Console.WriteLine($"    ⬡ {c.Name}");
else
    Console.WriteLine("    (No usages resolved)");

// ─── Query 9: Classes implementing IEmailService ─────────────────────────────
PrintQueryHeader("Query 9 — Classes implementing IEmailService");
var emailImpl = q.FindClassesImplementing("IEmailService").ToList();
if (emailImpl.Any())
    foreach (var c in emailImpl) Console.WriteLine($"    ✓ {c.Name}");
else
    Console.WriteLine("    (No implementors found)");

// ─── Query 10: Properties of Order ───────────────────────────────────────────
PrintQueryHeader("Query 10 — Properties of Order class");
var orderProps = q.FindPropertiesOfClass("Order").ToList();
if (orderProps.Any())
    foreach (var p in orderProps)
    {
        var type = p.Metadata.GetValueOrDefault("Type", "?");
        Console.WriteLine($"    • {p.Name}  : {type}");
    }
else
    Console.WriteLine("    (No properties found)");

// ─── Query 11: Fields of OrderService ────────────────────────────────────────
PrintQueryHeader("Query 11 — Fields of OrderService");
var fields = q.FindFieldsOfClass("OrderService").ToList();
if (fields.Any())
    foreach (var f in fields)
    {
        var type = f.Metadata.GetValueOrDefault("Type", "?");
        var ro   = f.Metadata.GetValueOrDefault("IsReadonly") == "True" ? " [readonly]" : "";
        Console.WriteLine($"    ▪ {f.Name}  : {type}{ro}");
    }
else
    Console.WriteLine("    (No fields found)");

// ─── Query 12: All Enums ──────────────────────────────────────────────────────
PrintQueryHeader("Query 12 — All Enums");
foreach (var e in q.FindAllEnums())
{
    var members = e.Metadata.GetValueOrDefault("Members", "");
    Console.WriteLine($"    «enum» {e.Name}  →  {members}");
}

// ── Final Summary ─────────────────────────────────────────────────────────────
PrintBanner("POC Complete — Knowledge Graph Generated Successfully!");
Console.WriteLine($"    nodes.json        : {Path.Combine(outputDir, "nodes.json")}");
Console.WriteLine($"    relationships.json: {Path.Combine(outputDir, "relationships.json")}");
Console.WriteLine();
Console.WriteLine("    Architecture:");
Console.WriteLine("      LegacyProject");
Console.WriteLine("           │");
Console.WriteLine("           ▼");
Console.WriteLine("      Roslyn Analyzer");
Console.WriteLine("           │");
Console.WriteLine("           ▼");
Console.WriteLine("      Metadata (DTOs)");
Console.WriteLine("           │");
Console.WriteLine("           ▼");
Console.WriteLine("      Knowledge Graph Builder");
Console.WriteLine("           │");
Console.WriteLine("           ▼");
Console.WriteLine("      Software Knowledge Graph");
Console.WriteLine("           │");
Console.WriteLine("           ▼");
Console.WriteLine("      JSON Export (nodes.json + relationships.json)");
Console.WriteLine("           │");
Console.WriteLine("           ▼");
Console.WriteLine("      Graph Queries → Console Output");
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════════
//  Helpers
// ═══════════════════════════════════════════════════════════════════════════════

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
        "Could not locate the LegacyProject directory. " +
        "Ensure the solution structure is intact and run from the solution root or any child directory.");
}



static void PrintBanner(string text)
{
    var width = text.Length + 6;
    var border = new string('═', width);
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"╔{border}╗");
    Console.WriteLine($"║   {text}   ║");
    Console.WriteLine($"╚{border}╝");
    Console.ResetColor();
    Console.WriteLine();
}

static void PrintStep(int step, string description)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write($"  ▶  Step {step}: ");
    Console.ResetColor();
    Console.WriteLine(description);
}

static void PrintQueryHeader(string title)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"  ┌─ {title}");
    Console.ResetColor();
}

static void PrintSeparator()
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("    " + new string('─', 52));
    Console.ResetColor();
}
