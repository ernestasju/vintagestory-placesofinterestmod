#if USE_TEST_RUNNER
using SourceCodeManagement;
class TestRunner
{
    public static void RunTests()
    {
        var root = new RootWithoutSuffix();
        // no generated properties expected
    }
}

[ClassWithNamespaces]
partial class RootWithoutSuffix
{
    public partial class ChildWithoutSuffix {}
}
#endif
