using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

[ProtoContract]
public class PlaceOfInterest
{
    [ProtoMember(1)]
    public required Vec3d XYZ { get; init; }

    [ProtoMember(2)]
    public required List<Tag> Tags { get; init; }

    public void Validate(bool allowNoTags = false)
    {
        if (!allowNoTags && Tags.Count == 0)
        {
            throw new ArgumentException("PlaceOfInterest has no tags");
        }

        if (Tags.Any(x => string.IsNullOrEmpty(x.Name)))
        {
            throw new ArgumentException("PlaceOfInterest has a tag with an empty name");
        }
    }

    public IEnumerable<Tag> CalculateActiveTags(int day)
    {
        return Tags.Where(x => x.IsActive(day));
    }

    public IEnumerable<string> CalculateActiveTagNames(int day)
    {
        return CalculateActiveTags(day).Select(x => x.Name);
    }

    public bool MatchesTags(
        int day,
        string[] includedTags,
        string[] excludedTags,
        bool wildcardsInIncludedTags = false,
        bool wildcardInExcludedTags = false)
    {
        HashSet<string> activeTags = CalculateActiveTagNames(day).ToHashSet();

        if (!includedTags.ContainsAny(["excluded", "hidden", "ignored"]))
        {
            excludedTags = ["excluded", "hidden", "ignored", .. excludedTags];
        }

        return
            activeTags.ContainsAll(includedTags, wildcardMatching: wildcardsInIncludedTags) &&
            !activeTags.ContainsAny(excludedTags, wildcardMatching: wildcardInExcludedTags);
    }

    public void UpdateTags(
        string[] tagsToRemove,
        string[] tagsToAdd,
        int startDay,
        int endDay)
    {
        foreach (string tagName in tagsToRemove)
        {
            Tags.RemoveAll(x => x.Name.MatchesPattern(tagName, caseInsensitive: true, wildcardMatching: true));
        }

        Dictionary<string, Tag> existingTags = Tags.ToDictionary(x => x.Name);
        foreach (string tagName in tagsToAdd)
        {
            if (existingTags.TryGetValue(tagName, out Tag? tag))
            {
                tag.StartDay = startDay;
                tag.EndDay = endDay;
            }
            else
            {
                Tags.Add(new Tag()
                {
                    Name = tagName,
                    StartDay = startDay,
                    EndDay = endDay,
                });
            }
        }

        Validate(allowNoTags: true);
    }
}
