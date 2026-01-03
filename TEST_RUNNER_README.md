# ClassWithNamespaces Code Generator Test Runner

This directory contains a test runner for the `ClassWithNamespaces` code generator.

## Test Cases

The test runner automatically detects and runs all test cases defined in `SourceCodeManagement.Tests\Program.cs`:

### Available Test Cases

- **TEST_NO_CHILD_NAMESPACES** - Tests a class with `[ClassWithNamespaces]` but no child namespace classes
- **TEST_NO_GENERATION** - Tests basic hierarchy generation with one level of namespace children
- **TEST_BASIC_HIERARCHY** - Tests multi-level namespace hierarchy with proper accessibility

Each test case validates:
- Class instantiation
- Generated properties via reflection
- Expected namespace accessor properties
- Proper nesting in multi-level hierarchies

## Running Tests

Simply run the PowerShell script from the repository root:

```powershell
.\run-tests.ps1
```

The script will:
1. Automatically detect all test cases from `Program.cs`
2. Build the `SourceCodeManagement.Tests` project with all test constants enabled
3. Run the executable, which will execute all tests
4. Display results to console with ✓/✗ indicators

## Test Output

Example output:
```
========== ClassWithNamespaces Code Generator Test Runner ==========

Detecting test cases from Program.cs...
Found test cases: TEST_NO_CHILD_NAMESPACES, TEST_NO_GENERATION, TEST_BASIC_HIERARCHY

Building project...
Define Constants: TEST_NO_CHILD_NAMESPACES;TEST_NO_GENERATION;TEST_BASIC_HIERARCHY

Running all tests...

========== ClassWithNamespaces Code Generator Tests ==========

TEST: NO_CHILD_NAMESPACES
✓ Root class instantiated successfully
✓ Root has 0 public properties (expected 0)
✓ PASS: No properties generated as expected

TEST: NO_GENERATION
✓ Container class instantiated successfully
✓ Container has 1 public properties
✓ Found Config property: ConfigNamespace
✓ PASS: Expected properties generated

TEST: BASIC_HIERARCHY
✓ Application class instantiated successfully
✓ Application has 1 public properties
✓ Found Settings property
✓ Settings has 1 public properties
✓ Found Database property in Settings
✓ PASS: Multi-level hierarchy generated correctly

========== Test Run Complete ==========
```

## Project Setup

The `SourceCodeManagement.Tests.csproj` is configured to:
1. Include the `SourceCodeManagement` source generator
2. Support multiple preprocessor directives via `DefineConstants`
3. Output executable to standard location

## Adding New Tests

To add a new test case:

1. Add a test class in `Program.cs`:
```csharp
static class MyNewTest
{
    public static void Run()
    {
        try
        {
            var instance = new TestClass();
            Console.WriteLine("✓ TestClass instantiated successfully");
            
            var properties = typeof(TestClass).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Console.WriteLine($"✓ TestClass has {properties.Length} public properties");
            
            // Validate expected properties
            Console.WriteLine("✓ PASS: Test passed\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ FAIL: {ex.Message}\n");
        }
    }

    [ClassWithNamespaces]
    public partial class TestClass
    {
        // Test class definition
    }
}
```

2. Add a preprocessor condition in `TestRunner.RunTests()`:
```csharp
#if TEST_MY_NEW_TEST
    Console.WriteLine("TEST: MY_NEW_TEST");
    MyNewTest.Run();
#endif
```

3. Update the `IsAnyTestDefined()` method to include your new constant:
```csharp
private static bool IsAnyTestDefined()
{
#if TEST_NO_GENERATION || TEST_NO_CHILD_NAMESPACES || TEST_BASIC_HIERARCHY || TEST_MY_NEW_TEST || TEST_ALL
    return true;
#else
    return false;
#endif
}
```

4. Run the tests - the script will automatically detect and run your new test case

## Troubleshooting

### Build Failed
- Check that `SourceCodeManagement.Tests` project builds correctly
- Verify `SourceCodeManagement` source generator is properly referenced
- Check for syntax errors in `Program.cs`

### No Output
- Ensure the executable runs successfully
- Check console output window for error messages
- Verify test cases are properly defined with `#if TEST_*` directives

### Missing Generated Code
- Ensure test classes are marked with `[ClassWithNamespaces]` attribute
- Verify child namespace classes end with "Namespace" suffix
- Check that child classes are marked as `partial`
