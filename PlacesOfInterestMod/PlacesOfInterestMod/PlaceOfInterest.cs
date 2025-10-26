using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod
{
    [ProtoContract]
    public class PlaceOfInterest
    {
        [ProtoMember(1)]
        public required Vec3d XYZ { get; init; }

        [ProtoMember(2)]
        public required HashSet<string> Tags { get; init; }

        public bool MatchesTags(
            string[] includedTags,
            string[] excludedTags)
        {
            return includedTags.All(tag => Tags.Contains(tag)) && !excludedTags.Any(tag => Tags.Contains(tag));
        }
    }
}
