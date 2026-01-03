using Microsoft.CodeAnalysis;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal static class MarkerAttribute
{
    internal const string Name = "SourceCodeManagement.ClassWithNamespacesAttribute";

    internal static void EmitAttribute(IncrementalGeneratorPostInitializationContext ctx)
    {
        // NOTE: Attribute to mark classes that will have namespaces boilerplate generation.
        // NOTE: Without it, we cannot determine which classes to target without getting false positives.
        ctx.AddSource(
            "ClassWithNamespacesAttribute.g.cs",
            """
            namespace SourceCodeManagement
            {
                [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false)]
                internal sealed class ClassWithNamespacesAttribute : global::System.Attribute
                {
                }
            }
            """);
    }
}
