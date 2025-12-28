# Quick Start Guide

## What Was Done

✅ **Refactored ServerChatCommands** to separate parameter parsing from business logic
✅ **Created comprehensive tests** for the `/tag` command handler
✅ **All documentation** explaining the changes

## Before You Start

Make sure you have the `VINTAGE_STORY` environment variable set:

**Windows PowerShell**:
```powershell
$env:VINTAGE_STORY = "C:\path\to\VintageStory"
```

**Linux/Mac Bash**:
```bash
export VINTAGE_STORY=/path/to/VintageStory
```

## The 3-Minute Setup

1. **Rename the test file**:
   ```
   HandleCommandTagInterestingPlace_Tests.cs.txt  →  HandleCommandTagInterestingPlace_Tests.cs
   ```

2. **Build**:
   ```bash
   dotnet build
   ```

3. **Run Tests**:
   ```bash
   dotnet test PlacesOfInterestMod.IntegrationTests
   ```

Expected output: **8 tests pass**

## Key Files Changed

| File | Change |
|------|--------|
| `ServerChatCommands.cs` | Added `RegisterChatCommands()`, updated handler signatures |
| `PlacesOfInterestModSystem.cs` | Simplified to call `_serverChatCommands.RegisterChatCommands()` |

## New Test File

| File | Location |
|------|----------|
| `HandleCommandTagInterestingPlace_Tests.cs` | `PlacesOfInterestMod.IntegrationTests/` |

(Provided as `.cs.txt` - rename to `.cs` to use)

## The 8 Tests

```csharp
✅ NoTagsWithNoParameters_ReturnsNothingToAdd
✅ NewPlaceWithSingleTag_AddsPlaceWithTag
✅ NewPlaceWithExcludedTag_DoesNotAddPlace
✅ ExistingPlaceIncludeAndExcludeSameTag_RemovesTagFromPlace
✅ ExistingPlaceIncludeAndExcludeDifferentTags_ChangesPlaceTags
✅ NewPlaceWithMultipleTags_AddsPlaceWithAllTags
✅ ExistingPlaceAddTag_UpdatesPlaceWithAdditionalTag
✅ ExistingPlaceRemoveAllTags_DeletesPlace
```

## What Changed in Handlers

### Before
```csharp
public TextCommandResult HandleCommandTagInterestingPlace(TextCommandCallingArgs args)
{
    PlayerPlacesOfInterest poi = new(args.Caller.Player);
    TagQuery tagQuery = TagQuery.Parse(args.LastArg?.ToString() ?? "");
    // ... logic ...
}
```

### After
```csharp
public TextCommandResult HandleCommandTagInterestingPlace(
    PlayerPlacesOfInterest poi,
    TagQuery tagQuery,
    Vec3d playerPos)
{
    // ... logic ...
}
```

**Benefit**: Now testable! Just call it with domain objects.

## Example Test

```csharp
[Fact]
public void NewPlaceWithSingleTag_AddsPlaceWithTag()
{
    // Setup
    var (poi, storage) = CreateTestPoiWithPlaces([]);
    var tagQuery = TagQuery.Parse("x");

    // Act - No mocking the chat system!
    var result = _sut.HandleCommandTagInterestingPlace(poi, tagQuery, new Vec3d(0, 0, 0));

    // Assert
    result.Should().NotBeNull();
    poi.Places.All.ToList().Should().HaveCount(1);
}
```

## Documentation Files

1. **REFACTORING_SUMMARY.md** - Overview
2. **REFACTORING_DETAILS.md** - Technical details
3. **VISUAL_OVERVIEW.md** - Diagrams and visuals
4. **TESTING_INSTRUCTIONS.md** - How to run tests
5. **COMPLETION_REPORT.md** - Full report
6. **COMPLETION_CHECKLIST.md** - What was done

## Common Issues

**Tests don't appear?**
- Ensure VINTAGE_STORY is set
- Run `dotnet clean` then `dotnet build`
- Refresh Test Explorer (Ctrl+R in Visual Studio)

**Build fails with "Vintagestory not found"?**
- Check VINTAGE_STORY environment variable
- Make sure it points to a valid Vintage Story installation
- Restart your IDE after setting the variable

**Tests won't run?**
- Make sure test file is named `.cs` not `.cs.txt`
- Verify VINTAGE_STORY is still set
- Clean and rebuild

## Success Indicators

✅ Build succeeds for both projects
✅ Test Explorer shows 8 tests
✅ All 8 tests pass
✅ Test output shows green checkmarks

## What's Next

Now that handlers are testable:
- Add more tests for edge cases
- Test other handler methods
- Test error conditions
- Verify all command behaviors

## Support Files Included

```
📦 Deliverables
├─ 📄 Refactored ServerChatCommands.cs
├─ 📄 Updated PlacesOfInterestModSystem.cs
├─ 📝 HandleCommandTagInterestingPlace_Tests.cs.txt (→ rename to .cs)
├─ 📄 REFACTORING_SUMMARY.md
├─ 📄 REFACTORING_DETAILS.md
├─ 📄 VISUAL_OVERVIEW.md
├─ 📄 TESTING_INSTRUCTIONS.md
├─ 📄 COMPLETION_REPORT.md
├─ 📄 COMPLETION_CHECKLIST.md
└─ 📄 QUICK_START_GUIDE.md (this file)
```

## TL;DR

1. Set `VINTAGE_STORY` environment variable
2. Rename test file: `HandleCommandTagInterestingPlace_Tests.cs.txt` → `HandleCommandTagInterestingPlace_Tests.cs`
3. Build: `dotnet build`
4. Test: `dotnet test`
5. ✅ All 8 tests pass

Done! Handlers are now testable.
