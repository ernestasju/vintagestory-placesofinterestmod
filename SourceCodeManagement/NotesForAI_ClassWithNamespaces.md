# ClassWithNamespaces Analyzer Design

## Overview

The `ClassWithNamespaces` analyzer is a source generator that automatically generates partial class declarations and boilerplate code for classes marked with the `[ClassWithNamespaces]` attribute.

**Intention:** This analyzer enables organizing non-static data within a class using a namespace-like hierarchy. Instead of having all data at the root class level, you can create nested "namespace classes" that logically group related data and functionality, while maintaining navigable parent-child relationships.

The generator creates a navigable namespace hierarchy, allowing you to access parent namespaces and sibling namespace classes through auto-generated properties.

## Generation Rules and Principles

### Rule 1: Trigger and Generated Structure
The generation is triggered by any class with `[ClassWithNamespaces]` attribute. This is the root class / "This" namespace class.

The generator produces a matching partial class structure, including:
- Containing namespace (if not global)
- Containing partial classes (if any exist)
- The "This" namespace class itself

**Will match code:**

```csharp
using SourceCodeManagement; // required to use ClassWithNamespaces attribute

namespace ContainerNamespace; // optional

... class ContainerClass // zero or more container classes
{
   [ClassWithNamespaces]
   ... class SomeClassWithNamespaces
   {
        // ...
   }
}
```

**Will generate code:**

```csharp
namespace ContainerNamespace; // optional

partial class ContainerClass // zero or more container classes
{
   partial class SomeClassWithNamespaces
   {
        // ...
   }
}
```

**Key Points:**
- The attribute requires `using SourceCodeManagement;` directive (or use fully qualified `[SourceCodeManagement.ClassWithNamespaces]`)
- The class can be at any nesting level (top-level or nested in zero or more container classes)
- The class can be in any namespace (including global namespace)
- The class must be `partial` (generator will create matching partial declaration)
- `...` represents any modifiers (accessibility, static, sealed, abstract, etc.)
- Generated code does NOT include the using directive (not needed)

### Rule 2: Generated File Naming
We name generated file `<FullNamespace>.<FullClassPath>.g.cs` where:
- `<FullNamespace>` = Complete namespace path (empty if global namespace)
- `<FullClassPath>` = Path to the class with `[ClassWithNamespaces]` attribute within the class hierarchy, separated by periods. Containing classes appear first.

**Examples:**
- `Demo0` in namespace `PlacesOfInterestMod.IntegrationTests`:
  - `PlacesOfInterestMod.IntegrationTests.Demo0.g.cs`
- `Demo1` nested in `ClassesWithNamespacesTestCases` in namespace `PlacesOfInterestMod.IntegrationTests`:
  - `PlacesOfInterestMod.IntegrationTests.ClassesWithNamespacesTestCases.Demo1.g.cs`
- `InnerClass` nested in `OuterClass` nested in `Container` in namespace `MyApp`:
  - `MyApp.Container.OuterClass.InnerClass.g.cs`
- `MyClass` in global namespace:
  - `MyClass.g.cs`

### Rule 3: Effective Accessibility
Every namespace class will have *effective* accessibility (private, protected, internal, public).

**Determining Effective Accessibility:**
- **Root "This" namespace class (has `[ClassWithNamespaces]` attribute):**
  - If has explicit accessibility modifier → use it
  - If no explicit accessibility modifier → `private`
- **Child namespace classes:**
  - If has explicit accessibility modifier → use it
  - If no explicit accessibility modifier → inherit parent's *effective* accessibility

**Important:** 
- Root "This" namespace class does NOT inherit accessibility from its containing class. It only uses its own explicit modifier or defaults to `private`.

**Case 1: This namespace without modifier → private, child inherits private**

Will match code:
```csharp
[ClassWithNamespaces]
partial class RootClass // no accessibility modifier
{
    partial class ChildNamespace { } // no accessibility modifier
}
```

Will generate code:
```csharp
partial class RootClass // only partial modifier
{
    private partial class ChildNamespace // accessibility modifier is inherited from RootClass (private)
    {
    }
}
```

**Case 2: Child with explicit modifier overrides inherited**

Will match code:
```csharp
[ClassWithNamespaces]
partial class RootClass // no accessibility modifier
{
    internal partial class ChildNamespace { } // internal modifier
}
```

Will generate code:
```csharp
partial class RootClass // only partial modifier
{
    internal partial class ChildNamespace // internal modifier overrides inherited private
    {
    }
}
```

**Case 3: This namespace with explicit modifier, child inherits**

