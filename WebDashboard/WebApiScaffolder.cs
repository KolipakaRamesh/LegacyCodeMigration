using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WebDashboard
{
    public class WebApiScaffolder
    {
        public class GraphNode
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Namespace { get; set; } = string.Empty;
            public Dictionary<string, string> Metadata { get; set; } = new();
        }

        public class GraphRelationship
        {
            public string FromNodeId { get; set; } = string.Empty;
            public string ToNodeId { get; set; } = string.Empty;
            public string RelationshipType { get; set; } = string.Empty;
        }

        public class ParameterModel
        {
            public string Type { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public static (string Name, string Path) FindLegacyProject(string solutionDir)
        {
            var csprojFiles = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories);
            foreach (var file in csprojFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!name.Equals("WebDashboard", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("KnowledgeGraphEngine", StringComparison.OrdinalIgnoreCase) &&
                    !name.EndsWith(".Api", StringComparison.OrdinalIgnoreCase))
                {
                    return (name, Path.GetDirectoryName(file)!);
                }
            }
            throw new FileNotFoundException("Could not dynamically locate legacy project file in solution.");
        }

        public static async Task<string> MigrateAsync(string solutionDir, string outputDir)
        {
            var nodesPath = Path.Combine(outputDir, "nodes.json");
            var relsPath = Path.Combine(outputDir, "relationships.json");

            if (!File.Exists(nodesPath) || !File.Exists(relsPath))
            {
                throw new FileNotFoundException("Knowledge graph files nodes.json and relationships.json must be generated first.");
            }

            // 1. Discover Legacy Project Name dynamically
            var (legacyProjName, legacyProjDir) = FindLegacyProject(solutionDir);

            // 2. Load Graph Data
            var nodesJson = await File.ReadAllTextAsync(nodesPath);
            var relsJson = await File.ReadAllTextAsync(relsPath);

            var nodes = JsonSerializer.Deserialize<List<GraphNode>>(nodesJson) ?? new();
            var rels = JsonSerializer.Deserialize<List<GraphRelationship>>(relsJson) ?? new();

            // 3. Setup Target Project Directory
            var apiProjDir = Path.Combine(solutionDir, legacyProjName + ".Api");
            if (Directory.Exists(apiProjDir))
            {
                try { Directory.Delete(apiProjDir, true); } catch { }
            }
            Directory.CreateDirectory(apiProjDir);
            Directory.CreateDirectory(Path.Combine(apiProjDir, "Controllers"));
            Directory.CreateDirectory(Path.Combine(apiProjDir, "DTOs"));

            // 4. Migrate All Legacy Code Files (True Migration with dynamic namespace renaming)
            CopyAndRewriteLegacySourceDirectory(legacyProjDir, apiProjDir, legacyProjName);

            // 5. Generate Swagger Example Filter
            await GenerateSwaggerExampleFilterAsync(apiProjDir, legacyProjName);

            // 6. Generate standalone .csproj
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk.Web"">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.5.0"" />
  </ItemGroup>

</Project>";
            await File.WriteAllTextAsync(Path.Combine(apiProjDir, $"{legacyProjName}.Api.csproj"), csprojContent);

            // 7. Pair services dynamically using IMPLEMENTS relationships
            var serviceInterfaces = nodes.Where(n => n.Type == "Interface" && n.Name.EndsWith("Service")).ToList();
            var serviceClasses = nodes.Where(n => n.Type == "Class" && n.Name.EndsWith("Service")).ToList();

            var servicePairs = new List<(GraphNode? InterfaceNode, GraphNode ClassNode)>();
            var matchedClassIds = new HashSet<string>();

            foreach (var iface in serviceInterfaces)
            {
                // Find implementing class in relationships
                var implRel = rels.FirstOrDefault(r => r.ToNodeId == iface.Id && r.RelationshipType == "IMPLEMENTS");
                if (implRel != null)
                {
                    var cls = serviceClasses.FirstOrDefault(c => c.Id == implRel.FromNodeId);
                    if (cls != null)
                    {
                        servicePairs.Add((iface, cls));
                        matchedClassIds.Add(cls.Id);
                        continue;
                    }
                }

                // Fallback: If no relationship, look for class with name without "I"
                var expectedClassName = iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1])
                    ? iface.Name.Substring(1)
                    : iface.Name;
                var fallbackCls = serviceClasses.FirstOrDefault(c => c.Name == expectedClassName);
                if (fallbackCls != null)
                {
                    servicePairs.Add((iface, fallbackCls));
                    matchedClassIds.Add(fallbackCls.Id);
                }
            }

            // Add standalone classes (classes ending with "Service" that weren't matched to any interface)
            foreach (var cls in serviceClasses)
            {
                if (!matchedClassIds.Contains(cls.Id))
                {
                    servicePairs.Add((null, cls));
                }
            }

            // 8. Generate Controllers
            foreach (var pair in servicePairs)
            {
                var serviceNode = pair.InterfaceNode ?? pair.ClassNode;

                var methodRels = rels.Where(r => r.FromNodeId == serviceNode.Id && r.RelationshipType == "HAS_METHOD").ToList();
                var serviceMethods = new List<GraphNode>();
                foreach (var mr in methodRels)
                {
                    var mNode = nodes.FirstOrDefault(n => n.Id == mr.ToNodeId);
                    if (mNode != null)
                    {
                        // Check if it's public. Default to True if metadata not populated.
                        var isPublic = mNode.Metadata.GetValueOrDefault("IsPublic", "True");
                        if (isPublic.Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            serviceMethods.Add(mNode);
                        }
                    }
                }

                await GenerateControllerAsync(apiProjDir, serviceNode, serviceMethods, legacyProjName);
            }

            // 9. Generate Program.cs dynamically registering discovered entities from the graph
            await GenerateProgramCsAsync(apiProjDir, servicePairs, nodes, legacyProjName);

            return apiProjDir;
        }

        private static void CopyAndRewriteLegacySourceDirectory(string sourceDir, string targetDir, string legacyProjName)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var ext = Path.GetExtension(file);
                // Skip csproj and user files, only migrate source code files
                if (ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase) || 
                    ext.Equals(".user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var targetFile = Path.Combine(targetDir, Path.GetFileName(file));
                
                if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    var content = File.ReadAllText(file);
                    // Rewrite namespaces and usings dynamically to {legacyProjName}.Api
                    content = content.Replace("namespace " + legacyProjName, "namespace " + legacyProjName + ".Api")
                                     .Replace("using " + legacyProjName, "using " + legacyProjName + ".Api");
                    File.WriteAllText(targetFile, content);
                }
                else
                {
                    File.Copy(file, targetFile, true);
                }
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                // Skip transient compilation/build artifacts directories
                if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) || 
                    dirName.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                    dirName.StartsWith("."))
                {
                    continue;
                }
                CopyAndRewriteLegacySourceDirectory(subDir, Path.Combine(targetDir, dirName), legacyProjName);
            }
        }

        private static async Task GenerateSwaggerExampleFilterAsync(string apiProjDir, string legacyProjName)
        {
            var content = $@"using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;

namespace {legacyProjName}.Api
{{
    public class SwaggerExampleSchemaFilter : ISchemaFilter
    {{
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {{
            if (schema.Properties == null) return;

            foreach (var property in schema.Properties)
            {{
                var name = property.Key;
                var propSchema = property.Value;
                var type = context.Type.GetProperty(name, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (type == null) continue;

                if (type.PropertyType == typeof(string))
                {{
                    if (name.Contains(""email"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""john.doe@example.com"");
                    else if (name.Contains(""first"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""John"");
                    else if (name.Contains(""last"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""Doe"");
                    else if (name.Contains(""phone"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""+1-555-0199"");
                    else if (name.Contains(""street"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""123 Main Street"");
                    else if (name.Contains(""city"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""New York"");
                    else if (name.Contains(""state"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""NY"");
                    else if (name.Contains(""zip"", StringComparison.OrdinalIgnoreCase) || name.Contains(""postal"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""10001"");
                    else if (name.Contains(""country"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""USA"");
                    else if (name.Contains(""number"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiString(""INV-2026-001"");
                    else
                        propSchema.Example = new OpenApiString(""Sample "" + name);
                }}
                else if (type.PropertyType == typeof(Guid))
                {{
                    propSchema.Example = new OpenApiString(Guid.NewGuid().ToString());
                }}
                else if (type.PropertyType == typeof(int) || type.PropertyType == typeof(int?))
                {{
                    if (name.Contains(""quantity"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiInteger(2);
                    else
                        propSchema.Example = new OpenApiInteger(1);
                }}
                else if (type.PropertyType == typeof(decimal) || type.PropertyType == typeof(decimal?))
                {{
                    if (name.Contains(""limit"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiDouble(5000.00);
                    else if (name.Contains(""price"", StringComparison.OrdinalIgnoreCase) || name.Contains(""amount"", StringComparison.OrdinalIgnoreCase))
                        propSchema.Example = new OpenApiDouble(99.99);
                    else
                        propSchema.Example = new OpenApiDouble(10.00);
                }}
                else if (type.PropertyType == typeof(double) || type.PropertyType == typeof(double?))
                {{
                    propSchema.Example = new OpenApiDouble(1.0);
                }}
                else if (type.PropertyType == typeof(bool) || type.PropertyType == typeof(bool?))
                {{
                    propSchema.Example = new OpenApiBoolean(true);
                }}
                else if (type.PropertyType == typeof(DateTime) || type.PropertyType == typeof(DateTime?))
                {{
                    propSchema.Example = new OpenApiString(DateTime.UtcNow.ToString(""yyyy-MM-ddTHH:mm:ssZ""));
                }}
            }}
        }}
    }}
}}";
            await File.WriteAllTextAsync(Path.Combine(apiProjDir, "SwaggerExampleSchemaFilter.cs"), content);
        }

        private static async Task GenerateControllerAsync(string apiProjDir, GraphNode serviceNode, List<GraphNode> methods, string legacyProjName)
        {
            var cleanServiceName = serviceNode.Name.StartsWith("I") && serviceNode.Name.Length > 1 && char.IsUpper(serviceNode.Name[1])
                ? serviceNode.Name.Substring(1) 
                : serviceNode.Name;

            var controllerName = cleanServiceName.EndsWith("Service") 
                ? cleanServiceName.Substring(0, cleanServiceName.Length - "Service".Length) + "sController"
                : cleanServiceName + "sController";

            var sb = new StringBuilder();
            sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
            sb.AppendLine($"using {legacyProjName}.Api.Interfaces;");
            sb.AppendLine($"using {legacyProjName}.Api.Models;");
            sb.AppendLine($"using {legacyProjName}.Api.Services;");
            sb.AppendLine($"using {legacyProjName}.Api.DTOs;");
            sb.AppendLine();
            sb.AppendLine($"namespace {legacyProjName}.Api.Controllers;");
            sb.AppendLine();
            sb.AppendLine("[ApiController]");
            sb.AppendLine($"[Route(\"api/[controller]\")]");
            sb.AppendLine($"public class {controllerName} : ControllerBase");
            sb.AppendLine("{");

            // Dependency Injection fields
            var serviceFieldName = "_" + serviceNode.Name.Substring(0, 1).ToLower() + serviceNode.Name.Substring(1);
            sb.AppendLine($"    private readonly {serviceNode.Name} {serviceFieldName};");
            sb.AppendLine();
            sb.AppendLine($"    public {controllerName}({serviceNode.Name} service)");
            sb.AppendLine("    {");
            sb.AppendLine($"        {serviceFieldName} = service;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // Generate Action methods dynamically
            foreach (var method in methods)
            {
                var returnType = method.Metadata.GetValueOrDefault("ReturnType", "System.Threading.Tasks.Task");
                var cleanReturnType = GetCleanReturnType(returnType);
                bool isVoid = string.IsNullOrEmpty(cleanReturnType);

                var parametersStr = method.Metadata.GetValueOrDefault("Parameters", "");
                var parameters = ParseParameters(parametersStr);

                var cleanMethodName = method.Name.EndsWith("Async") 
                    ? method.Name.Substring(0, method.Name.Length - "Async".Length) 
                    : method.Name;

                // Determine HTTP verb based on name prefix
                var verbAttribute = "HttpPost";
                if (cleanMethodName.StartsWith("Get") || cleanMethodName.StartsWith("Find") || cleanMethodName.StartsWith("List") || cleanMethodName.StartsWith("Read"))
                {
                    verbAttribute = "HttpGet";
                }
                else if (cleanMethodName.StartsWith("Update") || cleanMethodName.StartsWith("Modify") || cleanMethodName.StartsWith("Set") || cleanMethodName.StartsWith("Edit"))
                {
                    verbAttribute = "HttpPut";
                }
                else if (cleanMethodName.StartsWith("Delete") || cleanMethodName.StartsWith("Remove") || cleanMethodName.StartsWith("Deactivate") || cleanMethodName.StartsWith("Cancel"))
                {
                    verbAttribute = "HttpDelete";
                }

                // Determine routing template and parameter binding
                if (parameters.Count == 0)
                {
                    sb.AppendLine($"    [{verbAttribute}(\"{cleanMethodName}\")]");
                    sb.AppendLine($"    public async Task<IActionResult> {cleanMethodName}()");
                    sb.AppendLine("    {");
                    if (isVoid)
                    {
                        sb.AppendLine($"        await {serviceFieldName}.{method.Name}();");
                        sb.AppendLine("        return Ok();");
                    }
                    else
                    {
                        sb.AppendLine($"        var result = await {serviceFieldName}.{method.Name}();");
                        sb.AppendLine("        return Ok(result);");
                    }
                    sb.AppendLine("    }");
                }
                else if (parameters.Count == 1)
                {
                    var param = parameters[0];

                    if (param.Type.Contains("("))
                    {
                        // Tuple parameter
                        var resolved = ResolveTupleParameter(param.Type, param.Name, apiProjDir, legacyProjName);
                        var dtoClassName = $"{cleanMethodName}RequestDto";
                        await GenerateMethodDtoAsync(apiProjDir, dtoClassName, new List<ParameterModel> { param }, legacyProjName);

                        sb.AppendLine($"    [{verbAttribute}(\"{cleanMethodName}\")]");
                        sb.AppendLine($"    public async Task<IActionResult> {cleanMethodName}([FromBody] {dtoClassName} dto)");
                        sb.AppendLine("    {");
                        sb.AppendLine($"        var {param.Name}Tuple = {resolved.ConvertExpression.Replace("dto.", "dto." + Capitalize(param.Name))};");
                        if (isVoid)
                        {
                            sb.AppendLine($"        await {serviceFieldName}.{method.Name}({param.Name}Tuple);");
                            sb.AppendLine("        return Ok();");
                        }
                        else
                        {
                            sb.AppendLine($"        var result = await {serviceFieldName}.{method.Name}({param.Name}Tuple);");
                            sb.AppendLine("        return Ok(result);");
                        }
                        sb.AppendLine("    }");
                    }
                    else if (IsSimpleType(param.Type))
                    {
                        var resolvedParamType = param.Type.Replace(legacyProjName + ".", legacyProjName + ".Api.");

                        sb.AppendLine($"    [{verbAttribute}(\"{cleanMethodName}/{{{param.Name}}}\")]");
                        sb.AppendLine($"    public async Task<IActionResult> {cleanMethodName}({resolvedParamType} {param.Name})");
                        sb.AppendLine("    {");
                        if (isVoid)
                        {
                            sb.AppendLine($"        await {serviceFieldName}.{method.Name}({param.Name});");
                            sb.AppendLine("        return Ok();");
                        }
                        else
                        {
                            sb.AppendLine($"        var result = await {serviceFieldName}.{method.Name}({param.Name});");
                            sb.AppendLine("        return Ok(result);");
                        }
                        sb.AppendLine("    }");
                    }
                    else
                    {
                        var resolvedParamType = param.Type.Replace(legacyProjName + ".", legacyProjName + ".Api.");

                        sb.AppendLine($"    [{verbAttribute}(\"{cleanMethodName}\")]");
                        sb.AppendLine($"    public async Task<IActionResult> {cleanMethodName}([FromBody] {resolvedParamType} {param.Name})");
                        sb.AppendLine("    {");
                        if (isVoid)
                        {
                            sb.AppendLine($"        await {serviceFieldName}.{method.Name}({param.Name});");
                            sb.AppendLine("        return Ok();");
                        }
                        else
                        {
                            sb.AppendLine($"        var result = await {serviceFieldName}.{method.Name}({param.Name});");
                            sb.AppendLine("        return Ok(result);");
                        }
                        sb.AppendLine("    }");
                    }
                }
                else
                {
                    // Multiple parameters require a DTO
                    var dtoClassName = $"{cleanMethodName}RequestDto";
                    await GenerateMethodDtoAsync(apiProjDir, dtoClassName, parameters, legacyProjName);

                    sb.AppendLine($"    [{verbAttribute}(\"{cleanMethodName}\")]");
                    sb.AppendLine($"    public async Task<IActionResult> {cleanMethodName}([FromBody] {dtoClassName} dto)");
                    sb.AppendLine("    {");

                    // Map parameters to call, translating custom tuple collections if encountered
                    var callArgs = new List<string>();
                    foreach (var param in parameters)
                    {
                        if (param.Type.Contains("("))
                        {
                            var resolved = ResolveTupleParameter(param.Type, $"dto.{Capitalize(param.Name)}", apiProjDir, legacyProjName);
                            sb.AppendLine($"        var {param.Name}Tuple = {resolved.ConvertExpression};");
                            callArgs.Add($"{param.Name}Tuple");
                        }
                        else
                        {
                            callArgs.Add($"dto.{Capitalize(param.Name)}");
                        }
                    }

                    if (isVoid)
                    {
                        sb.AppendLine($"        await {serviceFieldName}.{method.Name}({string.Join(", ", callArgs)});");
                        sb.AppendLine("        return Ok();");
                    }
                    else
                    {
                        sb.AppendLine($"        var result = await {serviceFieldName}.{method.Name}({string.Join(", ", callArgs)});");
                        sb.AppendLine("        return Ok(result);");
                    }
                    sb.AppendLine("    }");
                }
                sb.AppendLine();
            }

            sb.AppendLine("}");

            await File.WriteAllTextAsync(Path.Combine(apiProjDir, "Controllers", $"{controllerName}.cs"), sb.ToString());
        }

        private static async Task GenerateMethodDtoAsync(string apiProjDir, string dtoClassName, List<ParameterModel> parameters, string legacyProjName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine($"using {legacyProjName}.Api.Models;");
            sb.AppendLine();
            sb.AppendLine($"namespace {legacyProjName}.Api.DTOs;");
            sb.AppendLine();
            sb.AppendLine($"public class {dtoClassName}");
            sb.AppendLine("{");

            foreach (var param in parameters)
            {
                var targetType = param.Type.Replace(legacyProjName + ".", legacyProjName + ".Api.");
                if (targetType.Contains("("))
                {
                    var resolved = ResolveTupleParameter(targetType, param.Name, apiProjDir, legacyProjName);
                    targetType = resolved.MappedType;
                }
                sb.AppendLine($"    public {targetType} {Capitalize(param.Name)} {{ get; set; }} = default!;");
            }

            sb.AppendLine("}");

            await File.WriteAllTextAsync(Path.Combine(apiProjDir, "DTOs", $"{dtoClassName}.cs"), sb.ToString());
        }

        private static (string MappedType, string ConvertExpression) ResolveTupleParameter(string paramType, string paramName, string apiProjDir, string legacyProjName)
        {
            int openIdx = paramType.IndexOf('(');
            int closeIdx = paramType.LastIndexOf(')');
            if (openIdx >= 0 && closeIdx > openIdx)
            {
                string tupleContent = paramType.Substring(openIdx + 1, closeIdx - openIdx - 1);
                var fields = tupleContent.Split(',').Select(f => f.Trim()).ToList();
                var propBuilders = new List<string>();
                var selectFields = new List<string>();

                foreach (var field in fields)
                {
                    var parts = field.Split(' ');
                    if (parts.Length >= 2)
                    {
                        var fType = parts[0].Replace(legacyProjName + ".", legacyProjName + ".Api.");
                        var fName = parts[1];
                        var capName = char.ToUpper(fName[0]) + fName.Substring(1);
                        propBuilders.Add($"    public {fType} {capName} {{ get; set; }} = default!;");
                        selectFields.Add($"x.{capName}");
                    }
                }

                // Generate a name for the DTO class dynamically based on parameter name
                var dtoName = string.Join("", fields.Select(f => {
                    var parts = f.Split(' ');
                    var name = parts.Length >= 2 ? parts[1] : "Field";
                    return char.ToUpper(name[0]) + name.Substring(1);
                })) + "Dto";

                // Generate class DTO
                var dtoFileContent = $@"namespace {legacyProjName}.Api.DTOs;

public class {dtoName}
{{
{string.Join("\n", propBuilders)}
}}";
                string filePath = Path.Combine(apiProjDir, "DTOs", $"{dtoName}.cs");
                File.WriteAllText(filePath, dtoFileContent);

                // Replace ValueTuple inside the generic wrapper
                var cleanType = paramType.Substring(0, openIdx) + dtoName + paramType.Substring(closeIdx + 1);
                cleanType = cleanType.Replace("System.Collections.Generic.List", "List")
                                     .Replace("System.Collections.Generic.IEnumerable", "IEnumerable");

                // Mapping expression back to ValueTuple: items.Select(x => (x.ProductId, x.Quantity)).ToList()
                var conversion = $"{paramName}.Select(x => ({string.Join(", ", selectFields)})).ToList()";

                return (cleanType, conversion);
            }
            return (paramType, paramName);
        }

        private static async Task GenerateProgramCsAsync(string apiProjDir, List<(GraphNode? InterfaceNode, GraphNode ClassNode)> servicePairs, List<GraphNode> nodes, string legacyProjName)
        {
            // 1. Discover all unique namespaces of classes and interfaces in the graph
            var namespaces = nodes
                .Where(n => n.Type == "Class" || n.Type == "Interface" || n.Type == "Enum")
                .Select(n => n.Namespace)
                .Where(ns => !string.IsNullOrEmpty(ns))
                .Distinct()
                .OrderBy(ns => ns)
                .ToList();

            // 2. Discover context classes (DatabaseContext / DbContext)
            var contextNodes = nodes
                .Where(n => n.Type == "Class" && 
                            (n.Name.EndsWith("Context") || n.Name.Contains("Context")) &&
                            n.Metadata.GetValueOrDefault("IsAbstract", "False") == "False")
                .ToList();

            // 3. Discover repository classes (excluding abstract BaseRepository)
            var repositoryNodes = nodes
                .Where(n => n.Type == "Class" && 
                            n.Name.EndsWith("Repository") &&
                            n.Metadata.GetValueOrDefault("IsAbstract", "False") == "False")
                .ToList();

            var sb = new StringBuilder();
            
            // Generate namespace usings dynamically, rewriting legacyProjName to legacyProjName.Api
            foreach (var ns in namespaces)
            {
                var apiNs = ns.Replace(legacyProjName, legacyProjName + ".Api");
                sb.AppendLine($"using {apiNs};");
            }
            sb.AppendLine($"using {legacyProjName}.Api.DTOs;");
            sb.AppendLine();
            sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
            sb.AppendLine();
            sb.AppendLine("// Explicitly bind the API to port 5002");
            sb.AppendLine("builder.WebHost.UseUrls(\"http://localhost:5002\");");
            sb.AppendLine();
            sb.AppendLine("builder.Services.AddControllers();");
            sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
            
            // Register SchemaFilter dynamically
            sb.AppendLine("builder.Services.AddSwaggerGen(c =>");
            sb.AppendLine("{");
            sb.AppendLine($"    c.SchemaFilter<{legacyProjName}.Api.SwaggerExampleSchemaFilter>();");
            sb.AppendLine("});");
            sb.AppendLine();

            sb.AppendLine("builder.Services.AddCors(options =>");
            sb.AppendLine("{");
            sb.AppendLine("    options.AddPolicy(\"AllowAll\", policy =>");
            sb.AppendLine("    {");
            sb.AppendLine("        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();");
            sb.AppendLine("    });");
            sb.AppendLine("});");
            sb.AppendLine();
            
            // Register Contexts Discovered via Knowledge Graph
            foreach (var ctx in contextNodes)
            {
                sb.AppendLine($"builder.Services.AddSingleton<{ctx.Name}>();");
            }
            sb.AppendLine();

            // Register Repositories Discovered via Knowledge Graph
            foreach (var repo in repositoryNodes)
            {
                sb.AppendLine($"builder.Services.AddSingleton<{repo.Name}>();");
            }
            sb.AppendLine();

            // Register Services Discovered via Knowledge Graph
            foreach (var pair in servicePairs)
            {
                if (pair.InterfaceNode != null)
                {
                    sb.AppendLine($"builder.Services.AddScoped<{pair.InterfaceNode.Name}, {pair.ClassNode.Name}>();");
                }
                else
                {
                    sb.AppendLine($"builder.Services.AddScoped<{pair.ClassNode.Name}>();");
                }
            }

            sb.AppendLine();
            sb.AppendLine("var app = builder.Build();");
            sb.AppendLine();
            sb.AppendLine("app.UseSwagger();");
            sb.AppendLine("app.UseSwaggerUI();");
            sb.AppendLine("app.UseCors(\"AllowAll\");");
            sb.AppendLine("app.UseAuthorization();");
            sb.AppendLine("app.MapControllers();");
            sb.AppendLine();
            sb.AppendLine("app.Run();");

            await File.WriteAllTextAsync(Path.Combine(apiProjDir, "Program.cs"), sb.ToString());
        }

        public static List<ParameterModel> ParseParameters(string parametersStr)
        {
            var result = new List<ParameterModel>();
            if (string.IsNullOrWhiteSpace(parametersStr)) return result;

            var rawParams = new List<string>();
            var current = new StringBuilder();
            int depth = 0;

            for (int i = 0; i < parametersStr.Length; i++)
            {
                char c = parametersStr[i];
                if (c == '<' || c == '(' || c == '[') depth++;
                else if (c == '>' || c == ')' || c == ']') depth--;

                if (c == ',' && depth == 0)
                {
                    rawParams.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            if (current.Length > 0)
            {
                rawParams.Add(current.ToString().Trim());
            }

            foreach (var rp in rawParams)
            {
                var trimmed = rp.Trim();
                int lastSpace = trimmed.LastIndexOf(' ');
                if (lastSpace > 0)
                {
                    var type = trimmed.Substring(0, lastSpace).Trim();
                    var name = trimmed.Substring(lastSpace + 1).Trim();
                    result.Add(new ParameterModel { Type = type, Name = name });
                }
            }

            return result;
        }

        private static bool IsSimpleType(string type)
        {
            var lower = type.ToLower();
            return lower == "string" || lower == "int" || lower == "guid" || lower == "bool" || 
                   lower == "datetime" || lower == "decimal" || lower == "double" || lower == "float" ||
                   type.Contains("Enum");
        }

        private static string GetCleanReturnType(string returnType)
        {
            var clean = returnType.Replace("System.Threading.Tasks.", "").Trim();
            if (clean.StartsWith("Task<") && clean.EndsWith(">"))
            {
                return clean.Substring(5, clean.Length - 6);
            }
            if (clean == "Task" || clean == "void")
            {
                return "";
            }
            return clean;
        }

        private static string Capitalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpper(text[0]) + text.Substring(1);
        }
    }
}
