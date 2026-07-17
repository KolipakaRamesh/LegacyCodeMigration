# LegacyCodeMigration — Software Knowledge Graph POC

> A .NET 8 proof of concept that transforms a **legacy C# codebase** into a queryable **Software Knowledge Graph** using the Roslyn Compiler Platform — without touching a database, LLM, or any external service.

---

## Key Features

### 1. Roslyn-Based Static Analysis & Graph Construction
The POC parses the target legacy C# codebase assemblies and maps classes, interfaces, enums, methods, properties, fields, constructors, and relationships directly from the source code.

```
Legacy C# Project  (.cs source files)
        │
        ▼
Roslyn Compiler Platform
        │   Parses every file into a syntax tree
        │   Builds an in-memory CSharpCompilation
        │   Walks every declaration with a CSharpSyntaxWalker
        │   Extracts: classes · interfaces · enums · methods ·
        │             properties · fields · constructors ·
        │             inheritance · implementations · invocations ·
        │             object creations · DI dependencies
        ▼
Project Metadata  (structured DTOs)
        │
        ▼
Knowledge Graph Builder
        │   Maps every type, member, and relationship to a graph node or edge
        │   No hardcoded rules — everything is derived from the metadata
        ▼
Software Knowledge Graph  (in-memory)
        │   247 nodes · 404 directed relationships
        ▼
JSON Export
        │   nodes.json          ← every node (class, method, field, etc.)
        │   relationships.json  ← every directed edge (INHERITS, USES, CALLS, etc.)
        ▼
Graph Query Engine  (pure in-memory, no database)
            Find all classes · Find implementations · Trace dependencies ·
            Get dependency chains · Find callers · List methods/properties/fields
```

### 2. Automated API Migration (`WebApiScaffolder.cs`)
When migrating legacy monolithic codebases to modern microservices or cloud-native Web APIs, developers typically spend hours writing boilerplate controllers, setting up Dependency Injection, creating DTO structures, and manually adapting legacy namespaces.

The `WebApiScaffolder` proves that this transition can be completely automated by using the generated Software Knowledge Graph. It reads the extracted metadata (`nodes.json` and `relationships.json`) and the original source files, then programmatically builds a fully-functional, modern ASP.NET Core Web API wrapper (`LegacyProject.Api`) containing scaffolded endpoints for all legacy services.

When you click **"Migrate to Web API"** on the dashboard (or send a POST to `/api/graph/migrate`), the scaffolder automatically executes the following pipeline:

1. **Dynamic Target Discovery**: Dynamically searches the solution folder for the target legacy project, identifying its name and directory without any hardcoded paths.
2. **Boilerplate API Scaffolding**: Creates a new ASP.NET Core Web API directory structure, references needed NuGet packages (like Swashbuckle for Swagger), and generates a standalone `.csproj`.
3. **Smart Source Migration**: Copies the legacy source files and dynamically rewrites `namespace` and `using` declarations to map them to the new `{ProjectName}.Api` namespace.
4. **Service Pairing via Graph Relationships**: Finds all class and interface declarations ending in `Service`, maps implementations to interfaces using `IMPLEMENTS` relationships from the graph, and determines public methods using `HAS_METHOD`.
5. **Controller Scaffolding**: 
   - Creates a new `ApiController` (e.g., `CustomersController.cs`, `OrdersController.cs`) for each service.
   - Automatically maps HTTP verbs (`HttpGet`, `HttpPost`, `HttpPut`, `HttpDelete`) based on method naming conventions (e.g., methods starting with `Get`/`Find`/`List` map to `HttpGet`).
   - Automatically parses method parameters. If multiple parameters or value-tuples are found, it generates a custom Request DTO class (e.g., `CreateOrderRequestDto.cs`) and maps incoming JSON requests.
6. **Generic Swagger Schema Filter**: Creates a generic swagger filter (`SwaggerExampleSchemaFilter.cs`) that injects realistic mockup data into Swagger UI for common property types (like email, price, names, ZIP) to facilitate manual testing.
7. **Dynamic `Program.cs` Composition**: Scans graph data to dynamically discover context classes (`*Context`), repository classes (`*Repository`), and service implementations, registering them with the appropriate dependency injection lifecycle (`AddSingleton`/`AddScoped`) in the new Web API's entrypoint.
8. **Auto-Run**: Boots up the new API on port `5002` immediately, making it ready to receive web requests.

