# ClassWithNamespaces Analyzer Design

## Overview

The `ClassWithNamespaces` analyzer is a source generator that automatically generates partial class declarations and boilerplate code for classes marked with the `[ClassWithNamespaces]` attribute.

The generator creates a navigable namespace hierarchy, allowing you to access parent namespaces and sibling namespace classes through auto-generated properties.

## Usage

### Basic Usage

Mark any class with `[ClassWithNamespaces]` attribute:

```csharp
using SourceCodeManagement;

[ClassWithNamespaces]
public partial class MySession
{
    public partial class DatabaseNamespace
    {
        // ...
    }
    
    public partial class CacheNamespace
    {
        // ...
    }
}
```

The generator will:
1. Create a partial class declaration in a generated file
2. Generate properties to navigate the namespace hierarchy
3. Generate constructors to initialize the namespace hierarchy

### What Gets Generated

For each class marked with `[ClassWithNamespaces]`:
- **Partial class declaration** with appropriate modifiers
- **Properties** for each child namespace class
- **Constructor** that initializes the namespace hierarchy
- **Properties** to navigate back to parent namespaces via `This`, `ThisParent`, etc.

## Generated File Names

Generated files follow the naming convention:
```
<FullNamespace>.<OutermostClass>.<ParentClasses>.<ClassName>.g.cs
```

### Examples

```csharp
// Root class in namespace
[ClassWithNamespaces]
public partial class TestSession  // in PlacesOfInterestMod.IntegrationTests
// Generated file: PlacesOfInterestMod.IntegrationTests.TestSession.g.cs

// Nested class in static class
[ClassWithNamespaces]
protected partial class Demo1  // nested in ClassesWithNamespacesTestCases (static)
// Generated file: PlacesOfInterestMod.IntegrationTests.ClassesWithNamespacesTestCases.Demo1.g.cs

// Multiple levels of nesting (unlikely but possible)
[ClassWithNamespaces]
internal partial class OuterClass
{
    [ClassWithNamespaces]
    public partial class InnerClass
    // Generated file: MyNamespace.OuterClass.InnerClass.g.cs
}
```

## Accessibility Modifiers

The generator preserves and intelligently handles accessibility modifiers:

### Root Classes (no parent in hierarchy)

- **Explicit modifier**: Uses the declared modifier
  ```csharp
  [ClassWithNamespaces]
  protected partial class Demo1  // → protected partial
  ```

- **No explicit modifier**: Infers from C# nesting rules
  - Nested in type → `private`
  - Top-level → `internal`

### Child Namespace Classes

- **Explicit modifier**: Uses the declared modifier
  ```csharp
  internal partial class ZNamespace  // → internal partial
  ```

- **No explicit modifier**: Inherits from parent class's accessibility
  ```csharp
  [ClassWithNamespaces]
  protected partial class Demo1
  {
      partial class XNamespace  // No explicit modifier
      // → Inherits 'protected' from Demo1
      // → Generated as: protected partial
  }
  ```

## Generated Code Structure

### Class Modifiers

Generated classes always include:
1. **Accessibility modifier** (from rules above)
2. **`partial` keyword** (always added)
3. **Other modifiers** from the original class are NOT included in generated code

```csharp
// Original user code
[ClassWithNamespaces]
protected sealed partial class Demo1 { }

// Generated code
protected partial class Demo1 { }
// Note: 'sealed' is NOT in generated code - user keeps it in their file
```

### Generated Members

For each namespace class child:
```csharp
public ClassName PropertyName { get; }
```

For each parent namespace:
```csharp
public ParentClassName This { get => _parent; }
public GrandparentClassName ThisParent { get => _parent._parent; }
```

## Edge Cases & Limitations

### Edge Case 1: No Namespace Children

```csharp
[ClassWithNamespaces]
public partial class EmptySession
{
    // No nested classes ending with "Namespace"
}
```

**Result**: Generator returns `null` (no file generated)  
**Reason**: Class has no namespace children, so no boilerplate needed

### Edge Case 2: Non-Partial Child Classes

