using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal sealed class NamespaceAccessibility
{
    public SyntaxKind[] Original { get; }
    public SyntaxKind[] Effective { get; }

    public NamespaceAccessibility(ClassDeclarationSyntax classDeclaration, NamespaceClass? parent)
    {
        Original = classDeclaration.AccessibilityModifiers.ToArray();

        if (Original.Length > 0)
        {
            Effective = Original;
            return;
        }

        if (parent is not null)
        {
            Effective = parent.Accessibility.Effective;
            return;
        }

        Effective = [ SyntaxKind.PrivateKeyword ];
    }
}
