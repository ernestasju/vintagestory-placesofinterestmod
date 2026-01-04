using Microsoft.CodeAnalysis;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal static class ChildNamespacePropertyAttribute
{
    internal const string AttributeName = "SourceCodeManagement.ChildNamespaceProperty";

    internal static void EmitAttribute(IncrementalGeneratorPostInitializationContext ctx)
    {
        ctx.AddSource(
            "ChildNamespacePropertyAttribute.g.cs",
            """
            namespace SourceCodeManagement
            {
                /// <summary>
                /// Marks a property as a child namespace property that should be initialized by the ChildNamespacesInitializer aspect.
                /// This attribute is applied by the ClassWithNamespaces code generator.
                /// </summary>
                [System.AttributeUsage(System.AttributeTargets.Property)]
                internal sealed class ChildNamespacePropertyAttribute : System.Attribute
                {
                }
            }
            """);
    }
}
