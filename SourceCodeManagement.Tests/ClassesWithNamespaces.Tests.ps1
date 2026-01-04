function Should-BeDiffAssertion($ActualValue, $ExpectedValue, [switch]$Negate, [string]$Because) {
    [bool] $Succeeded = $ActualValue -ceq $ExpectedValue

    if ($Negate) {
        $Succeeded = -not $Succeeded
    }

    if ($true -eq $Succeeded) {
        return [Pester.ShouldResult]@{ Succeeded = $Succeeded }
    }

    # ANSI colors
    $Reset  = "`e[0m"
    $Red    = "`e[31m"
    $Green  = "`e[32m"
    $Yellow = "`e[33m"
    $Dim    = "`e[2m"
    $BoldRed = "`e[1;31m"

    # Colorize diff content: + additions green, - removals red, context dim
    $Colored = $(
        ($ActualValue -split "`r?`n") |
        ForEach-Object -Process {
            switch -Regex ($_) {
                '^\+' { "$Green$_$Reset"; break; }
                '^- ' { "$Red$_$Reset"; break; }
                '^@@' { "$Yellow$_$Reset"; break; }
                default { "$Dim$_$Reset"; break; }
            }
        }
    ) -join "`n"

    $FailureMessage = "${BoldRed}Diff is not empty.${Reset}`nActual result:`n$Dim$ActualValue$Reset`n${BoldRed}Diff:$Reset`n$Colored"

    return [Pester.ShouldResult]@{
        Succeeded      = $Succeeded
        FailureMessage = $FailureMessage
        ExpectResult   = @{
            Actual   = $ActualValue
            Expected = $ExpectedValue
            Because  = $Because
        }
    }
}

if (-not $Global:AddedBeDiffAssertion)
{
    Add-ShouldOperator `
        -Name BeDiff `
        -InternalName Should-BeDiffAssertion `
        -Test ${function:Should-BeDiffAssertion}

    $Global:AddedBeDiffAssertion = $true
}

function global:RunTest($Code, $ExpectedResult) {
    $Code | Out-File -LiteralPath './TestRunner.cs' -Force
    $ExpectedResult | Out-File -LiteralPath './expected.txt' -Force

    $ActualResult = @(
        '===== BUILD OUTPUT ====='
        dotnet build . -verbosity minimal -c Debug /p:DefineConstants=USE_TEST_RUNNER 2>&1
        if ($LASTEXITCODE -eq 0) {
            '===== RUN OUTPUT ====='
            dotnet run --no-build --no-restore -verbosity quiet 2>&1
        }
    ) -split "`r?`n"
    
    # NOTE: Remove irrelevant stuff and simplify output.
    $Here = (Get-Location).Path
    $ActualResult |
    Where-Object -FilterScript {
        if ($_ -match '^\s*Time Elapsed') { return $false }
        if ($_ -match 'warning\s*\w+') { return $false }
        if ($_ -match '\d+\s+Warning\(s\)') { return $false }
        if ($_ -like '*Determining projects to restore...*') { return $false }
        if ($_ -like '*All projects are up-to-date for restore.*') { return $false }
        if ($_ -match 'SourceCodeManagement\s+->') { return $false }
        if ($_ -match 'SourceCodeManagement\.Tests\s+->') { return $false }
        return $true

    } |
    Foreach-Object -Process {
        # NOTE: Remove line numbers and path parts to this folder
        ($_ -replace '\(\d+,\d+\)', '').Replace($Here, '.')
    } |
    Out-File -LiteralPath './actual.txt' -Force

    $Diff = (git diff -U99999 --no-index -- './actual.txt' './expected.txt' 2>&1) -join "`n"
    $Diff | Should -BeDiff ''
}

Describe 'ClassWithNamespaces Code Generator' {

    It 'TEST_NO_CHILD_NAMESPACES' {
        $Code = @'
#if USE_TEST_RUNNER
using SourceCodeManagement;
class TestRunner
{
    public static void RunTests()
    {
        var root = new TestClass();
        _ = root.Test; // generated namespace exists
    }
}

[ClassWithNamespaces]
partial class TestClass
{
    internal partial class TestNamespace {}
}
#endif
'@
        $Snapshot = @'
'@
        RunTest $Code $Snapshot
    }

    It 'TEST_NO_GENERATION' {
        $Code = @'
#if USE_TEST_RUNNER
using SourceCodeManagement;
class TestRunner
{
    public static void RunTests()
    {
        var container = new Container();
        _ = container.Config; // generated property for namespace
    }
}

[ClassWithNamespaces]
partial class Container
{
    public partial class ConfigNamespace {}
}
#endif
'@
        $Snapshot = @'
'@
        RunTest $Code $Snapshot
    }

    It 'TEST_BASIC_HIERARCHY' {
        $Code = @'
#if USE_TEST_RUNNER
using SourceCodeManagement;
class TestRunner
{
    public static void RunTests()
    {
        var app = new Application();
        _ = app.Settings.Database; // nested namespaces
    }
}

[ClassWithNamespaces]
partial class Application
{
    public partial class SettingsNamespace
    {
        public partial class DatabaseNamespace {}
    }
}
#endif
'@
        $Snapshot = @'
'@
        RunTest $Code $Snapshot
    }

    It 'TEST_INVALID_PUBLIC_CHILD_IN_PRIVATE_ROOT' {
        $Code = @'
#if USE_TEST_RUNNER
using SourceCodeManagement;
class TestRunner
{
    public static void RunTests() {}
}

[ClassWithNamespaces]
partial class PrivateRoot
{
    public partial class PublicChildNamespace {}
}
#endif
'@
        $Snapshot = @'
'@
        RunTest $Code $Snapshot
    }

    It 'TEST_INVALID_PROTECTED_IN_INTERNAL' {
        $Code = @'
#if USE_TEST_RUNNER
using SourceCodeManagement;
class TestRunner
{
    public static void RunTests() {}
}

[ClassWithNamespaces]
internal partial class InternalRoot
{
    protected partial class ProtectedChildNamespace {}
}
#endif
'@
        $Snapshot = @'
'@
        RunTest $Code $Snapshot
    }

    It 'TEST_INVALID_NO_NAMESPACE_SUFFIX' {
        $Code = @'
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
'@
        $Snapshot = @'
'@
        RunTest $Code $Snapshot
    }
}
