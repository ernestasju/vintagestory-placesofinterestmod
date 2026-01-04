using Microsoft.CodeAnalysis;

namespace SourceCodeManagement.CodeGenerators.ClassesWithNamespaces;

internal static class ChildNamespacesInitializerAttribute
{
    internal const string AttributeName = "SourceCodeManagement.ChildNamespacesInitializer";

    internal static void EmitAttribute(IncrementalGeneratorPostInitializationContext ctx)
    {
        ctx.AddSource(
            "ChildNamespacesInitializerAttribute.g.cs",
            """
            using System;
            using System.Reflection;
            using PostSharp.Aspects;
            using PostSharp.Serialization;

            namespace SourceCodeManagement
            {
                [PSerializable]
                internal sealed class ChildNamespacesInitializerAttribute : OnMethodBoundaryAspect
                {
                    public override bool CompileTimeValidate(MethodBase method)
                    {
                        return method.IsConstructor;
                    }
                    
                    public override void OnExit(MethodExecutionArgs args)
                    {
                        var instance = args.Instance;
                        Type type = instance.GetType();

                        foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (prop.GetCustomAttribute(typeof(ChildNamespacePropertyAttribute)) != null && prop.GetValue(instance) == null)
                            {
                                var ctor = prop.PropertyType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                    .First(x =>
                                    {
                                        var ps = x.GetParameters();
                                        return ps.Length == 1 && ps[0].ParameterType == type;
                                    });

                                prop.SetValue(instance, ctor.Invoke(new object[] { instance }));
                            }
                        }
                    }
                }
            }
            """);
    }
}