```csharp
[ClassWithNamespaces]
public partial class Demo1
{
    class NonPartialNamespace { }  // NOT partial
    partial class PartialNamespace { }  // IS partial
}
```

**Result**: Only `PartialNamespace` is processed  
**Reason**: Only `partial` classes are considered namespace classes

### Edge Case 3: User Forgets `partial` Keyword

```csharp
[ClassWithNamespaces]
public class Demo1  // Missing 'partial'
{
    partial class XNamespace { }
}
```

**Result**: Generator creates file with `partial` keyword  
**Error**: Compiler error - duplicate class declaration without `partial` in user code  
**User Action**: User adds `partial` to fix

### Edge Case 4: User-Defined Constructor

```csharp
[ClassWithNamespaces]
public partial class Demo1
{
    partial class XNamespace { }
    
    public Demo1()  // User-defined constructor
    {
        // Custom initialization
    }
}
```

**Result**: Generator does NOT create a constructor  
**Reason**: User has already defined one; generator respects this

### Edge Case 5: Sealed Root Class

```csharp
[ClassWithNamespaces]
public sealed partial class Demo1
{
    partial class XNamespace { }
}
```

**Result**: 
- Generated class: `public partial` (sealed is NOT added)
- Child class: `public partial` (sealed is NOT inherited)  
**Reason**: `sealed` modifier is not part of the namespace hierarchy contract

## Modifier Inheritance Rules

```
Root Class (no parent)
├─ Has explicit accessibility → Use it
└─ No accessibility → Infer from C# nesting (private if nested, internal if top-level)

Child Class (has parent)
├─ Has explicit accessibility → Use it
└─ No accessibility → Inherit from parent's generated accessibility
```

## When Generator Runs

The generator runs during compilation when:
1. A class is marked with `[ClassWithNamespaces]` attribute
2. The class has at least one nested class ending with "Namespace"
3. At least one of those classes has the `partial` keyword

## File Output Location

Generated files are placed in the standard C# generator output directory:
- Debug: `obj/Debug/<TargetFramework>/generated/SourceCodeManagement/`
- Release: `obj/Release/<TargetFramework>/generated/SourceCodeManagement/`

They are automatically included in compilation.

## Troubleshooting

### "ClassWithNamespacesAttribute not found"
- Ensure the `SourceCodeManagement` analyzer DLL is referenced in your project
- Ensure the `using SourceCodeManagement;` directive is present

### "Class does not contain definition for property X"
- Ensure the namespace class has the `partial` keyword
- Ensure the namespace class name ends with "Namespace"
- Ensure `[ClassWithNamespaces]` is on a parent class or the class itself

### Generated file not appearing
- Check that the class has at least one nested `partial class` ending with "Namespace"
- Check that the root class is marked with `[ClassWithNamespaces]`
- Rebuild the project (sometimes needed after adding the attribute)

## Relevant Source Files

### Generator Implementation

- **`SourceCodeManagement/CodeGenerators/ClassNamespaces/ClassNamespaceGenerator.cs`**
  - Entry point for the source generator
  - Handles attribute detection and source output registration
  - Registers the `ClassWithNamespacesAttribute` post-initialization

- **`SourceCodeManagement/CodeGenerators/ClassNamespaces/NamespaceClass.cs`**
  - Core model representing a namespace class structure
  - Contains `ParseSyntax()` - parses marked classes into the model
  - Contains `ToCompilationUnitSyntax()` - generates the C# code
  - Handles modifier calculation and file naming

- **`SourceCodeManagement/Extensions.cs`**
  - Extension methods for symbol operations
  - `CalculateFullNamespaceName()` - gets the full namespace
  - `ReduceWithParents<T>()` - walks parent chains for path building

### Test & Example

- **`PlacesOfInterestMod.IntegrationTests/ClassesWithNamespacesTestCases.cs`**
  - Integration tests demonstrating the analyzer
  - Shows the expected usage patterns
  - Contains test cases: `Demo0`, `Demo1`, `Demo2`
