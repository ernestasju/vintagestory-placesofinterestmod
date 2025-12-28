using System.Collections.Generic;
using System.Linq;

namespace PlacesOfInterestMod;

public sealed class TagQueryWithDays
{
    private readonly HashSet<TagName> _includedTagNames;
    private readonly HashSet<TagPattern> _includedTagPatterns;
    private readonly HashSet<TagName> _excludedTagNames;
    private readonly HashSet<TagPattern> _excludedTagPatterns;
    private readonly HashSet<TagName> _additionalExcludedTagNames;
    private readonly int _day;
    private readonly int _startDay;
    private readonly int _endDay;

    public TagQueryWithDays(
        HashSet<TagName> includedTagNames,
        HashSet<TagPattern> includedTagPatterns,
        HashSet<TagName> excludedTagNames,
        HashSet<TagPattern> excludedTagPatterns,
        HashSet<TagName> additionalExcludedTagNames,
        int day,
        int startDay,
        int endDay)
    {
        _includedTagNames = includedTagNames;
        _includedTagPatterns = includedTagPatterns;
        _excludedTagNames = excludedTagNames;
        _excludedTagPatterns = excludedTagPatterns;
        _additionalExcludedTagNames = additionalExcludedTagNames;
        _day = day;
        _startDay = startDay;
        _endDay = endDay;
    }

    public IEnumerable<TagName> IncludedTagNames => _includedTagNames;

    public IEnumerable<TagPattern> IncludedTagPatterns => _includedTagPatterns;

    public IEnumerable<TagName> ExcludedTagNames => _excludedTagNames;

    public IEnumerable<TagPattern> ExcludedTagPatterns => _excludedTagPatterns;

    public static TagQueryWithDays CreateForUpdate(
        IEnumerable<TagName> includedTagNames,
        IEnumerable<TagName> excludedTagNames,
        bool replace,
        int day,
        int startDay,
        int endDay)
    {
        return new(
            includedTagNames.ToHashSet(),
            [],
            excludedTagNames.ToHashSet(),
            replace ? [new TagPattern(TagPatternType.Wildcard, "*")] : [],
            [],
            day,
            startDay,
            endDay);
    }

    public bool HasIncludedTagNames()
    {
        return _includedTagNames.Count > 0;
    }

    public bool TestPlace(Place place)
    {
        HashSet<TagName> tagNames = place.CalculateActiveTagNames(_day).ToHashSet();

#pragma warning disable T0010 // Internal Styling Rule T0010
        return
            _includedTagNames.All(x => tagNames.Contains(x)) &&
            _includedTagPatterns.All(pattern => tagNames.Any(tagName => pattern.Test(tagName))) &&
            !_excludedTagNames.Any(x => tagNames.Contains(x)) &&
            !_excludedTagPatterns.Any(pattern => tagNames.Any(tagName => pattern.Test(tagName))) &&
            !_additionalExcludedTagNames.Any(x => tagNames.Contains(x));
#pragma warning restore T0010 // Internal Styling Rule T0010
    }

    public void UpdatePlace(Place place, bool allowRemove, out bool tagsChanged)
    {
        tagsChanged = false;

        List<Tag> newTags = [];
        foreach (Tag tag in place.Tags)
        {
            if (_includedTagNames.Contains(tag.Name) || _includedTagPatterns.Any(x => x.Test(tag.Name)))
            {
                if (tag.StartDay != _startDay || tag.EndDay != _endDay)
                {
                    tagsChanged = true;
                    newTags.Add(new()
                    {
                        Name = tag.Name,
                        StartDay = _startDay,
                        EndDay = _endDay,
                    });
                    continue;
                }

                newTags.Add(tag);
                continue;
            }

            if (allowRemove &&
                (_excludedTagNames.Contains(tag.Name) || _excludedTagPatterns.Any(x => x.Test(tag.Name))))
            {
                tagsChanged = true;
                continue;
            }

            newTags.Add(tag);
        }

        foreach (TagName tagName in _includedTagNames)
        {
            if (newTags.Any(x => x.Name == tagName))
            {
                continue;
            }

            tagsChanged = true;
            newTags.Add(new()
            {
                Name = tagName,
                StartDay = _startDay,
                EndDay = _endDay,
            });
        }

        if (tagsChanged)
        {
            place.ReplaceTags(newTags);
        }
    }
}
