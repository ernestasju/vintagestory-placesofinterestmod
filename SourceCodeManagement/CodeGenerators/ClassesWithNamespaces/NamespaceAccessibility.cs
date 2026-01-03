using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal sealed class NamespaceAccessibility
{
    public ImmutableArray<SyntaxKind> Original { get; }
    public ImmutableArray<SyntaxKind> Effective { get; }

    public NamespaceAccessibility(ClassDeclarationSyntax classDeclaration, NamespaceClass? parent)
    {
        Original = classDeclaration.AccessibilityModifiers.ToImmutableArray();

        Effective = 0 switch
        {
            _ when Original is not [] => Original,
            _ when parent is not null => parent.Accessibility.Effective,
            _ => [SyntaxKind.PrivateKeyword],
        };
    }
}
