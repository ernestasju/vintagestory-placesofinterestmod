using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod
{
    [ProtoContract]
    public class OldPlaceOfInterest
    {
        [ProtoMember(1)]
        public required Vec3d XYZ { get; init; }

        [ProtoMember(2)]
        public required HashSet<string> Tags { get; init; }
    }

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
                throw new Exception("PlaceOfInterest has no tags");
            }

            foreach (Tag tag in Tags)
            {
                if (string.IsNullOrEmpty(tag.Name))
                {
                    throw new Exception("PlaceOfInterest has a tag with an empty name");
                }
            }
        }

        public IEnumerable<Tag> GetActiveTags(int day)
        {
            return Tags.Where(tag => tag.IsActive(day));
        }

        public IEnumerable<string> GetActiveTagNames(int day)
        {
            return GetActiveTags(day).Select(x => x.Name);
        }

        public bool MatchesTags(
            int day,
            string[] includedTags,
            string[] excludedTags)
        {
            HashSet<string> activeTags = GetActiveTagNames(day).ToHashSet();

            return includedTags.All(tag => activeTags.Contains(tag)) && !excludedTags.Any(tag => activeTags.Contains(tag));
        }

        public void UpdateTags(
            string[] tagsToAdd,
            int startDay,
            int endDay,
            string[] tagsToRemove)
        {
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
            foreach (string tagName in tagsToRemove)
            {
                Tags.RemoveAll(x => x.Name == tagName);
            }

            Validate(allowNoTags: true);
        }
    }

    [ProtoContract]
    public class Tag
    {
        [ProtoMember(1)]
        public required string Name { get; init; }

        [ProtoMember(2)]
        public int StartDay { get; set; }

        [ProtoMember(3)]
        public int EndDay { get; set; }

        public bool IsActive(int day)
        {
            if (StartDay >= 0 && day < StartDay)
            {
                return false;
            }
            if (EndDay > 0 && day > EndDay)
            {
                return false;
            }
            return true;
        }
    }
}