Will match code:
```csharp
... partial class ParentNamespace // suppose parent has protected effective accessibility - can be explit or calculated
{
    partial class ChildNamespace { } // no accessibility modifier
}
```

Will generate code:
```csharp
protected partial class ParentNamespace // calculated effective accessibility
{
    protected partial class ChildNamespace // child namespace inherits accessibility modifier from parent namespace
    {
    }
}
```

### Rule 4: "This" Namespace Class Modifiers
We do not add any modifiers other than `partial` to generated "This" namespace class.

**Will match code:**
```csharp
[ClassWithNamespaces]
protected sealed partial class RootClass
{
}
```

**Will generate code:**
```csharp
partial class RootClass // only 'partial', no 'protected' or 'sealed'
{
}
```

### Rule 5: Namespace Subclasses
Every namespace class can have zero or more namespace subclasses named `<NamespaceName>Namespace`. Only direct subclasses are considered for each namespace class. Only `partial` classes are considered as namespace subclasses.

**Will match code:**
```csharp
partial class SomeNamespace
{
    partial class FirstNamespace { } // direct child, ends with "Namespace" → included
    
    partial class NotANamespace // does not end with "Namespace" → excluded
    {
        partial class SecondNamespace { } // not direct child of namespace class → excluded
    }
    
    class ThirdNamespace { } // direct child, ends with "Namespace", not partial → included (compiler error)
}
```

**Will generate code:**
```csharp
partial class SomeNamespace
{
    // FirstNamespace and ThirdNamespace are generated (direct children ending with "Namespace")
    // ThirdNamespace will cause compiler error due to missing 'partial' in user code
    
    ... partial class FirstNamespace
    {
    }
    
    ... partial class ThirdNamespace
    {
    }
}
```

### Rule 6: Parent Reference Field
Each namespace class other than "This" will have field `private readonly ParentNamespaceClass _parent;`.

**Will match code:**
```csharp
[ClassWithNamespaces]
partial class RootClass
{
    partial class ChildNamespace
    {
        partial class GrandchildNamespace { }
    }
}
```

**Will generate code:**
```csharp
partial class RootClass
{
    // no _parent field (this is "This" namespace class)
    
    ... partial class ChildNamespace
    {
        private readonly RootClass _parent; // parent reference field
        
        ... partial class GrandchildNamespace
        {
            private readonly ChildNamespace _parent; // parent reference field
        }
    }
}
```

### Rule 7: Child Namespace Properties
Each namespace class will have autoproperties for its directly contained non-static namespace classes that will have only getter. The property will not have setter and it will be assigned in the constructor. The property will have accessibility modifier equal to that child namespace class's *effective* accessibility (matching the property type, so visibility is consistent).

**Will match code:**
```csharp
partial class ParentNamespace
{
    partial class ChildNamespace { }
    internal partial class InternalChildNamespace { }
}
```

**Will generate code:**
```csharp
... partial class ParentNamespace // has some effective accessibility
{
    internal InternalChildNamespace InternalChild { get; } // this is internal because InternalChildNamespace is internal
        
    internal partial class InternalChildNamespace
    {
    }
}
```

### Rule 8: Constructors
Each namespace class will have a constructor that is used to assign `_parent` from constructor parameter and/or assign child namespace properties. This generated constructor will always have accessibility modifier equal to that namespace class *effective* accessibility.

**Will match code:**
```csharp
partial class ChildNamespace { }
```

**Will generate code:**
```csharp
<accessibility-modifier-2> partial class ChildNamespace // from original class or calculated from class namespace hierearchy
{
    <accessibility-modifier-2> ChildNamespace(ParentNamespace parent) // the constructor has the same accessibility as the class
    {
        _parent = parent; // keep reference to parent
    }
}
```

### Rule 9: "This" Namespace Class Constructor
"This" namespace class constructor will be generated without any parameters because it will not have parent namespace. Each child namespace property will always be initialized using parent namespace class instance (e.g., `new SomeNamespace(this)`).

**Exception:** If user has already defined any constructor, the generator does NOT create a constructor.

**Will match code:**
```csharp
[ClassWithNamespaces]
partial class RootClass
{
    partial class ChildNamespace { }
}
```

**Will generate code:**
```csharp
partial class RootClass // "This" namespace class
{
    ... RootClass() // no parameters, accessibility = RootClass effective accessibility
    {
        Child = new ChildNamespace(this); // initialize child namespace properties
    }
    
    ... partial class ChildNamespace
    {
        ... ChildNamespace(RootClass parent) // has parent parameter
        {
            _parent = parent;
        }
    }
}
```

