using SourceCodeManagement;

namespace PlacesOfInterestMod.IntegrationTests;

static partial class ClassesWithNamespacesTestCases // some of this code will not compile and that is expected
{
    public static void TestGeneration()
    {
        var t0 = new Demo0();
        _ = t0.Some; // should be generated

        var t1 = new Demo0();
        _ = t1.Some; // should be generated - the generated class should have partial modifier and the compiler will complain to user to add the missing partial modifier

        var t2 = new Demo1();
        _ = t2.X; // should be generated and user should get a compiler error to add missing partial modifier
        _ = t2.X.Y; // should be generated
        _ = t2.X.Z; // should be generated

        var t3 = new Demo2();
        _ = t3.W; // should be generated
        _ = t3.Q; // should be generated
    }

    [ClassWithNamespaces]
    class Demo0 // should generate class with only partial modifier
    {
        //private Demo0()
        //{
        //}

        partial class SomeNamespace // should generate class with private modifier
        {
            //public SomeNamespace(Demo0 parent)
            //{
            //    _parent = parent;
            //}
        }

        partial class Namespace // should also not generate anything because there is no namespace name
        {
        }

        partial class NotAName_space // this subclass tree should be ignored
        {
            partial class SomeOtherNamespace // this should be ignored too
            {
            }
        }
    }

    [ClassWithNamespaces]
    protected partial class Demo1
    {
        // Should generate subclass but then the compiler will show error which is ok

        class SomeNamespace
        {
        }

        partial class XNamespace
        {
            // should be protected - accessibility modifier inherited from Demo class

            sealed partial class YNamespace // should also be protected because XNamespace is protected. Generated class should not be sealed because it is enough to specify the modifier here.
            {
            }

            internal partial class ZNamespace // should be internal but not sealed - the accessibility modifier is specified here
            {
            }
        }
    }

    [ClassWithNamespaces]
    partial class Demo2
    {
        sealed partial class WNamespace // generated class does not need any modifiers other than private inherited from Demo2
        {
            internal partial class QNamespace // generated class should have internal modifier
            {
            }
        }
    }
}

static partial class ClassesWithNamespacesTestCases2 // some of this code will not compile and that is expected
{
    public static void TestGeneration()
    {
        var t0 = new Demo0();
        _ = t0.Some; // should be generated

        var t1 = new Demo0();
        _ = t1.Some; // should be generated - the generated class should have partial modifier and the compiler will complain to user to add the missing partial modifier

        var t2 = new Demo1();
        _ = t2.X; // should be generated and user should get a compiler error to add missing partial modifier
        _ = t2.X.Y; // should be generated
        _ = t2.X.Z; // should be generated

        var t3 = new Demo2();
        _ = t3.W; // should be generated
        _ = t3.Q; // should be generated
    }

    [ClassWithNamespaces]
    class Demo0 // should generate class with only partial modifier
    {
        partial class SomeNamespace // should generate class with private modifier
        {
        }
    }

    [ClassWithNamespaces]
    protected partial class Demo1
    {
        // Should generate subclass but then the compiler will show error which is ok

        class SomeNamespace
        {
        }

        partial class XNamespace
        {
            // should be protected - accessibility modifier inherited from Demo class

            sealed partial class YNamespace // should also be protected because XNamespace is protected. Generated class should not be sealed because it is enough to specify the modifier here.
            {
            }

            internal partial class ZNamespace // should be internal but not sealed - the accessibility modifier is specified here
            {
            }
        }
    }

    [ClassWithNamespaces]
    partial class Demo2
    {
        sealed partial class WNamespace // generated class does not need any modifiers other than private inherited from Demo2
        {
            internal partial class QNamespace // generated class should have internal modifier
            {
            }
        }
    }
}
