#if USE_TEST_RUNNER
using System;
using System.Linq;
using System.Reflection;

namespace SourceCodeManagement.Tests
{
    internal static class SnapshotPrinter
    {
        internal static void PrintAllTypesAndProperties()
        {
            var asm = Assembly.GetExecutingAssembly();
            Console.WriteLine("===== TYPE MAP =====");
            foreach (var type in asm.GetTypes().Where(x => string.IsNullOrWhiteSpace(x.Namespace) || x.Namespace == "SourceCodeManagement.TestSamples").OrderBy(t => t.FullName))
            {
                // Skip compiler generated backing types
                if (type.FullName == null) continue;
                Console.WriteLine(type.FullName + " " + type.Namespace);

                var props = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                 .OrderBy(p => p.Name)
                                 .ToArray();
                foreach (var p in props)
                {
                    var get = p.GetGetMethod(true);
                    var set = p.GetSetMethod(true);

                    string Accessibility(MethodInfo? m)
                    {
                        if (m == null) return "-";
                        if (m.IsPublic) return "public";
                        if (m.IsFamily) return "protected";
                        if (m.IsAssembly && !m.IsFamilyOrAssembly) return "internal";
                        if (m.IsFamilyOrAssembly) return "protected internal";
                        if (m.IsPrivate) return "private";
                        return "unknown";
                    }

                    string access = $"get:{Accessibility(get)} set:{Accessibility(set)}";
                    Console.WriteLine($"  {p.PropertyType.FullName} {p.Name} [{access}]");
                }
            }
            Console.WriteLine("===== END TYPE MAP =====");
        }
    }
}
#endif
