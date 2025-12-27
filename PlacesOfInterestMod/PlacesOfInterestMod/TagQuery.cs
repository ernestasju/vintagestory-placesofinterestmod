using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlacesOfInterestMod;

public sealed class TagQuery
{
    private readonly HashSet<TagName> _includedTagNames;
    private readonly HashSet<TagPattern> _includedTagPatterns;
    private readonly HashSet<TagName> _excludedTagNames;
    private readonly HashSet<TagPattern> _excludedTagPatterns;
    private readonly HashSet<TagName> _additionalExcludedTagNames;
    private readonly int? _startOffset;
    private readonly PeriodUnit _startOffsetUnit;
    private readonly int? _endOffset;
    private readonly PeriodUnit _endOffsetUnit;

    private TagQuery(
        HashSet<TagName> includedTagNames,
        HashSet<TagPattern> includedTagPatterns,
        HashSet<TagName> excludedTagNames,
        HashSet<TagPattern> excludedTagPatterns,
        HashSet<TagName> additionalExcludedTagNames,
        int? startDayOffset,
        PeriodUnit startOffsetUnit,
        int? endDayOffset,
        PeriodUnit endOffsetUnit)
    {
        _includedTagNames = includedTagNames;
        _includedTagPatterns = includedTagPatterns;
        _excludedTagNames = excludedTagNames;
        _excludedTagPatterns = excludedTagPatterns;
        _additionalExcludedTagNames = additionalExcludedTagNames;
        _startOffset = startDayOffset;
        _startOffsetUnit = startOffsetUnit;
        _endOffset = endDayOffset;
        _endOffsetUnit = endOffsetUnit;
    }

    public IEnumerable<TagName> IncludedTagNames => _includedTagNames;

    public IEnumerable<TagPattern> IncludedTagPatterns => _includedTagPatterns;

    public IEnumerable<TagName> ExcludedTagNames => _excludedTagNames;

    public IEnumerable<TagPattern> ExcludedTagPatterns => _excludedTagPatterns;

    public bool HasIncludedTagNames()
    {
        return _includedTagNames.Count > 0;
    }

    public TagQueryWithDays WithDays(
        PlayerCalendar calendar)
    {
        int startDay = 0;
        if (_startOffset is not null && _startOffset > 0)
        {
            startDay = calendar.CalculateDay(_startOffset.Value, _startOffsetUnit);
        }

        int endDay = 0;
        if (_endOffset is not null && _endOffset >= 0)
        {
            endDay = calendar.CalculateDay(_endOffset.Value, _endOffsetUnit);
        }

        return WithDays(calendar.Today, startDay, endDay);
    }

    public TagQueryWithDays WithDays(
        int day,
        int startDay,
        int endDay)
    {
        return new(
            _includedTagNames,
            _includedTagPatterns,
            _excludedTagNames,
            _excludedTagPatterns,
            _additionalExcludedTagNames,
            day,
            startDay,
            endDay);
    }

    public static (TagQuery SearchTagQuery, TagQuery UpdateTagQuery) ParseSearchAndUpdate(string input)
    {
        string cleanedInput = input.Trim();
        if (!input.Contains(" -> "))
        {
            cleanedInput = " -> " + cleanedInput;
        }

        string[] parts = cleanedInput.Split(" -> ", StringSplitOptions.TrimEntries);

        TagQuery searchQuery = Parse(parts[0]);
        TagQuery updateQuery = Parse(parts[1]);

        return (searchQuery, updateQuery);
    }

    public static TagQuery Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new([], [], [], [], [], 0, PeriodUnit.Day, 0, PeriodUnit.Day);
        }

        HashSet<TagName> includedTagNames = [];
        HashSet<TagPattern> includedTagPatterns = [];
        HashSet<TagName> excludedTagNames = [];
        HashSet<TagPattern> excludedTagPatterns = [];
        HashSet<TagName> additionalExcludedTagNames = [];
        int? startOffset = null;
        PeriodUnit startOffsetUnit = PeriodUnit.Day;
        int? endOffset = null;
        PeriodUnit endOffsetUnit = PeriodUnit.Day;

        foreach (string token in input.Split(" ", StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = Regex.Match(token.ToLower(), @"^(?<Sign>[\+\-])?(?:(?<Number>0|[1-9]\d*)(?<Unit>[yqmwd])|(?<Tag>.*))$");
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

                if (sign != "" && number == 0)
                {
                    continue;
                }

                PeriodUnit unit = match.Groups["Unit"].Value switch
                {
                    "y" => PeriodUnit.Year,
                    "q" => PeriodUnit.Quarter,
                    "m" => PeriodUnit.Month,
                    "w" => PeriodUnit.ResinWeek, // NOTE: Vintage Story month can be as short as 3 days, but the resin always respawns after 7 days.
                    "d" => PeriodUnit.Day,

                    // NOTE: Unreachable due to regex.
                    _ => PeriodUnit.Day,
                };

                if (sign is "+" or "-")
                {
                    // NOTE: Saying I don't way to see this place for 7 days is equivalent to saying I want to see it in 7 days.
                    startOffset = number;
                    startOffsetUnit = unit;
                    continue;
                }

                if (sign == "")
                {
                    endOffset = number;
                    endOffsetUnit = unit;
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

        return new(
            includedTagNames,
            includedTagPatterns,
            excludedTagNames,
            excludedTagPatterns,
            additionalExcludedTagNames,
            startOffset,
            startOffsetUnit,
            endOffset,
            endOffsetUnit);
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
