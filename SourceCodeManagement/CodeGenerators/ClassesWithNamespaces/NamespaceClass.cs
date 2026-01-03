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
            ChildNamespaceClasses = System.Array.Empty<NamespaceClass>(),
            Parent = parent,
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
        else
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
}
