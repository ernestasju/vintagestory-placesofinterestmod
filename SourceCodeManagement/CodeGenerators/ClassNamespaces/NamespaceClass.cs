using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static SourceCodeManagement.Extensions;

namespace SourceCodeManagement.CodeGenerators.ClassNamespaces;

internal sealed class NamespaceClass
{
    private NamespaceClass()
    {
    }

    public string ClassFileName { get; set; }

    public required string ClassNamespace { get; init; }

    public required string ClassName { get; init; }

    public required bool HasUserDefinedConstructor { get; init; }

    public required string NamespaceName { get; init; }

    public required SyntaxKind NewClassAccessibility { get; init; }

    public NamespaceClass? Parent { get; init; }

    public required List<NamespaceClass> ChildClassNamespaces { get; init; }

    public IEnumerable<NamespaceClass> Parents
    {
        get
        {
            var current = Parent;
            while (current is not null)
            {
                yield return current;
                current = current.Parent;
            }
        }
    }

    public static NamespaceClass? ParseSyntax(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;
        var classSymbol = (INamedTypeSymbol)context.TargetSymbol;

        // Only process Demo1 for now
        if (classSymbol.Name != "Demo1")
        {
            return null;
        }

        return new()
        {
            ClassFileName = classSymbol.FullSymbolPath + ".g.cs",
            ClassNamespace = classSymbol.FullNamespace,
            ClassName = classSymbol.Name,
            HasUserDefinedConstructor = false,
            NamespaceName = "This",
            NewClassAccessibility = SyntaxKind.ProtectedKeyword,
            Parent = null,
            ChildClassNamespaces = []
        };
    }

    public static void AddSource(
        SourceProductionContext context,
        NamespaceClass namespaceClass)
    {
        var compilationUnit = namespaceClass.ToCompilationUnitSyntax();
        var normalizedSource = compilationUnit
            .NormalizeWhitespace()
            .ToFullString();
        context.AddSource(namespaceClass.ClassFileName, normalizedSource);
    }

    public CompilationUnitSyntax ToCompilationUnitSyntax()
    {
        var namespaceDecl = NamespaceDeclaration(ParseName(ClassNamespace));
        return CompilationUnit().AddMembers(namespaceDecl);
    }

    public ClassDeclarationSyntax ToClassDeclarationSyntax()
    {
        throw new NotImplementedException();
    }

    private ConstructorDeclarationSyntax GenerateConstructor()
    {
        throw new NotImplementedException();
    }

    private static string CreateParentChain(int depth)
    {
        throw new NotImplementedException();
    }
}
