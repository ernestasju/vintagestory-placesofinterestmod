using SourceCodeManagement;

namespace PlacesOfInterestMod.IntegrationTests;

static partial class ClassesWithNamespacesAccessibilityTests
{
    /// <summary>
    /// Test Case 1: Private root with private child (most restrictive)
    /// Private root class can have private namespace classes
    /// Child property should be private
    /// </summary>
    [ClassWithNamespaces]
    private partial class PrivateRootPrivateChild
    {
        partial class ChildNamespace { }
    }

    /// <summary>
    /// Test Case 2: Private root with internal child (should fail - can't expose internal from private)
    /// This violates C# accessibility rules: internal member cannot be accessed from private class
    /// </summary>
    [ClassWithNamespaces]
    private partial class PrivateRootInternalChild
    {
        internal partial class ChildNamespace { }
    }

    /// <summary>
    /// Test Case 3: Protected root with protected child
    /// Protected root class (in static partial class) - edge case
    /// Protected child inherits from parent
    /// </summary>
    [ClassWithNamespaces]
    protected partial class ProtectedRootProtectedChild
    {
        partial class ChildNamespace { }
    }

    /// <summary>
    /// Test Case 4: Protected root with internal child
    /// Internal child should work - internal is more restrictive than protected in some contexts
    /// </summary>
    [ClassWithNamespaces]
    protected partial class ProtectedRootInternalChild
    {
        internal partial class ChildNamespace { }
    }

    /// <summary>
    /// Test Case 5: Internal root with internal child
    /// Both internal - should work fine
    /// </summary>
    [ClassWithNamespaces]
    internal partial class InternalRootInternalChild
    {
        partial class ChildNamespace { }
    }

    /// <summary>
    /// Test Case 6: Internal root with protected child (should fail)
    /// Can't have protected member in internal class - protected requires inheritance
    /// </summary>
    [ClassWithNamespaces]
    internal partial class InternalRootProtectedChild
    {
        protected partial class ChildNamespace { }
    }

    /// <summary>
    /// Test Case 7: Deep nesting - private -> private -> private
    /// All levels private - should work
    /// </summary>
    [ClassWithNamespaces]
    private partial class DeepPrivateRoot
    {
        partial class Level1Namespace
        {
            partial class Level2Namespace { }
        }
    }

    /// <summary>
    /// Test Case 8: Deep nesting - private -> private -> protected (should fail)
    /// Can't expose protected from private
    /// </summary>
    [ClassWithNamespaces]
    private partial class DeepPrivateWithProtected
    {
        partial class Level1Namespace
        {
            protected partial class Level2Namespace { }
        }
    }

    /// <summary>
    /// Test Case 9: Deep nesting - protected -> private -> internal (complex case)
    /// Level1 inherits protected from root
    /// Level2 specifies internal (explicit overrides inherited)
    /// Level1 property for Level2 will be internal
    /// </summary>
    [ClassWithNamespaces]
    protected partial class DeepMixedAccessibility
    {
        partial class Level1Namespace
        {
            internal partial class Level2Namespace { }
        }
    }

    /// <summary>
    /// Test Case 10: Private root with private child (testing ancestor properties)
    /// Child should be able to reference parent via private ancestor property
    /// </summary>
    [ClassWithNamespaces]
    private partial class PrivateRootWithAncestor
    {
        partial class ChildNamespace
        {
            partial class GrandchildNamespace { }
        }
    }

    /// <summary>
    /// Test Case 11: Protected root with protected child with internal grandchild
    /// Tests mixed accessibility inheritance across multiple levels
    /// </summary>
    [ClassWithNamespaces]
    protected partial class ProtectedComplexHierarchy
    {
        partial class ProtectedLevel1Namespace
        {
            partial class ProtectedLevel2Namespace
            {
                internal partial class InternalLevel3Namespace { }
            }
        }
    }

    /// <summary>
    /// Test Case 12: Testing property accessibility matching
    /// Root class has multiple children with different accessibilities
    /// Each child property should match the child's effective accessibility
    /// </summary>
    [ClassWithNamespaces]
    private partial class MixedChildAccessibility
    {
        partial class PrivateChildNamespace { }
        internal partial class InternalChildNamespace { }
    }

    /// <summary>
    /// Usage test to verify the generated code can be instantiated
    /// NOTE: This test is disabled for now due to accessibility issues being worked on
    /// </summary>
    public static void TestInstantiationDisabled()
    {
        // Test will be enabled once reflection-based factory method implementation is complete
    }
}