### 3. Interactive Web Dashboard
A modern, responsive web dashboard serves as the user interface to control graph generation, trigger API migrations, view codebase statistics, and explore dependencies interactively.

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      LegacyProject                          │
│   22 classes · 5 interfaces · 3 enums · 8 namespaces       │
│   (enterprise order management domain — the analysis target)│
└────────────────────────┬────────────────────────────────────┘
                         │  source files
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  KnowledgeGraphEngine                        │
│                                                             │
│  ┌──────────────────┐    ┌──────────────────────────────┐  │
│  │  RoslynAnalyzer  │───▶│  ProjectMetadata (DTOs)       │  │
│  │  SyntaxWalker    │    │  ClassMetadata, MethodMetadata│  │
│  └──────────────────┘    │  InterfaceMetadata, etc.      │  │
│                          └──────────────┬───────────────┘  │
│                                         │                   │
│                          ┌──────────────▼───────────────┐  │
│                          │   KnowledgeGraphBuilder       │  │
│                          │   Metadata → Nodes + Edges    │  │
│                          └──────────────┬───────────────┘  │
│                                         │                   │
│                          ┌──────────────▼───────────────┐  │
│                          │  SoftwareKnowledgeGraph       │  │
│                          │  (in-memory: nodes + edges)   │  │
│                          └──────┬───────────────┬───────┘  │
│                                 │               │           │
│                    ┌────────────▼──┐   ┌────────▼────────┐ │
│                    │ JsonGraphStorage│  │ GraphQueryEngine │ │
│                    │ nodes.json     │  │ 10 query methods │ │
│                    │ relationships  │  │ (pure in-memory) │ │
│                    │ .json          │  └─────────────────┘ │
│                    └───────────────┘                       │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                      WebDashboard                           │
│   Web dashboard UI & Minimal API generator endpoint routes  │
└─────────────────────────────────────────────────────────────┘
```

---

## The Problem It Solves

Legacy codebases are hard to understand. They accumulate years of undocumented dependencies, hidden coupling, inheritance chains, and service interactions that no single developer fully knows. Before you can migrate, refactor, or modernise a legacy system, you need to answer questions like:

- What does `OrderService` actually depend on?
- Which classes implement `IRepository`?
- What happens if I change `Customer`?
- Which services call `DatabaseContext` directly?
- What is the full inheritance chain of `BaseRepository`?

This POC answers all of those questions automatically by reading the source code itself and structuring it into a navigable dependency graph.

---

## Where It Is Useful

| Scenario | How This POC Helps |
|---|---|
| **Legacy migration** | Understand the full dependency graph before moving any code — know what breaks if you change a class |
| **Codebase onboarding** | New team members can query the graph instead of reading thousands of lines of code |
| **Architecture validation** | Detect architectural violations — e.g. a `Service` layer class directly calling another `Service` |
| **Refactoring planning** | Find all callers of a method, all implementors of an interface, all classes that `USES` a specific repository |
| **CI/CD enforcement** | Run the graph builder in a pipeline and assert dependency rules (e.g. "no circular USES between services") |
| **Documentation generation** | `nodes.json` + `relationships.json` can feed any visualisation tool — Gephi, D3.js, Cytoscape, Mermaid |
| **Future AI/agent tooling** | The graph files provide structured, self-describing context about the codebase without requiring source file access |
| **Neo4j / graph database import** | The Id-based JSON format maps directly to a property graph — importable with zero transformation |

---

## The Legacy Domain (Analysis Target)

The `LegacyProject` is a simulated enterprise **Order Management System** — realistic enough that Roslyn finds meaningful structure: deep inheritance, constructor-injected dependencies, interface contracts, and cross-layer calls.

> [!NOTE]
> **Project Type & Executability**: `LegacyProject` compiles as a .NET 8 **Class Library** (`.dll`). It is not directly runnable because it does not contain a program entry point. Instead, it serves as a static source code target. The analysis pipeline and graph queries are run by executing the **[WebDashboard](file:///d:/MyDevelopment/LegacyCodeMigration/WebDashboard/Program.cs)** project.

### Layer Overview

| Layer | Classes | What It Contains |
|---|---|---|
| **Base** | `BaseEntity`, `BaseRepository<T>` | Abstract root of all models and repositories |
| **Models** | `Customer`, `Order`, `OrderItem`, `Product`, `Invoice`, `Payment`, `Address` | Domain entities, all extending `BaseEntity` |
| **Enums** | `OrderStatus`, `PaymentStatus`, `CustomerType` | Value types used across the domain |
| **Interfaces** | `IRepository<T>`, `IOrderService`, `ICustomerService`, `IInvoiceService`, `IEmailService` | Contracts for all service and data-access layers |
| **Repositories** | `CustomerRepository`, `OrderRepository`, `ProductRepository`, `InvoiceRepository` | Extend `BaseRepository<T>`, implement `IRepository<T>` |
| **Services** | `OrderService`, `CustomerService`, `InvoiceService`, `PaymentService`, `EmailNotificationService` | Business logic, wired via constructor injection |
| **Infrastructure** | `DatabaseContext` | Mock connection management |
| **Helpers** | `DateHelper`, `PriceCalculator`, `ValidationHelper` | Static utility classes |

### Why This Domain?

It covers every relationship type the graph can extract:

| Pattern | Example |
|---|---|
| Inheritance | `CustomerRepository` → `BaseRepository<Customer>` → `BaseEntity` |
| Interface implementation | `OrderService` → `IOrderService` |
| Constructor DI (USES) | `OrderService` takes 5 dependencies via constructor |
| Method invocations (CALLS) | `OrderService.CreateOrderAsync` calls `CustomerRepository.GetByIdAsync` |
| Object creation (CREATES) | `OrderService` creates `Order`, `OrderItem` via `new` |
| Cross-layer reference | `Order.Customer` property references the `Customer` model |

---

## Output Data Model

Both files are written to `WebDashboard/wwwroot/output/` after every run, making them directly servable and downloadable.

### `nodes.json` — What exists in the codebase

```json
{
  "Id":        "class:LegacyProject.Services.OrderService",
  "Name":      "OrderService",
  "Type":      "Class",
  "Namespace": "LegacyProject.Services",
  "Metadata":  { "IsAbstract": "False", "IsStatic": "False" }
}
```

### `relationships.json` — How things are connected

```json
{
  "FromNodeId":       "class:LegacyProject.Services.OrderService",
  "ToNodeId":         "class:LegacyProject.Repositories.CustomerRepository",
  "RelationshipType": "USES",
  "Properties":       {}
}
```

The two files are linked by `Id` — every `FromNodeId` and `ToNodeId` in `relationships.json` resolves to an `Id` in `nodes.json`. The `Id` prefix (`class:`, `method:`, `interface:`, `namespace:`, …) encodes the node type directly, making the files self-describing.

### Relationship types extracted

`INHERITS` · `IMPLEMENTS` · `USES` · `CALLS` · `CREATES` · `REFERENCES` · `HAS_METHOD` · `HAS_PROPERTY` · `HAS_FIELD` · `HAS_CONSTRUCTOR` · `BELONGS_TO_NAMESPACE` · `BELONGS_TO_PROJECT`

---

## How to Run

```bash
# From the solution root:
dotnet run --project WebDashboard/WebDashboard.csproj
```

No configuration needed. The tool locates `LegacyProject/` automatically.

**Last run results:** 247 nodes · 404 relationships extracted from 30 source files in ~2 seconds.

---

## Extension Points

This POC is intentionally thin at the storage and output layer. The `IGraphStorage` interface means any backend can be added without changing the engine:

- **Neo4j** — implement `Neo4jGraphStorage`, import the same JSON directly
- **Mermaid / GraphViz** — read `relationships.json`, filter by type, emit diagram syntax
- **Architecture tests** — load the graph in a test project, assert no forbidden dependency edges
- **Multi-project** — run `RoslynProjectAnalyzer` over multiple `.csproj` paths, merge into one graph
- **CI enforcement** — fail the pipeline if a new `Service → Service USES` edge appears

---

## Future Migration Roadmap (TODOs)

We plan to use the generated `nodes.json` and `relationships.json` files to automate, track, and validate future service migrations (such as extracting `CustomerService` into a standalone Web API):

- [x] **Automated Endpoint Generation**: Develop a tool that parses method declarations in `nodes.json` to automatically scaffold REST controllers and request/response DTO classes. *(Implemented via [WebApiScaffolder.cs](file:///d:/MyDevelopment/LegacyCodeMigration/WebDashboard/WebApiScaffolder.cs))*
- [/] **Dependency & Impact Analysis**: Trace incoming/outgoing dependencies to assess change impact before refactoring. *(Partially implemented via the interactive Web Dashboard sidebar)*
- [ ] **Client Proxy Generation**: Generate client-side API clients (e.g., `CustomerServiceClient`) using the signatures and parameters parsed from the graph.
- [ ] **Orphaned / Dead Code Clean-up**: Scan for orphaned nodes (classes or methods with zero incoming connections) to clean up legacy technical debt before migration.
- [ ] **Modular Clustering for Microservices**: Use graph clustering algorithms (e.g., community detection) on the relationship data to suggest clean boundaries for splitting the monolith.
- [ ] **Shared Utility Package Isolation**: Identify high-use static utility classes (e.g., helpers) to package them into reusable NuGet libraries rather than duplicating them.
- [ ] **Migration Progress Tracking**: Create a pipeline script that checks the graph after each refactoring step, reporting on the percentage of dependencies successfully decoupled from legacy boundaries.
- [ ] **CI Architecture Guardrails**: Implement automated gateway tests in CI to scan `relationships.json` and block PRs that introduce circular dependencies or violate layer boundaries (e.g., services calling other services directly).

---

## Tech Stack

| Component | Technology |
|---|---|
| Runtime | .NET 8.0 |
| Roslyn | `Microsoft.CodeAnalysis.CSharp` 4.9.2 |
| Serialization | `System.Text.Json` (built-in) |
| External services | **None** |
| AI / LLM / Vector DB | **Not used** |
