using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal sealed class NamespaceClass
{
    public required string ClassName { get; init; }
    public required IReadOnlyList<NamespaceClass> ChildNamespaceClasses { get; init; }

    private NamespaceClass()
    {
    }

    public static NamespaceClass? ParseSyntax(ClassDeclarationSyntax classDeclaration)
    {
        if (classDeclaration is null || classDeclaration.Identifier.Text is null)
        {
            return null;
        }

        var childNamespaceClasses = classDeclaration.Members
            .OfType<ClassDeclarationSyntax>()
            .Where(IsNamespaceClass)
            .Select(ParseSyntax)
            .Where(child => child is not null)
            .Select(child => child!)
            .ToArray();

        return new()
        {
            ClassName = classDeclaration.Identifier.Text,
            ChildNamespaceClasses = childNamespaceClasses,
        };
    }

    public ClassDeclarationSyntax ToClassDeclarationSyntax()
    {
        var classDeclaration = ClassDeclaration(ClassName)
            .WithModifiers(TokenList(Token(SyntaxKind.PartialKeyword)));

        if (ChildNamespaceClasses.Count != 0)
        {
            classDeclaration = classDeclaration.AddMembers(
                ChildNamespaceClasses
                    .Select(child => child.ToClassDeclarationSyntax())
                    .ToArray());
        }

        return classDeclaration;
    }

    private static bool IsNamespaceClass(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.Identifier.Text.EndsWith("Namespace");
    }
}
