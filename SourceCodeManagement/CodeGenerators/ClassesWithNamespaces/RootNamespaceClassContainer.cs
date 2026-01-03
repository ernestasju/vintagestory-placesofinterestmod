using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal sealed partial class RootNamespaceClassContainer
{
    private RootNamespaceClassContainer()
    {
    }

    public required string Namespace { get; init; }
    public required string[] ParentClasses { get; init; }
    public required NamespaceClass RootNamespaceClass { get; init; }

    #region Parser
    public static IncrementalValuesProvider<RootNamespaceClassContainer> FindClassesWithMarkerAttribute(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MarkerAttribute.Name,
                (node, _) => IsValidSyntax(node),
                (ctx, _) => ParseSyntax((ClassDeclarationSyntax)ctx.TargetNode))
            .Where(container => container is not null)
            .Select((container, _) => container!);
    }

    private static bool IsValidSyntax(SyntaxNode node)
    {
        // NOTE: As long as the ClassWithNamespacesAttribute is on the class it is good enough for us.
        return node is ClassDeclarationSyntax;
    }

    private static RootNamespaceClassContainer? ParseSyntax(ClassDeclarationSyntax classDeclaration)
    {
        var rootNamespaceClass = NamespaceClass.ParseSyntax(classDeclaration);
        if (rootNamespaceClass is null)
        {
            return null;
        }

        var namespaceName = GetNamespaceName(classDeclaration);
        var parentClasses = GetParentClasses(classDeclaration);

        return new RootNamespaceClassContainer
        {
            Namespace = namespaceName,
            ParentClasses = parentClasses,
            RootNamespaceClass = rootNamespaceClass,
        };
    }

    private static string GetNamespaceName(ClassDeclarationSyntax classDeclaration)
    {
        var namespaceDeclaration = classDeclaration.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDeclaration?.Name.ToString() ?? string.Empty;
    }

    private static string[] GetParentClasses(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Ancestors()
            .OfType<ClassDeclarationSyntax>()
            .Select(c => c.Identifier.Text)
            .Reverse()
            .ToArray();
    }
    #endregion Parser

    #region Generator
    public static void EmitGeneratedClassFile(SourceProductionContext ctx, RootNamespaceClassContainer container)
    {
        string fileName = container.RootNamespaceClass.ClassName + ".cs";
        if (container.ParentClasses is not [])
        {
            fileName = string.Join(".", container.ParentClasses) + "." + fileName;
        }
        if (container.Namespace != "")
        {
            fileName = container.Namespace + "." + fileName;
        }

        ctx.AddSource(
            fileName,
            container
                .ToCompilationUnitSyntax()
                .NormalizeWhitespace()
                .ToFullString());
    }

    private CompilationUnitSyntax ToCompilationUnitSyntax()
    {
        var classDeclaration = RootNamespaceClass.ToClassDeclarationSyntax();

        var wrappedClass = WrapInParentClasses(classDeclaration);

        if (string.IsNullOrEmpty(Namespace))
        {
            return CompilationUnit().AddMembers(wrappedClass);
        }

        return CompilationUnit()
            .AddMembers(
                NamespaceDeclaration(ParseName(Namespace))
                    .AddMembers(wrappedClass));
    }

    private MemberDeclarationSyntax WrapInParentClasses(ClassDeclarationSyntax innerClass)
    {
        MemberDeclarationSyntax result = innerClass;

        for (int i = ParentClasses.Length - 1; i >= 0; i--)
        {
            var parentName = ParentClasses[i];
            result = ClassDeclaration(parentName)
                .WithModifiers(TokenList(Token(SyntaxKind.PartialKeyword)))
                .AddMembers(result);
        }

        return result;
    }
    #endregion Generator
}
