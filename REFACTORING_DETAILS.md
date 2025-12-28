# Refactoring Overview: ServerChatCommands Parameter Parsing

## Before: Parameter Parsing in Handlers

```csharp
public TextCommandResult HandleCommandTagInterestingPlace(TextCommandCallingArgs args)
{
    // Had to parse arguments inside the handler
    PlayerPlacesOfInterest poi = new(args.Caller.Player);
    TagQuery tagQuery = TagQuery.Parse(args.LastArg?.ToString() ?? "");
    Places placesCloseToPlayer = poi.Places.All.AtPlayerPosition();
    // ... rest of logic
}
```

**Problem**: Hard to test because handler depends on TextCommandCallingArgs (Vintage Story API type)

## After: Parameter Parsing in Registration

### Handler (now testable):
```csharp
public TextCommandResult HandleCommandTagInterestingPlace(
    PlayerPlacesOfInterest poi, 
    TagQuery tagQuery)
{
    // Pure logic, no parsing needed
    Places placesCloseToPlayer = poi.Places.All.AtPlayerPosition();
    // ... rest of logic
}
```

### Registration (where parsing happens):
```csharp
public void RegisterChatCommands(ICoreServerAPI serverApi)
{
    _ = serverApi.ChatCommands.Create()
        .WithName("interesting")
        .WithAlias("tag")
        .RequiresPlayer()
        .RequiresPrivilege(Privilege.chat)
        .WithDescription(Lang.Get("places-of-interest-mod:interestingCommandDescription"))
        .WithExamples(...)
        .WithArgs(serverApi.ChatCommands.Parsers.OptionalAll("tags"))
        .HandleWith((TextCommandCallingArgs args) =>
        {
            // Parsing happens here
            PlayerPlacesOfInterest poi = new(args.Caller.Player);
            TagQuery tagQuery = TagQuery.Parse(args.LastArg?.ToString() ?? "");
            
            // Then call the testable handler
            return HandleCommandTagInterestingPlace(poi, tagQuery);
        });
}
```

**Benefit**: Now the handler can be tested directly without mocking the entire chat system

## Testing Example

```csharp
[Fact]
public void NewPlaceWithSingleTag_AddsPlaceWithTag()
{
    // Arrange
    var (poi, storage) = CreateTestPoiWithPlaces([]);
    var tagQuery = TagQuery.Parse("x");

    // Act
    var result = _sut.HandleCommandTagInterestingPlace(poi, tagQuery, new Vec3d(0, 0, 0));

    // Assert
    result.Should().NotBeNull();
    var places = poi.Places.All.ToList();
    places.Should().HaveCount(1);
    places[0].Tags[0].Name.Value.Should().Be("x");
}
```

**Advantages**:
✅ No need to mock TextCommandCallingArgs
✅ No need to mock the chat command system
✅ Only need to mock PlayerPlacesOfInterest dependencies (IPlayer, IWorldData)
✅ Tests focus on the actual business logic
✅ Clear test names describe the behavior being tested

## Handler Method Signatures Summary

| Method | Parameters |
|--------|-----------|
| `HandleCommandClearInterestingPlaces` | `PlayerPlacesOfInterest` |
| `HandleCommandTagInterestingPlace` | `PlayerPlacesOfInterest, TagQuery` |
| `HandleCommandFindInterestingPlace` | `PlayerPlacesOfInterest, TagQuery` |
| `HandleCommandFindTagsAroundPlayer` | `PlayerPlacesOfInterest, int searchRadius, TagQuery, TagQuery` |
| `HandleCommandEditPlaces` | `PlayerPlacesOfInterest, int searchRadius, TagQuery, TagQuery` |

All handlers are now pure functions that work with domain types instead of API types. The player position is accessed directly from `PlayerPlacesOfInterest.XYZ` when needed.
