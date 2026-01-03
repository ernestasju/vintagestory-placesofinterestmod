#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test runner for ClassWithNamespaces code generator
.DESCRIPTION
    Automatically detects test cases from Program.cs, builds project, and runs all tests
.EXAMPLE
    .\run-tests.ps1
#>

$ErrorActionPreference = 'Stop'

# Colors for output
$InfoColor = 'Cyan'
$ErrorColor = 'Red'

Write-Host "========== ClassWithNamespaces Code Generator Test Runner ==========" -ForegroundColor $InfoColor

$ProjectPath = 'SourceCodeManagement.Tests'
$ProgramFile = Join-Path $ProjectPath 'Program.cs'
$Configuration = 'Debug'

# Read Program.cs and extract test case directives
Write-Host "Detecting test cases from Program.cs..." -ForegroundColor $InfoColor

if (-not (Test-Path $ProgramFile)) {
    Write-Host "Error: $ProgramFile not found" -ForegroundColor $ErrorColor
    exit 1
}

$content = Get-Content $ProgramFile -Raw
$testCases = @()

# Extract all #if TEST_* directives
$matches = [regex]::Matches($content, '#if\s+(TEST_\w+)')
foreach ($match in $matches) {
    $testCase = $match.Groups[1].Value
    if ($testCase -notin $testCases) {
        $testCases += $testCase
    }
}

if ($testCases.Count -eq 0) {
    Write-Host "No test cases found in $ProgramFile" -ForegroundColor $ErrorColor
    exit 1
}

Write-Host "Found test cases: $($testCases -join ', ')`n" -ForegroundColor $InfoColor

try {
    Write-Host "Building project..." -ForegroundColor $InfoColor
    Write-Host "Define Constants: $($testCases -join ';')`n" -ForegroundColor $InfoColor
    
    $buildArgs = @(
        'build',
        $ProjectPath,
        '-c', $Configuration,
        "/p:DefineConstants=$($testCases -join ';')",
        '--no-restore'
    )
    
    & dotnet @buildArgs 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor $ErrorColor
        exit 1
    }
    
    Write-Host "`nRunning all tests...`n" -ForegroundColor $InfoColor
    
    $exePath = ".\$ProjectPath\bin\$Configuration\net8.0\SourceCodeManagement.Tests.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Host "Executable not found at $exePath" -ForegroundColor $ErrorColor
        exit 1
    }
    
    & $exePath
    
    Write-Host "`n========== Test Run Complete ==========" -ForegroundColor $InfoColor
}
catch {
    Write-Host "Error: $_" -ForegroundColor $ErrorColor
    exit 1
}
