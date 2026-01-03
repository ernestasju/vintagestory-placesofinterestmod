using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal sealed class NamespaceClass
{
    public required string ClassName { get; init; }
    public required IReadOnlyList<NamespaceClass> ChildNamespaceClasses { get; set; }
    public NamespaceClass? Parent { get; init; }
    public required bool HasMatchingConstructor { get; init; }

    private NamespaceClass()
    {
    }

    public static NamespaceClass? ParseSyntax(ClassDeclarationSyntax classDeclaration, NamespaceClass? parent = null)
    {
        if (classDeclaration?.Identifier.Text is null)
        {
            return null;
        }

        var hasMatchingConstructor = HasConstructorWithExpectedParameters(classDeclaration, parent?.ClassName);

        var current = new NamespaceClass
        {
            ClassName = classDeclaration.Identifier.Text,
            ChildNamespaceClasses = System.Array.Empty<NamespaceClass>(),
            Parent = parent,
            HasMatchingConstructor = hasMatchingConstructor,
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
        var classDeclaration = ClassDeclaration(ClassName)
            .WithModifiers(TokenList(Token(SyntaxKind.PartialKeyword)));

        var members = new List<MemberDeclarationSyntax>();

        if (Parent is not null)
        {
            members.Add(
                FieldDeclaration(
                    VariableDeclaration(IdentifierName(Parent.ClassName))
                        .AddVariables(VariableDeclarator("_parent")))
                .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword))));

            if (!HasMatchingConstructor)
            {
                members.Add(
                    ConstructorDeclaration(ClassName)
                        .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                        .WithParameterList(
                            ParameterList(
                                SingletonSeparatedList(
                                    Parameter(Identifier("parent"))
                                        .WithType(IdentifierName(Parent.ClassName)))))
                        .WithBody(
                            Block(
                                ExpressionStatement(
                                    AssignmentExpression(
                                        SyntaxKind.SimpleAssignmentExpression,
                                        IdentifierName("_parent"),
                                        IdentifierName("parent"))))));
            }
        }
        else if (!HasMatchingConstructor)
        {
            members.Add(
                ConstructorDeclaration(ClassName)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                    .WithBody(Block()));
        }

        if (ChildNamespaceClasses.Count != 0)
        {
            members.AddRange(
                ChildNamespaceClasses
                    .Select(child => child.ToClassDeclarationSyntax()));
        }

        return classDeclaration.AddMembers(members.ToArray());
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
