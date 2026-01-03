using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static SourceCodeManagement.Extensions;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal sealed class NamespaceClass
{
    public required string ClassName { get; init; }
    public required IReadOnlyList<NamespaceClass> ChildNamespaceClasses { get; set; }
    public NamespaceClass? Parent { get; init; }
    public required bool HasMatchingConstructor { get; init; }
    public required NamespaceAccessibility Accessibility { get; init; }

    private NamespaceClass()
    {
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

        var childNamespaceClasses = classDeclaration.Members
            .OfType<ClassDeclarationSyntax>()
            .Where(IsNamespaceClass)
            .Select(child => ParseSyntax(child, current))
            .Where(child => child is not null)
            .Select(child => child!)
            .ToArray();

        current.ChildNamespaceClasses = childNamespaceClasses;

        return current;
    }

    public ClassDeclarationSyntax ToClassDeclarationSyntax()
    {
        SyntaxKind[] modifierKinds = Parent is null
            ? [ SyntaxKind.PartialKeyword ]
            : [ SyntaxKind.PublicKeyword, SyntaxKind.PartialKeyword ];

        var classDeclaration = ClassDeclaration(ClassName)
            .WithModifiers(Modifiers(modifierKinds))
            .WithLeadingTrivia(
                Comments([
                    $"// Namespace name: {GetNamespaceName()}",
                    $"// Namespace accessibility: {Accessibility.Effective.ToDisplayString()}",
                    $"// Original class accessibility: {Accessibility.Original.ToDisplayString()}"]));

        var members = new List<MemberDeclarationSyntax>();

        if (Parent is not null)
        {
            var fieldModifiers = new[] { SyntaxKind.PrivateKeyword, SyntaxKind.ReadOnlyKeyword };
            members.Add(
                FieldDeclaration(
                    VariableDeclaration(IdentifierName(Parent.ClassName))
                        .AddVariables(VariableDeclarator("_parent")))
                .WithModifiers(TokenList(fieldModifiers.Select(Token))));
        }

        if (ChildNamespaceClasses.Count != 0)
        {
            var propertyModifiers = new[] { SyntaxKind.PublicKeyword };
            members.AddRange(
                ChildNamespaceClasses
                    .Select(child =>
                        PropertyDeclaration(
                                IdentifierName(child.ClassName),
                                Identifier(GetNamespacePropertyName(child)))
                            .WithModifiers(TokenList(propertyModifiers.Select(Token)))
                            .WithAccessorList(
                                AccessorList(
                                    SingletonList<AccessorDeclarationSyntax>(
                                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))))));
        }

        if (Parent is not null)
        {
            if (!HasMatchingConstructor)
            {
                var statements = new List<StatementSyntax>
                {
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName("_parent"),
                            IdentifierName("parent"))),
                };
                statements.AddRange(CreateChildAssignments());

                var ctorModifiers = new[] { SyntaxKind.PublicKeyword };
                members.Add(
                    ConstructorDeclaration(ClassName)
                        .WithModifiers(TokenList(ctorModifiers.Select(Token)))
                        .WithParameterList(
                            ParameterList(
                                SingletonSeparatedList(
                                    Parameter(Identifier("parent"))
                                        .WithType(IdentifierName(Parent.ClassName)))))
                        .WithBody(Block(statements)));
            }
        }
        else if (!HasMatchingConstructor)
        {
            var statements = CreateChildAssignments().ToArray();
            members.Add(
                ConstructorDeclaration(ClassName)
                    .WithModifiers(Modifiers([ SyntaxKind.PrivateKeyword ]))
                    .WithBody(Block(statements)));
        }

        if (ChildNamespaceClasses.Count != 0)
        {
            members.AddRange(
                ChildNamespaceClasses
                    .Select(child => child.ToClassDeclarationSyntax()));
        }

        return classDeclaration.AddMembers(members.ToArray());
    }

    private IEnumerable<StatementSyntax> CreateChildAssignments()
    {
        if (ChildNamespaceClasses.Count == 0)
        {
            yield break;
        }

        var argumentList = ArgumentList(
            SingletonSeparatedList(
                Argument(ThisExpression())));

        foreach (var child in ChildNamespaceClasses)
        {
            yield return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(GetNamespacePropertyName(child)),
                    ObjectCreationExpression(IdentifierName(child.ClassName))
                        .WithArgumentList(argumentList)));
        }
    }

    private static string GetNamespacePropertyName(NamespaceClass child)
    {
        const string suffix = "Namespace";
        return child.ClassName.EndsWith(suffix)
            ? child.ClassName.Substring(0, child.ClassName.Length - suffix.Length)
            : child.ClassName;
    }

    private string GetNamespaceName()
    {
        if (Parent is null)
        {
            return "This";
        }

        return GetNamespacePropertyName(this);
    }

    private static bool IsNamespaceClass(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Identifier.Text.EndsWith("Namespace");
    }

    private static bool HasConstructorWithExpectedParameters(ClassDeclarationSyntax classDeclaration, string? parentClassName)
    {
        var constructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>();

        if (parentClassName is null)
        {
            return constructors.Any(ctor => ctor.ParameterList is { Parameters.Count: 0 });
        }

        return constructors.Any(ctor =>
        {
            if (ctor.ParameterList is null || ctor.ParameterList.Parameters.Count != 1)
            {
                return false;
            }

            var parameter = ctor.ParameterList.Parameters[0];
            return parameter.Type?.ToString() == parentClassName;
        });
    }
}
