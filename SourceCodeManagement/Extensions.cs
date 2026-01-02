using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SourceCodeManagement;

internal static partial class Extensions
{
    extension(SyntaxFactory)
    {
        public static FieldDeclarationSyntax Field(
            string typeName,
            string fieldName,
            params SyntaxKind[] modifiers)
        {
            var modifierTokens = modifiers.Select(Token).ToArray();
            return FieldDeclaration(
                    VariableDeclaration(ParseTypeName(typeName))
                        .AddVariables(VariableDeclarator(fieldName)))
                .AddModifiers(modifierTokens);
        }

        public static PropertyDeclarationSyntax Property(
            string typeName,
            string propertyName,
            SyntaxToken accessibilityModifier,
            params SyntaxKind[] accessorKinds)
        {
            var accessors = accessorKinds.Length > 0
                ? accessorKinds.Select(kind =>
                    AccessorDeclaration(kind)
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken)))
                    .ToArray()
                : [
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(
                            Token(SyntaxKind.SemicolonToken)),
                ];

            var property = PropertyDeclaration(
                ParseTypeName(typeName), 
                propertyName);
            
            if (!accessibilityModifier.IsKind(SyntaxKind.None))
            {
                property = property.AddModifiers(accessibilityModifier);
            }

            return property.AddAccessorListAccessors(accessors);
        }

        public static PropertyDeclarationSyntax PropertyWithExpressionBody(
            string typeName,
            string propertyName,
            string expression,
            SyntaxToken accessibilityModifier)
        {
            var property = PropertyDeclaration(
                ParseTypeName(typeName), 
                propertyName);
            
            if (!accessibilityModifier.IsKind(SyntaxKind.None))
            {
                property = property.AddModifiers(accessibilityModifier);
            }

            return property.AddAccessorListAccessors(
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            ParseExpression(expression)))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)));
        }

        public static ConstructorDeclarationSyntax Constructor(
            string className,
            IEnumerable<StatementSyntax> statements,
            SyntaxKind accessibility)
        {
            // TODO: Inline.
            return ConstructorDeclaration(className)
                .AddModifiers(Token(accessibility))
                .WithBody(Block(statements));
        }

        public static StatementSyntax Assignment(string left, string right)
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(left),
                    IdentifierName(right)));
        }

        public static StatementSyntax ObjectCreationAssignment(
            string propertyName,
            string typeName,
            string argumentName)
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(propertyName),
                    ObjectCreationExpression(ParseTypeName(typeName))
                        .AddArgumentListArguments(
                            Argument(IdentifierName(argumentName)))));
        }

        public static ParameterSyntax Parameter(
            string typeName, 
            string parameterName)
        {
            return SyntaxFactory.Parameter(Identifier(parameterName))
                .WithType(ParseTypeName(typeName));
        }
    }

    extension(SyntaxNode @this)
    {
        public bool IsPartialClass =>
            @this is ClassDeclarationSyntax classDeclarationSyntax &&
            classDeclarationSyntax.IsPartial;
    }

    extension(ClassDeclarationSyntax @this)
    {
        public bool IsPartial =>
            @this.Modifiers
                .Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    }

    extension(SyntaxToken @this)
    {
        public bool IsAccessibilityModifier() =>
            @this.IsKind(SyntaxKind.PublicKeyword) ||
            @this.IsKind(SyntaxKind.PrivateKeyword) ||
            @this.IsKind(SyntaxKind.ProtectedKeyword) ||
            @this.IsKind(SyntaxKind.InternalKeyword);
    }

    extension(SyntaxKind @this)
    {
        public bool IsAccessibilityModifier() =>
            @this == SyntaxKind.PublicKeyword ||
            @this == SyntaxKind.PrivateKeyword ||
            @this == SyntaxKind.ProtectedKeyword ||
            @this == SyntaxKind.InternalKeyword;
    }

    extension(IEnumerable<SyntaxToken> @this)
    {
        public SyntaxToken GetAccessibilityModifier() =>
            @this.FirstOrDefault(m => m.IsAccessibilityModifier());
    }

    extension(INamedTypeSymbol @this)
    {
    }

    extension(IMethodSymbol @this)
    {
        public bool HasParameterTypes(params ITypeSymbol[] types)
        {
            return
                @this.Parameters.Length == types.Length &&
                @this.Parameters.SequenceEqual(types, SymbolEqualityComparer.Default);
        }
    }

    extension(ISymbol @this)
    {
        public string FullSymbolPath
        {
            get
            {
                string ns = @this.FullNamespace;
                return
                    (string.IsNullOrWhiteSpace(ns) ? "" : ns + ".") + 
                    @this. SymbolPath;
            }
        }

        public string FullNamespace
        {
            get
            {
                var ns = @this.ContainingNamespace;
                if (ns.IsGlobalNamespace)
                {
                    return "";
                }

                return ns.ToDisplayString();
            }
        }

        public string SymbolPath
        {
            get
            {
                string path = "";
                for (ISymbol s = @this; s is not null; s = s.ContainingSymbol)
                {
                    path = s.Name + "." + path;
                }

                return path;
            }
        }
    }

    extension<T>(T @this)
    {
        public T Do<TNew>(Action<T> action)
        {
            action(@this);
            return @this;
        }

        public TNew Map<TNew>(Func<T, TNew> func) =>
            func(@this);
    }
}
