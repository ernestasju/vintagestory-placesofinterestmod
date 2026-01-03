using System.Reflection;
using SourceCodeManagement;

#if USE_TEST_RUNNER == false
var session = new TestSession();
Console.WriteLine(session.Client.Chat.Commands.CopyPlaces(100, "park,nature"));

[ClassWithNamespaces]
partial class TestSession
{
    //internal TestSession()
    //{
    //    Client = (ClientNamespace)typeof(ClientNamespace).GetMethod("ConstructNamespaceClassInstance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { this });
    //}

    public partial class ClientNamespace
    {
        private object CreateClientSide()
        {
            return default!;
        }

        public partial class ChatNamespace
        {
            public partial class CommandsNamespace
            {
                public string CopyPlaces(int radius, string tags)
                {
                    object clientSide = Client.CreateClientSide();
                    return "Handled CopyPlaces command";
                }

                public string PastePlaces(string existingPlaceAction) =>
                    "Handled PastePlaces command";
            }
        }
    }
}
#else
TestRunner.RunTests();

/// <summary>
/// Test runner for ClassWithNamespaces code generator tests
/// </summary>
static class TestRunner
{
    public static void RunTests()
    {
        Console.WriteLine("========== ClassWithNamespaces Code Generator Tests ==========\n");

#if TEST_NO_GENERATION
        Console.WriteLine("TEST: NO_GENERATION");
        NoGenerationTest.Run();
#endif

#if TEST_NO_CHILD_NAMESPACES
        Console.WriteLine("TEST: NO_CHILD_NAMESPACES");
        NoChildNamespacesTest.Run();
#endif

#if TEST_BASIC_HIERARCHY
        Console.WriteLine("TEST: BASIC_HIERARCHY");
        BasicHierarchyTest.Run();
#endif

#if TEST_ALL
        Console.WriteLine("TEST: ALL TESTS");
        NoGenerationTest.Run();
        NoChildNamespacesTest.Run();
        BasicHierarchyTest.Run();
        Console.WriteLine("\n========== All Tests Completed ==========");
#endif

        // Default: show available types and properties
        if (!IsAnyTestDefined())
        {
            Console.WriteLine("No test defined. Available types:\n");
            foreach (Type type in typeof(Program).Assembly.GetTypes())
            {
                if (type.IsCompilerGenerated())
                    continue;

                Console.WriteLine($"{type.FullName}");
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Length > 0)
                {
                    foreach (var prop in properties)
                    {
                        Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
                    }
                }
            }
        }
    }

    private static bool IsAnyTestDefined()
    {
#if TEST_NO_GENERATION || TEST_NO_CHILD_NAMESPACES || TEST_BASIC_HIERARCHY || TEST_ALL
        return true;
#else
        return false;
#endif
    }
}

/// <summary>
/// Test Case 1: Class with [ClassWithNamespaces] but no child namespace classes
/// Expected: Generator should skip (no namespace children to generate)
/// </summary>
static class NoChildNamespacesTest
{
    public static void Run()
    {
        try
        {
            var instance = new Root();
            Console.WriteLine("✓ Root class instantiated successfully");
            
            var properties = typeof(Root).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Console.WriteLine($"✓ Root has {properties.Length} public properties (expected 0)");
            
            if (properties.Length == 0)
            {
                Console.WriteLine("✓ PASS: No properties generated as expected\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: Unexpected properties generated\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }

    [ClassWithNamespaces]
    public partial class Root
    {
    }
}

/// <summary>
/// Test Case 2: Class with namespace children - validates basic hierarchy generation
/// Expected: Generator creates namespace accessor properties and child classes
/// </summary>
static class NoGenerationTest
{
    public static void Run()
    {
        try
        {
            var instance = new Container();
            Console.WriteLine("✓ Container class instantiated successfully");
            
            var rootType = typeof(Container);
            var properties = rootType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Console.WriteLine($"✓ Container has {properties.Length} public properties");
            
            // Check for expected namespace accessor
            var configProperty = properties.FirstOrDefault(p => p.Name == "Config");
            if (configProperty != null)
            {
                Console.WriteLine($"✓ Found Config property: {configProperty.PropertyType.Name}");
                Console.WriteLine("✓ PASS: Expected properties generated\n");
            }
            else
            {
                Console.WriteLine("✗ FAIL: Config property not found\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }

    [ClassWithNamespaces]
    public partial class Container
    {
        public partial class ConfigNamespace
        {
        }
    }
}

/// <summary>
/// Test Case 3: Validates multi-level namespace hierarchy
/// Expected: Generator creates nested namespace classes with proper accessibility
/// </summary>
static class BasicHierarchyTest
{
    public static void Run()
    {
        try
        {
            var instance = new Application();
            Console.WriteLine("✓ Application class instantiated successfully");
            
            var appType = typeof(Application);
            var appProperties = appType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Console.WriteLine($"✓ Application has {appProperties.Length} public properties");
            
            var settingsProperty = appProperties.FirstOrDefault(p => p.Name == "Settings");
            if (settingsProperty != null)
            {
                Console.WriteLine($"✓ Found Settings property");
                
                // Try to instantiate the Settings namespace
                var settingsInstance = settingsProperty.GetValue(instance);
                if (settingsInstance != null)
                {
                    var settingsType = settingsInstance.GetType();
                    var settingsProperties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    Console.WriteLine($"✓ Settings has {settingsProperties.Length} public properties");
                    
                    var databaseProperty = settingsProperties.FirstOrDefault(p => p.Name == "Database");
                    if (databaseProperty != null)
                    {
                        Console.WriteLine($"✓ Found Database property in Settings");
                        Console.WriteLine("✓ PASS: Multi-level hierarchy generated correctly\n");
                    }
                    else
                    {
                        Console.WriteLine("✗ FAIL: Database property not found\n");
                    }
                }
                else
                {
                    Console.WriteLine("✗ FAIL: Settings instance is null\n");
                }
            }
            else
            {
                Console.WriteLine("✗ FAIL: Settings property not found\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }

    [ClassWithNamespaces]
    public partial class Application
    {
        public partial class SettingsNamespace
        {
            public partial class DatabaseNamespace
            {
            }
        }
    }
}
#endif // USE_TEST_RUNNER
