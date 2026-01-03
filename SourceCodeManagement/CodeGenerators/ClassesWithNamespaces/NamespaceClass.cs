using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static SourceCodeManagement.Extensions;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal sealed class NamespaceClass
{
    private const string NamespaceSuffix = "Namespace";

    private NamespaceClass()
    {
    }

    public required string ClassName { get; init; }
    public required IReadOnlyList<NamespaceClass> ChildNamespaceClasses { get; set; }
    public NamespaceClass? Parent { get; init; }
    public required bool HasMatchingConstructor { get; init; }
    public required NamespaceAccessibility Accessibility { get; init; }

    private string NamespaceName => Parent is null ? "This" : ClassName[..^NamespaceSuffix.Length];

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

    public static NamespaceClass? ParseSyntax(ClassDeclarationSyntax classDeclaration, NamespaceClass? parent = null)
    {
        if (classDeclaration?.Identifier.Text is null)
        {
            return null;
        }

        var current = new NamespaceClass
        {
            ClassName = classDeclaration.Identifier.Text,
            ChildNamespaceClasses = [],
            Parent = parent,
            HasMatchingConstructor = HasConstructorWithExpectedParameters(classDeclaration, parent?.ClassName),
            Accessibility = new NamespaceAccessibility(classDeclaration, parent),
        };

        current.ChildNamespaceClasses = classDeclaration.Members
            .OfType<ClassDeclarationSyntax>()
            .Where(IsValidSyntax)
            .Select(child => ParseSyntax(child, current))
            .OfType<NamespaceClass>()
            .ToArray();

        return current;
    }

    private static bool IsValidSyntax(ClassDeclarationSyntax classDeclaration)
    {
        string className = classDeclaration.Identifier.Text;
        return className.EndsWith(NamespaceSuffix) && className != NamespaceSuffix;
    }

    private static bool HasConstructorWithExpectedParameters(ClassDeclarationSyntax classDeclaration, string? parentClassName)
    {
        var constructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>();

        return parentClassName switch
        {
            null => constructors.Any(ctor => ctor.ParameterList is { Parameters.Count: 0 }),
            _ => constructors.Any(ctor => ctor.ParameterList?.Parameters is [var parameter] && parameter.Type?.ToString() == parentClassName),
        };
    }

    public ClassDeclarationSyntax ToClassDeclarationSyntax() =>
        ClassDeclaration(ClassName)
            .WithModifiers(Modifiers([
                .. Parent is null ? [] : Accessibility.Effective,
                SyntaxKind.PartialKeyword,
            ]))
            .WithLeadingTrivia(
                Comments([
                    $"// Namespace name: {NamespaceName}",
                    $"// Namespace accessibility: {Accessibility.Effective.ToDisplayString()}",
                    $"// Original class accessibility: {Accessibility.Original.ToDisplayString()}"]))
            .AddMembers([
                .. ParentField(),
                .. Constructor(),
                .. ConstructNamespaceClassInstanceMethod(),
                .. Parents.Select((ancestor, depth) => ancestor.AncestorNamespaceProperty(depth + 1)),
                .. ChildNamespaceClasses.Select(child => child.ChildNamespaceProperty()),
                .. ChildNamespaceClasses.Select(child => child.ToClassDeclarationSyntax()),
            ]);

    private IEnumerable<MemberDeclarationSyntax> ParentField()
    {
        if (Parent is null)
        {
            yield break;
        }

        yield return
            FieldDeclaration(
                VariableDeclaration(IdentifierName(Parent.ClassName))
                    .AddVariables(VariableDeclarator("_parent")))
            .WithModifiers(Modifiers([ SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword ]));
    }

    private IEnumerable<MemberDeclarationSyntax> Constructor()
    {
        if (HasMatchingConstructor)
        {
            yield break;
        }

        var modifiers = Parent is null
            ? Modifiers([SyntaxKind.InternalKeyword])
            : Modifiers([SyntaxKind.PrivateKeyword]);

        yield return ConstructorDeclaration(ClassName)
            .WithModifiers(modifiers)
            .WithParameterList(ParameterList(SeparatedList([
                .. Parent?.ConstructorParameter() ?? []
            ])))
            .WithBody(Block(List([
                .. Parent?.ParentInitializer() ?? [],
                .. ChildNamespaceClasses.Select(x => x.ChildNamespaceInitializer()),
            ])));
    }

    private IEnumerable<MemberDeclarationSyntax> ConstructNamespaceClassInstanceMethod()
    {
        if (Parent is null)
        {
            yield break;
        }

        // Only nested classes need factory method for reflection-based initialization
        yield return MethodDeclaration(IdentifierName(ClassName), Identifier("ConstructNamespaceClassInstance"))
            .WithModifiers(Modifiers([SyntaxKind.PrivateKeyword, SyntaxKind.StaticKeyword]))
            .WithParameterList(ParameterList(SeparatedList([
                Parameter(Identifier("parent"))
                    .WithType(IdentifierName(Parent.ClassName))
            ])))
            .WithBody(Block(
                ReturnStatement(
                    ObjectCreationExpression(IdentifierName(ClassName))
                        .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(IdentifierName("parent"))))))));
    }

    private IEnumerable<ParameterSyntax> ConstructorParameter()
    {
        yield return Parameter(Identifier("parent"))
            .WithType(IdentifierName(ClassName));
    }

    private IEnumerable<ExpressionStatementSyntax> ParentInitializer()
    {
        yield return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName("_parent"),
                IdentifierName("parent")));
    }

    private ExpressionStatementSyntax ChildNamespaceInitializer()
    {
        var methodCall = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        TypeOfExpression(IdentifierName(ClassName)),
                        IdentifierName("GetMethod")),
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    Literal("ConstructNamespaceClassInstance")))))),
                IdentifierName("Invoke")),
            ArgumentList(
                SeparatedList([
                    Argument(LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    Argument(
                        ArrayCreationExpression(
                            ArrayType(PredefinedType(Token(SyntaxKind.ObjectKeyword))),
                            InitializerExpression(
                                SyntaxKind.ArrayInitializerExpression,
                                SingletonSeparatedList<ExpressionSyntax>(ThisExpression())))),
                ])));

        return ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(NamespaceName),
                CastExpression(IdentifierName(ClassName), methodCall)));
    }

    private MemberDeclarationSyntax ChildNamespaceProperty() =>
        PropertyDeclaration(
                IdentifierName(ClassName),
                Identifier(NamespaceName))
            .WithModifiers(Modifiers(Accessibility.Effective))
            .WithAccessorList(
                AccessorList(
                    SingletonList(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));


    private MemberDeclarationSyntax AncestorNamespaceProperty(int depth) =>
        PropertyDeclaration(
                IdentifierName(ClassName),
                Identifier(NamespaceName))
            .WithModifiers(Modifiers([ SyntaxKind.PrivateKeyword ]))
            .WithAccessorList(
                AccessorList(
                    SingletonList(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithExpressionBody(
                                ArrowExpressionClause(
                                    BuildParentAccessExpression(depth)))
                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));

    private ExpressionSyntax BuildParentAccessExpression(int depth)
    {
        ExpressionSyntax expression = IdentifierName("_parent");
        for (int i = 0; i < depth - 1; i++)
        {
            expression = MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                IdentifierName("_parent"));
        }
        return expression;
    }
}