### Rule 10: Ancestor Navigation Properties
Every namespace class other than "This" namespace class will have references to all its ancestor namespace classes. Each property will have accessibility modifier equal to the **ancestor namespace class's** *effective* accessibility (the type the property points to), not the containing class's accessibility. Each property will use one or more chained `_parent` fields (e.g., `_parent._parent` to go two namespace classes up). Create ancestor properties only for non-static namespace classes.

**Namespace Naming:**
Each namespace class has a namespace name:
- Root "This" class → namespace name is `This`
- Child class `XNamespace` → namespace name is `X` (remove "Namespace" suffix)
- Child class `DatabaseNamespace` → namespace name is `Database` (remove "Namespace" suffix)

**Property Naming:**
Ancestor properties use the namespace name of the target ancestor:
- Property pointing to immediate parent → use parent's namespace name
- Property pointing to grandparent → use grandparent's namespace name
- And so on...

**Will match code:**
```csharp
[ClassWithNamespaces]
protected partial class RootClass
{
    protected partial class FirstNamespace
    {
        internal partial class SecondNamespace { }
    }
}
```

**Will generate code:**
```csharp
partial class RootClass
{
    protected partial class FirstNamespace
    {
        internal partial class SecondNamespace
        {
            private readonly FirstNamespace _parent;
            
            protected FirstNamespace First { get => _parent; } // accessibility = FirstNamespace effective (protected), NOT SecondNamespace (internal)
            protected RootClass This { get => _parent._parent; } // accessibility = RootClass effective (protected), NOT SecondNamespace (internal)
        }
    }
}
```

**Key point:** Ancestor properties match the type they point to, ensuring consistent visibility regardless of the containing class's accessibility.

## Generation Conditions

The generator runs during compilation when a class is marked with `[ClassWithNamespaces]` attribute.

**Optional conditions** (compiler will show errors if not met):
- Having at least one nested class ending with "Namespace"
- Having `partial` keyword on those nested classes

If no namespace children exist, the generator returns `null` (no file generated).

## File Output Location

Generated files are placed in the standard C# generator output directory:
- Debug: `obj/Debug/<TargetFramework>/generated/SourceCodeManagement/`
- Release: `obj/Release/<TargetFramework>/generated/SourceCodeManagement/`

They are automatically included in compilation.

## Example: Complete Generation

Given this user code:

```csharp
using SourceCodeManagement;

namespace PlacesOfInterestMod.IntegrationTests;

internal static class Container
{
    [ClassWithNamespaces]
    protected partial class Demo1
    {
        partial class XNamespace
        {
            sealed partial class YNamespace { }
            internal partial class ZNamespace { }
        }
    }
}
```

The generator produces `PlacesOfInterestMod.IntegrationTests.Container.Demo1.g.cs`:

```csharp
// <auto-generated/>
namespace PlacesOfInterestMod.IntegrationTests;

internal static partial class Container
{
    protected partial class Demo1
    {
        protected XNamespace X { get => _parent; }

        protected Demo1()
        {
            X = new XNamespace(this);
        }

        protected partial class XNamespace
        {
            private readonly Demo1 _parent;

            protected Demo1 This { get => _parent; }

            protected YNamespace Y { get; }

            internal ZNamespace Z { get; }

            protected XNamespace(Demo1 parent)
            {
                _parent = parent;
                Y = new YNamespace(this);
                Z = new ZNamespace(this);
            }

            protected partial class YNamespace
            {
                private readonly XNamespace _parent;

                protected XNamespace X { get => _parent; }

                protected Demo1 This { get => _parent._parent; }

                protected YNamespace(XNamespace parent)
                {
                    _parent = parent;
                }
            }

            internal partial class ZNamespace
            {
                private readonly XNamespace _parent;

                internal XNamespace X { get => _parent; }

                internal Demo1 This { get => _parent._parent; }

                internal ZNamespace(XNamespace parent)
                {
                    _parent = parent;
                }
            }
        }
    }
}
```

**Key observations:**
- User code includes `using SourceCodeManagement;` to use the attribute
- Generated code does NOT include the using directive (not needed in generated file)
- `Demo1` has explicit `protected` → effective accessibility is `protected`
- `Demo1` generated class has only `partial` (no accessibility in generated code per Rule 7)
- `XNamespace` has no explicit modifier → inherits `protected` from `Demo1`
- `YNamespace` has no explicit modifier → inherits `protected` from `XNamespace` (not `sealed` since `sealed` is not accessibility)
- `ZNamespace` has explicit `internal` → effective accessibility is `internal`
- All properties and constructors match their namespace class's effective accessibility
- Ancestor properties use namespace names: `Demo1 This` (root), `XNamespace X` (parent XNamespace)
