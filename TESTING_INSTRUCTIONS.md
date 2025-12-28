# How to Run the Tests

## Prerequisites

1. **VINTAGE_STORY Environment Variable**
   - This must point to your Vintage Story installation directory
   - Example: `C:\Games\VintageStory` or `/home/user/VintageStory`
   - The build system uses this to locate VintagestoryAPI.dll and other required assemblies

## Setup Steps

### Step 1: Ensure VINTAGE_STORY is Set
   
**Windows (PowerShell)**:
```powershell
$env:VINTAGE_STORY = "C:\path\to\VintageStory"
# Or permanently:
[Environment]::SetEnvironmentVariable("VINTAGE_STORY", "C:\path\to\VintageStory", "User")
```

**Linux/Mac**:
```bash
export VINTAGE_STORY=/path/to/VintageStory
# Or permanently in ~/.bashrc or ~/.zshrc:
echo 'export VINTAGE_STORY=/path/to/VintageStory' >> ~/.bashrc
```

### Step 2: Prepare Test File

1. Copy `HandleCommandTagInterestingPlace_Tests.cs.txt` to `HandleCommandTagInterestingPlace_Tests.cs` in the test project:
   ```
   PlacesOfInterestMod.IntegrationTests/HandleCommandTagInterestingPlace_Tests.cs
   ```

2. Remove the `.txt` file

### Step 3: Build the Tests

```bash
dotnet build PlacesOfInterestMod.IntegrationTests/PlacesOfInterestMod.IntegrationTests.csproj
```

### Step 4: Run the Tests

**Using Visual Studio Test Explorer**:
- Open Test Explorer (Test > Test Explorer)
- All tests should appear under "HandleCommandTagInterestingPlace_Tests"
- Click "Run All" or run individual tests

**Using dotnet CLI**:
```bash
dotnet test PlacesOfInterestMod.IntegrationTests/PlacesOfInterestMod.IntegrationTests.csproj
```

**Using xUnit runner directly**:
```bash
# After building
dotnet test
```

## Test Cases

The test suite includes 8 comprehensive tests:

1. ✅ `NoTagsWithNoParameters_ReturnsNothingToAdd` - Empty command
2. ✅ `NewPlaceWithSingleTag_AddsPlaceWithTag` - Add single tag "x" to new place
3. ✅ `NewPlaceWithExcludedTag_DoesNotAddPlace` - Try to add "-x" to new place (should not create)
4. ✅ `ExistingPlaceIncludeAndExcludeSameTag_RemovesTagFromPlace` - Include "x" and exclude "x" from place with "x"
5. ✅ `ExistingPlaceIncludeAndExcludeDifferentTags_ChangesPlaceTags` - Include "y" and exclude "x" from place with "x"
6. ✅ `NewPlaceWithMultipleTags_AddsPlaceWithAllTags` - Add "x y z" to new place
7. ✅ `ExistingPlaceAddTag_UpdatesPlaceWithAdditionalTag` - Add "y" to place with "x"
8. ✅ `ExistingPlaceRemoveAllTags_DeletesPlace` - Exclude "x" from place with only "x"

## Expected Test Results

All tests should pass, covering these scenarios:

### New Place Scenarios
- **No parameters**: Returns "nothing to add" message
- **Single tag**: Place is created with that tag
- **Excluded tag only**: Place is not created
- **Multiple tags**: Place is created with all tags

### Existing Place Scenarios  
- **Include & exclude same tag**: Tag is removed (place deleted if only tag)
- **Include & exclude different tags**: Old tag removed, new tag added
- **Add new tag**: Place updated with additional tag
- **Remove all tags**: Place is deleted

## Troubleshooting

### Issue: "Vintagestory API not found"
**Solution**: Verify VINTAGE_STORY environment variable is set correctly
```bash
# Windows
echo %VINTAGE_STORY%

# Linux/Mac
echo $VINTAGE_STORY
```

### Issue: Tests don't appear in Test Explorer
**Solution**: 
1. Clean the build: `dotnet clean`
2. Rebuild: `dotnet build`
3. Refresh Test Explorer (Ctrl+R in Visual Studio)

### Issue: Mock setup errors
**Solution**: Ensure you have:
- `Moq` NuGet package (version 4.20.72 or later)
- `FluentAssertions` NuGet package (version 8.8.0 or later)

Run: `dotnet restore`

## Project Structure

```
PlacesOfInterestMod.IntegrationTests/
├── PlacesOfInterestMod.IntegrationTests.csproj
├── PlaceholderTests.cs  (original placeholder)
├── HandleCommandTagInterestingPlace_Tests.cs  ← Add this file
└── HandleCommandTagInterestingPlace_Tests.cs.txt  ← Reference file (rename and use above)
```

## Architecture of Tests

### Key Classes

**`HandleCommandTagInterestingPlace_Tests`** - Main test class
- Contains 8 test methods
- Tests the `/tag` command handler directly
- Uses test helpers to create mock objects

**`TestWorldDataStorage`** - Test helper
- Simulates Vintage Story's IWorldData without API dependencies
- Stores and retrieves ProtoPlace instances
- Allows tests to verify that places are properly saved

### Mock Setup

Tests create mocks for:
- `IPlayer` - Player interface
- `Entity` - Entity with position
- `IWorldData` - Data storage
- `IWorldAccessor` - World access
- `ICalendar` - Calendar for tracking days

All mocks are configured to work together seamlessly, allowing tests to:
1. Create test PlayerPlacesOfInterest instances
2. Set initial places
3. Call handlers
4. Verify changes to places

## Validation

To verify refactoring is working correctly:

```bash
# Build the main project
dotnet build PlacesOfInterestMod/PlacesOfInterestMod/PlacesOfInterestMod.csproj

# Build tests
dotnet build PlacesOfInterestMod.IntegrationTests/PlacesOfInterestMod.IntegrationTests.csproj

# Run tests  
dotnet test PlacesOfInterestMod.IntegrationTests/PlacesOfInterestMod.IntegrationTests.csproj -v detailed
```

All should succeed without errors.
