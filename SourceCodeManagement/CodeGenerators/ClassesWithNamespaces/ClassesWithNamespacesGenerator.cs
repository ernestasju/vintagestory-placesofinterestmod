using Microsoft.CodeAnalysis;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

[Generator(LanguageNames.CSharp)]
internal sealed class ClassesWithNamespacesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(MarkerAttribute.EmitAttribute);

        context.RegisterSourceOutput(
            RootNamespaceClassContainer.FindClassesWithMarkerAttribute(context),
            RootNamespaceClassContainer.EmitGeneratedClassFile);
    }
}
