using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace SourceCodeManagement.CodeGenerators.ClassNamespaces;

[Generator(LanguageNames.CSharp)]
public partial class ClassNamespaceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource(
                "ClassWithNamespacesAttribute.g.cs",
                """
                namespace SourceCodeManagement;

                [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                public sealed class ClassWithNamespacesAttribute : global::System.Attribute
                {
                }
                """));

        IncrementalValuesProvider<NamespaceClass> classesWithNamespaces = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                "SourceCodeManagement.ClassWithNamespacesAttribute",
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: NamespaceClass.ParseSyntax)
            .Where(static x => x is not null)!;

        context.RegisterSourceOutput(classesWithNamespaces, NamespaceClass.AddSource);
    }
}
