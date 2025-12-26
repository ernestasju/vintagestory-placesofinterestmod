using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlacesOfInterestMod;

public sealed class TagQuery
{
    private readonly HashSet<TagName> _includedTagNames;
    private readonly HashSet<TagPattern> _includedTagPatterns;
    private readonly HashSet<TagName> _excludedTagNames;
    private readonly HashSet<TagPattern> _excludedTagPatterns;
    private readonly HashSet<TagName> _additionalExcludedTagNames;
    private readonly int _day;
    private readonly int _startDay;
    private readonly int _endDay;

    private TagQuery(
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

    public static TagQuery CreateForUpdate(
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
            replace ? [new TagPattern("*")] : [],
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

    public void UpdatePlace(Place place, out bool tagsChanged)
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

            if (_excludedTagNames.Contains(tag.Name) || _excludedTagPatterns.Any(x => x.Test(tag.Name)))
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

        place.ReplaceTags(newTags);
    }

    public static TagQuery Parse(string input, int day)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new([], [], [], [], [], day, 0, 0);
        }

        HashSet<TagName> includedTagNames = [];
        HashSet<TagPattern> includedTagPatterns = [];
        HashSet<TagName> excludedTagNames = [];
        HashSet<TagPattern> excludedTagPatterns = [];
        HashSet<TagName> additionalExcludedTagNames = [];
        int? startDayOffset = null;
        int? endDayOffset = null;

        foreach (string token in input.Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = Regex.Match(token.ToLower(), @"^(?<Sign>[\+\-])?(?:(?<Number>[1-9]\d*)(?<Unit>[yqmwd])|(?<Tag>.*))$");
            if (!match.Success)
            {
                continue;
            }

            string sign = match.Groups["Sign"].Value;
            ArgumentNullException.ThrowIfNull(sign);

            if (match.Groups["Number"].Success && match.Groups["Unit"].Success)
            {
                if (!int.TryParse(match.Groups["Number"].Value, out int number))
                {
                    continue;
                }

                number *= match.Groups["Unit"].Value switch
                {
                    // TODO: Use game's calendar system.
                    "y" => 365,
                    "q" => 91,
                    "m" => 30,
                    "w" => 7,
                    "d" => 1,

                    // NOTE: Unreachable due to regex.
                    _ => 0,
                };

                if (sign is "+" or "-")
                {
                    // NOTE: Saying I don't way to see this place for 7 days is equivalent to saying I want to see it in 7 days.
                    startDayOffset = number;
                    continue;
                }

                if (sign == "")
                {
                    endDayOffset = number;
                    continue;
                }
            }

            string tag = match.Groups["Tag"].Value;
            bool wildcard = IsWildcardPattern(tag);
            if (!wildcard && tag.StartsWith('"') && tag.EndsWith('"'))
            {
                tag = tag[1..^1];
            }

            if (sign == "-")
            {
                if (IsWildcardPattern(tag))
                {
                    excludedTagPatterns.Add(new TagPattern(tag));
                }
                else
                {
                    excludedTagNames.Add(new TagName(tag));
                }
            }
            else
            {
                if (IsWildcardPattern(tag))
                {
                    includedTagPatterns.Add(new TagPattern(tag));
                }
                else
                {
                    includedTagNames.Add(new TagName(tag));
                }
            }
        }

        if (!includedTagNames.Contains(TagName.Excluded) && !includedTagNames.Contains(TagName.Hidden) && !includedTagNames.Contains(TagName.Ignored))
        {
            additionalExcludedTagNames = [TagName.Excluded, TagName.Hidden, TagName.Ignored];
        }

        int startDay = 0;
        if (startDayOffset is not null && startDayOffset > 0)
        {
            startDay = day + startDayOffset.Value;
        }

        int endDay = 0;
        if (endDayOffset is not null && endDayOffset >= 0)
        {
            endDay = day + endDayOffset.Value;
        }

        return new(
            includedTagNames,
            includedTagPatterns,
            excludedTagNames,
            excludedTagPatterns,
            additionalExcludedTagNames,
            day,
            startDay,
            endDay);
    }

    private static bool IsWildcardPattern(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith('"') && value.EndsWith('"'))
        {
            return false;
        }

        return Regex.IsMatch(value, @"^(?:\\\*|\\\?|\\\\|[^*?\\])*[*?]");
    }
}
