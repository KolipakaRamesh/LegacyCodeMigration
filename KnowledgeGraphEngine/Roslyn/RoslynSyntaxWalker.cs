using KnowledgeGraphEngine.Roslyn.DTOs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KnowledgeGraphEngine.Roslyn;

/// <summary>
/// A <see cref="CSharpSyntaxWalker"/> that visits every relevant declaration node
/// in a single C# source file and populates the corresponding metadata collections.
/// </summary>
public class RoslynSyntaxWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;

    // ── Output collections ────────────────────────────────────────────────────
    public List<ClassMetadata>     Classes    { get; } = new();
    public List<InterfaceMetadata> Interfaces { get; } = new();
    public List<EnumMetadata>      Enums      { get; } = new();

    // ── Visitor state ─────────────────────────────────────────────────────────
    private ClassMetadata?     _currentClass;
    private InterfaceMetadata? _currentInterface;
    private string             _currentNamespace = string.Empty;

    public RoslynSyntaxWalker(SemanticModel semanticModel)
        : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
    }

    // ── Namespace visitors ────────────────────────────────────────────────────

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        var prev = _currentNamespace;
        _currentNamespace = node.Name.ToString();
        base.VisitNamespaceDeclaration(node);
        _currentNamespace = prev;
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        var prev = _currentNamespace;
        _currentNamespace = node.Name.ToString();
        base.VisitFileScopedNamespaceDeclaration(node);
        _currentNamespace = prev;
    }

    // ── Class visitor ─────────────────────────────────────────────────────────

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
        var ns     = symbol?.ContainingNamespace.ToDisplayString() ?? _currentNamespace;

        var cls = new ClassMetadata
        {
            Name                = node.Identifier.Text,
            FullyQualifiedName  = symbol?.ToDisplayString() ?? $"{ns}.{node.Identifier.Text}",
            Namespace           = ns,
            IsAbstract          = node.Modifiers.Any(SyntaxKind.AbstractKeyword),
            IsStatic            = node.Modifiers.Any(SyntaxKind.StaticKeyword),
            IsSealed            = node.Modifiers.Any(SyntaxKind.SealedKeyword),
            UsingDirectives     = ExtractUsingDirectives(node)
        };

        // ── Base type list (inheritance + interface implementation) ──────────
        if (node.BaseList != null)
        {
            foreach (var baseTypeSyntax in node.BaseList.Types)
            {
                var typeInfo = _semanticModel.GetTypeInfo(baseTypeSyntax.Type);
                if (typeInfo.Type is INamedTypeSymbol typeSymbol)
                {
                    var shortName = StripGenerics(typeSymbol.Name);
                    if (typeSymbol.TypeKind == TypeKind.Interface)
                        cls.ImplementedInterfaces.Add(shortName);
                    else
                        cls.BaseClass ??= shortName;
                }
                else
                {
                    // Fallback to syntax text
                    var text = baseTypeSyntax.Type.ToString();
                    var stripped = StripGenerics(text.Contains('.') ? text.Split('.').Last() : text);
                    if (stripped.StartsWith('I') && stripped.Length > 1 && char.IsUpper(stripped[1]))
                        cls.ImplementedInterfaces.Add(stripped);
                    else
                        cls.BaseClass ??= stripped;
                }
            }
        }

        var prevClass = _currentClass;
        _currentClass     = cls;
        _currentInterface = null;

        base.VisitClassDeclaration(node);

        Classes.Add(cls);
        _currentClass = prevClass;
    }

    // ── Interface visitor ─────────────────────────────────────────────────────

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
        var ns     = symbol?.ContainingNamespace.ToDisplayString() ?? _currentNamespace;

        var iface = new InterfaceMetadata
        {
            Name               = node.Identifier.Text,
            FullyQualifiedName = symbol?.ToDisplayString() ?? $"{ns}.{node.Identifier.Text}",
            Namespace          = ns
        };

        if (node.BaseList != null)
        {
            iface.BaseInterfaces = node.BaseList.Types
                .Select(t => StripGenerics(t.Type.ToString()))
                .ToList();
        }

        var prevInterface = _currentInterface;
        var prevClass     = _currentClass;
        _currentInterface = iface;
        _currentClass     = null;

        base.VisitInterfaceDeclaration(node);

        Interfaces.Add(iface);
        _currentInterface = prevInterface;
        _currentClass     = prevClass;
    }

    // ── Enum visitor ──────────────────────────────────────────────────────────

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        var ns     = symbol?.ContainingNamespace.ToDisplayString() ?? _currentNamespace;

        Enums.Add(new EnumMetadata
        {
            Name               = node.Identifier.Text,
            FullyQualifiedName = symbol?.ToDisplayString() ?? $"{ns}.{node.Identifier.Text}",
            Namespace          = ns,
            Members            = node.Members.Select(m => m.Identifier.Text).ToList()
        });
    }

    // ── Method visitor ────────────────────────────────────────────────────────

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var method = new MethodMetadata
        {
            Name        = node.Identifier.Text,
            ReturnType  = node.ReturnType.ToString(),
            IsAsync     = node.Modifiers.Any(SyntaxKind.AsyncKeyword),
            IsStatic    = node.Modifiers.Any(SyntaxKind.StaticKeyword),
            IsOverride  = node.Modifiers.Any(SyntaxKind.OverrideKeyword),
            IsVirtual   = node.Modifiers.Any(SyntaxKind.VirtualKeyword),
            IsAbstract  = node.Modifiers.Any(SyntaxKind.AbstractKeyword),
            Parameters  = ExtractParameters(node.ParameterList)
        };

        // ── Scan the body for invocations and object creations ───────────────
        SyntaxNode? body = (SyntaxNode?)node.Body ?? node.ExpressionBody;
        if (body != null)
        {
            ExtractInvocations(body, method);
            ExtractObjectCreations(body, method);
        }

        if (_currentClass != null)
        {
            _currentClass.Methods.Add(method);
            _currentClass.MethodInvocations.AddRange(method.InvokedMethods);
            _currentClass.ObjectCreations.AddRange(method.CreatedTypes);
        }
        else if (_currentInterface != null)
        {
            _currentInterface.Methods.Add(method);
        }

        base.VisitMethodDeclaration(node);
    }

    // ── Constructor visitor ───────────────────────────────────────────────────

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (_currentClass == null)
        {
            base.VisitConstructorDeclaration(node);
            return;
        }

        var ctor = new ConstructorMetadata
        {
            ClassName  = node.Identifier.Text,
            Parameters = ExtractParameters(node.ParameterList)
        };

        _currentClass.Constructors.Add(ctor);

        base.VisitConstructorDeclaration(node);
    }

    // ── Property visitor ──────────────────────────────────────────────────────

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        var prop = new PropertyMetadata
        {
            Name      = node.Identifier.Text,
            Type      = node.Type.ToString(),
            HasGetter = node.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration))
                        ?? node.ExpressionBody != null,
            HasSetter = node.AccessorList?.Accessors.Any(a =>
                            a.IsKind(SyntaxKind.SetAccessorDeclaration) ||
                            a.IsKind(SyntaxKind.InitAccessorDeclaration)) ?? false,
            IsStatic  = node.Modifiers.Any(SyntaxKind.StaticKeyword)
        };

        if (_currentClass != null)
            _currentClass.Properties.Add(prop);
        else if (_currentInterface != null)
            _currentInterface.Properties.Add(prop);
    }

    // ── Field visitor ─────────────────────────────────────────────────────────

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (_currentClass == null) return;

        var typeName  = node.Declaration.Type.ToString();
        var isReadonly = node.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        var isStatic   = node.Modifiers.Any(SyntaxKind.StaticKeyword);
        var isConst    = node.Modifiers.Any(SyntaxKind.ConstKeyword);

        foreach (var variable in node.Declaration.Variables)
        {
            _currentClass.Fields.Add(new FieldMetadata
            {
                Name       = variable.Identifier.Text,
                Type       = typeName,
                IsReadonly = isReadonly,
                IsStatic   = isStatic,
                IsConst    = isConst
            });
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private List<ParameterInfo> ExtractParameters(ParameterListSyntax list)
    {
        return list.Parameters.Select(p => new ParameterInfo
        {
            Name = p.Identifier.Text,
            Type = StripGenerics(p.Type?.ToString() ?? "object")
        }).ToList();
    }

    private void ExtractInvocations(SyntaxNode body, MethodMetadata method)
    {
        foreach (var inv in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var methodSymbol = _semanticModel.GetSymbolInfo(inv).Symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                var containingType = methodSymbol.ContainingType?.Name ?? "Unknown";
                method.InvokedMethods.Add($"{containingType}.{methodSymbol.Name}");
            }
            else
            {
                // Fallback: use the raw expression text
                var raw = inv.Expression.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                    method.InvokedMethods.Add(raw);
            }
        }
    }

    private void ExtractObjectCreations(SyntaxNode body, MethodMetadata method)
    {
        foreach (var creation in body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var typeInfo = _semanticModel.GetTypeInfo(creation).Type;
            method.CreatedTypes.Add(typeInfo?.Name ?? creation.Type.ToString());
        }

        // Also capture implicit new (e.g. `new() { ... }` target-typed)
        foreach (var creation in body.DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>())
        {
            var typeInfo = _semanticModel.GetTypeInfo(creation).Type;
            if (typeInfo != null)
                method.CreatedTypes.Add(typeInfo.Name);
        }
    }

    private static List<string> ExtractUsingDirectives(SyntaxNode node)
    {
        return node.SyntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    /// <summary>Strips generic type parameters, e.g. "List&lt;T&gt;" → "List".</summary>
    private static string StripGenerics(string typeName)
    {
        var idx = typeName.IndexOf('<');
        return idx >= 0 ? typeName[..idx] : typeName;
    }
}
